# AED casualty, seated working copy (B). Run with Blender 5.2 in background:
#   blender -b --factory-startup --python aed_seated_bake.py
#
# Why this way: the field menu swaps only the mesh of the existing Mannequin_Body (and of the two attached pad models) under the
# patient prefab. A mesh is only interchangeable when it is in the same mesh-local frame and units as the original. So the
# output is the ORIGINAL FBX re-written element by element with only the vertex and normal arrays of the three geometries
# replaced: same FBX version, GlobalSettings (Y up, UnitScaleFactor 100 = metres), node hierarchy, node transforms (identity),
# materials, UVs, indices and import meta. A plain re-write of the unchanged original is checked to be byte-identical first.
#
# Frame (P): the original file coordinates, metres. Unity imports them with its handedness flip and the patient prefab puts the
# model under a localScale.x = -1, so the patient prefab root's local coordinates are these numbers again (the pad anchors of the
# prefab sit on the pad models in these numbers). The original casualty lies on its back: head toward -Z (z = -1.777), feet at
# z = 0, face +Y, back on the floor (y = 0.004).
#
# Pose: the body is the Mixamo Ch36 mannequin of Models/Mannequin/Mannequin.fbx (same 14442 vertices in the same order; arms in
# an A pose); its skin weights drive a seated pose: thighs turned forward 90 deg at the hip joints, shins and feet upright,
# everything above the hips rigid. Then the whole figure is stood up (rotation M: head to +Y, face to -Z, toward the practice
# side where the head was), the hip joints kept where they lay (x, z), the soles on the original floor height. The upper body,
# and with it the two pad models and the pad anchors, moves by one rigid transform T (rotation M, translation T.t).
import sys, os, json, math, array, hashlib
sys.path.insert(0, os.path.join(os.path.dirname(sys.executable), "5.2", "scripts", "addons_core"))
import bpy
from mathutils import Vector, Matrix
from io_scene_fbx import parse_fbx, encode_bin

HERE = os.path.dirname(os.path.abspath(__file__))
MODELS = os.path.dirname(HERE)
SRC = os.path.join(MODELS, "Mannequin_AED_Attached.fbx")
OUT = os.path.join(MODELS, "Mannequin_AED_Seated.fbx")
RIG = os.path.join(os.path.dirname(MODELS), "Mannequin", "Mannequin.fbx")
REPORT = os.path.join(HERE, "SeatedReport.json")
VERSION = "aed-seated-v2"

# ---------- FBX tree: parsed -> writable ----------
def to_elem(e):
    out = encode_bin.FBXElem(e.id)
    for value, t in zip(e.props, e.props_type):
        t = bytes((t,))
        if t == b"Y": out.add_int16(value)
        elif t == b"B": out.add_bool(value)
        elif t == b"C": out.add_char(value)
        elif t == b"Z": out.add_int8(value)
        elif t == b"I": out.add_int32(value)
        elif t == b"L": out.add_int64(value)
        elif t == b"F": out.add_float32(value)
        elif t == b"D": out.add_float64(value)
        elif t == b"S": out.add_string(value)
        elif t == b"R": out.add_bytes(value)
        elif t == b"i": out.add_int32_array(value)
        elif t == b"l": out.add_int64_array(value)
        elif t == b"f": out.add_float32_array(value)
        elif t == b"d": out.add_float64_array(value)
        elif t == b"b": out.add_bool_array(value)
        elif t == b"c": out.add_byte_array(value)
        else: raise ValueError(t)
    for c in e.elems:
        out.elems.append(to_elem(c))
    return out

def write_tree(root, version, path):
    top = encode_bin.FBXElem(b"")
    for e in root.elems:
        top.elems.append(to_elem(e))
    encode_bin.write(path, top, version)

def child(e, name):
    found = [c for c in e.elems if c.id == name]
    assert len(found) == 1, (name, len(found))
    return found[0]

def sha(path):
    return hashlib.sha256(open(path, "rb").read()).hexdigest()

root, version = parse_fbx.parse(SRC)
roundtrip = os.path.join(HERE, "_roundtrip_check.fbx")
write_tree(root, version, roundtrip)
roundtrip_identical = sha(roundtrip) == sha(SRC)
os.remove(roundtrip)
assert roundtrip_identical, "plain re-write of the original is not byte-identical; the output could not be trusted"

objects = child(root, b"Objects")
models = {e.props[1].split(b"\x00")[0]: e for e in objects.elems if e.id == b"Model"}
geoms = {e.props[0]: e for e in objects.elems if e.id == b"Geometry"}
links = [c.props for c in child(root, b"Connections").elems]
def geometry_of(model_name):
    mid = models[model_name].props[0]
    gids = [p[1] for p in links if p[0] == b"OO" and p[2] == mid and p[1] in geoms]
    assert len(gids) == 1
    return geoms[gids[0]]
body_g = geometry_of(b"Mannequin_Body")
pad_g = {b"Pad01_Attached": geometry_of(b"Pad01_Attached"), b"Pad02_Attached": geometry_of(b"Pad02_Attached")}
for m in models.values():
    p70 = [c for c in m.elems if c.id == b"Properties70"]
    assert not any(p.props[0].startswith(b"Lcl ") for c in p70 for p in c.elems), "a model node has a local transform"

def vec_array(a):
    return [Vector((a[i], a[i + 1], a[i + 2])) for i in range(0, len(a), 3)]
body_v = vec_array(child(body_g, b"Vertices").props[0])

# ---------- Ch36 rig, weights and the seated pose in its own frame (Z up, -Y forward) ----------
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=RIG)
ch = bpy.data.objects["Ch36"]; rig = bpy.data.objects["Armature"]
assert len(ch.data.vertices) == len(body_v)
groups = {g.index: g.name.split(":")[-1] for g in ch.vertex_groups}
weights = [[(groups[g.group], g.weight) for g in v.groups] for v in ch.data.vertices]
che = ch.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
rest = [ch.matrix_world @ v.co for v in che.vertices]
bone = lambda n: rig.matrix_world @ rig.data.bones["mixamorig1:" + n].head_local
H = {s: bone(s + "UpLeg") for s in ("Left", "Right")}
K = {s: bone(s + "Leg") for s in ("Left", "Right")}
A = {s: bone(s + "Foot") for s in ("Left", "Right")}

# file = c - Ch36 (the body is a point-mirrored copy of the rig mesh; fitted on the torso, where nothing differs but the offset)
torso = [i for i, w in enumerate(weights) if w and max(w, key=lambda x: x[1])[0] in ("Spine", "Spine1", "Spine2", "Neck", "Head")]
c = sum((body_v[i] + rest[i] for i in torso), Vector()) / len(torso)
fit_spread = max(((c - rest[i]) - body_v[i]).length for i in torso)

rot = Matrix.Rotation(math.radians(-90.0), 4, "X")
def thigh(side, drop):
    return Matrix.Translation((0, 0, -drop)) @ Matrix.Translation(H[side]) @ rot @ Matrix.Translation(-H[side])
drop = sum(((thigh(s, 0) @ K[s]) + (A[s] - K[s])).z - A[s].z for s in ("Left", "Right")) / 2
bone_map = {}
for s in ("Left", "Right"):
    bone_map[s + "UpLeg"] = thigh(s, drop)
    shin = Matrix.Translation((thigh(s, drop) @ K[s]) - K[s])
    for n in ("Leg", "Foot", "ToeBase", "Toe_End"):
        bone_map[s + n] = shin
upper = Matrix.Translation((0, 0, -drop))
def blend(i):
    w = weights[i]; total = sum(x[1] for x in w)
    if total <= 1e-6:
        return upper
    m = Matrix(((0, 0, 0, 0), (0, 0, 0, 0), (0, 0, 0, 0), (0, 0, 0, 0)))
    for name, wt in w:
        m = m + bone_map.get(name, upper) * (wt / total)
    return m

# Stand up: M maps lying head (-Z) to +Y and lying face (+Y) to -Z (a proper rotation: x -> -x, y -> -z, z -> -y).
M = Matrix(((-1, 0, 0), (0, 0, -1), (0, -1, 0)))
assert abs(M.determinant() - 1) < 1e-9
def pose(i, p):  # file point -> seated file point before the final translation
    return M @ (c - (blend(i) @ (c - p)))
hip_rest = (H["Left"] + H["Right"]) / 2
hip_lying = c - hip_rest
hip_seated0 = M @ (c - (upper @ hip_rest))
seated0 = [pose(i, p) for i, p in enumerate(body_v)]
floor_y = min(p.y for p in body_v)
t = Vector((hip_lying.x - hip_seated0.x, floor_y - min(p.y for p in seated0), hip_lying.z - hip_seated0.z))
seated = [p + t for p in seated0]
T_rot = M
T_t = M @ Vector((0, 0, drop)) + t          # upper body: p' = M (p + (0, 0, drop)) + t
upper_check = max((seated[i] - (T_rot @ body_v[i] + T_t)).length for i in torso)

def linear(i):
    return M @ blend(i).to_3x3()

# ---------- New arrays ----------
pvi = child(body_g, b"PolygonVertexIndex").props[0]
nl = child(body_g, b"LayerElementNormal")
assert child(nl, b"MappingInformationType").props[0] == b"ByPolygonVertex"
assert child(nl, b"ReferenceInformationType").props[0] == b"IndexToDirect"
normals = child(nl, b"Normals").props[0]; nidx = child(nl, b"NormalsIndex").props[0]
lin_cache = {}
new_normals = array.array("d")
for k, raw in enumerate(pvi):
    cp = raw if raw >= 0 else ~raw
    L = lin_cache.get(cp) or lin_cache.setdefault(cp, linear(cp))
    j = nidx[k] * 3
    n = (L @ Vector((normals[j], normals[j + 1], normals[j + 2]))).normalized()
    new_normals.extend(n)
replace = {}
replace[id(child(body_g, b"Vertices"))] = array.array("d", [x for p in seated for x in p])
replace[id(child(nl, b"Normals"))] = new_normals
replace[id(child(nl, b"NormalsIndex"))] = array.array(encode_bin.data_types.ARRAY_INT32, range(len(pvi)))
pad_report = {}
for name, g in pad_g.items():
    pv = vec_array(child(g, b"Vertices").props[0])
    moved = [T_rot @ p + T_t for p in pv]
    replace[id(child(g, b"Vertices"))] = array.array("d", [x for p in moved for x in p])
    pn_elem = child(child(g, b"LayerElementNormal"), b"Normals")
    pn = vec_array(pn_elem.props[0])
    replace[id(pn_elem)] = array.array("d", [x for n in pn for x in (T_rot @ n).normalized()])
    bb = lambda ps: ([min(p[a] for p in ps) for a in range(3)], [max(p[a] for p in ps) for a in range(3)])
    pad_report[name.decode()] = {"bounds_before": bb(pv), "bounds_after": bb(moved)}

def substitute(e):
    out = encode_bin.FBXElem(e.id)
    single = encode_bin.FBXElem(b"_")
    for value, t in zip(e.props, e.props_type):
        if id(e) in replace and bytes((t,)) in (b"d", b"i"):
            value = replace[id(e)]
        tmp = to_elem(parse_fbx.FBXElem(b"_", [value], bytearray([t]), []))
        out.props.extend(tmp.props); out.props_type.extend(tmp.props_type)
    for ch_ in e.elems:
        out.elems.append(substitute(ch_))
    return out
top = encode_bin.FBXElem(b"")
for e in root.elems:
    top.elems.append(substitute(e))
encode_bin.write(OUT, top, version)

# ---------- Checks of the written file ----------
out_root, out_version = parse_fbx.parse(OUT)
def flat(e, path=b""):
    here = path + b"/" + e.id
    items = [(here, tuple(bytes((t,)) for t in e.props_type))]
    for value, t in zip(e.props, e.props_type):
        if bytes((t,)) in (b"d", b"i", b"f", b"l", b"b", b"c"):
            # Normals are written one per polygon vertex (index = 0..n-1): only their lengths may change; every other array
            # keeps its length, and values outside the three replaced geometries are compared below.
            items.append((here, ("array",) if e.id in (b"Normals", b"NormalsIndex") else ("array", len(value))))
        else:
            items.append((here, value))
    for ch_ in e.elems:
        items.extend(flat(ch_, here))
    return items
same_structure = flat(parse_fbx.FBXElem(b"", [], b"", root.elems)) == flat(parse_fbx.FBXElem(b"", [], b"", out_root.elems)) and out_version == version
out_objects = child(out_root, b"Objects")
out_body = [e for e in out_objects.elems if e.id == b"Geometry" and e.props[0] == body_g.props[0]][0]
written = vec_array(child(out_body, b"Vertices").props[0])
write_error = max((a - b).length for a, b in zip(written, seated))
changed_arrays = sorted({"Vertices", "Normals", "NormalsIndex"})
# Every array outside the replaced ones keeps its exact values (indices, UVs, smoothing, materials, edges).
def arrays(e, acc):
    for value, t in zip(e.props, e.props_type):
        if bytes((t,)) in (b"d", b"i", b"f", b"l", b"b", b"c") and e.id not in (b"Vertices", b"Normals", b"NormalsIndex"):
            acc.append(bytes(memoryview(value)))
    for ch_ in e.elems:
        arrays(ch_, acc)
    return acc
other_arrays_equal = arrays(parse_fbx.FBXElem(b"", [], b"", root.elems), []) == arrays(parse_fbx.FBXElem(b"", [], b"", out_root.elems), [])

# Seat from the posed body: thighs and hips underside, the front of the thighs before the knees, the back of the hips.
def top_bone(i):
    w = weights[i]
    return max(w, key=lambda x: x[1])[0] if w else ""
thigh_ids = [i for i in range(len(seated)) if top_bone(i) in ("LeftUpLeg", "RightUpLeg", "Hips")]
spine_ids = [i for i in range(len(seated)) if top_bone(i) in ("Spine", "Spine1", "Spine2")]
seat_top = min(seated[i].y for i in thigh_ids)
knee_z = sum(((M @ (c - (bone_map[s + "UpLeg"] @ K[s]))) + t).z for s in ("Left", "Right")) / 2
hips_back_z = max(seated[i].z for i in thigh_ids)
torso_back_z = max(seated[i].z for i in spine_ids)
thigh_x = [seated[i].x for i in thigh_ids]
hip_seated = hip_seated0 + t
bounds = lambda ps: ([min(p[a] for p in ps) for a in range(3)], [max(p[a] for p in ps) for a in range(3)])
report = {
    "version": VERSION,
    "frame": "patient prefab root local = FBX file coordinates (metres, Y up); Unity import flips X and the prefab's model instance has localScale.x -1",
    "source": os.path.basename(SRC), "source_sha256": sha(SRC), "output": os.path.basename(OUT), "output_sha256": sha(OUT),
    "rig": os.path.basename(RIG),
    "fbx_version": version, "plain_rewrite_byte_identical": roundtrip_identical, "same_structure_except_arrays": same_structure, "other_arrays_equal": other_arrays_equal,
    "changed_arrays": changed_arrays, "write_error_m": write_error,
    "rig_fit_offset": list(c), "rig_fit_torso_spread_m": fit_spread,
    "thigh_drop_m": drop, "upper_rigid_error_m": upper_check,
    "upper_rotation_rows": [list(r) for r in T_rot], "upper_translation": list(T_t),
    "hip_centre_lying": list(hip_lying), "hip_centre_seated": list(hip_seated),
    "floor_y": floor_y, "forward": [0.0, 0.0, -1.0], "up": [0.0, 1.0, 0.0],
    "seat_top_y": seat_top, "seat_front_z": knee_z + 0.06, "seat_back_z": hips_back_z + 0.02,
    "seat_centre_x": (min(thigh_x) + max(thigh_x)) / 2, "seat_width": max(thigh_x) - min(thigh_x) + 0.06,
    "back_plane_z": torso_back_z + 0.02, "back_top_y": seat_top + 0.48,
    "body_bounds_before": bounds(body_v), "body_bounds_after": bounds(seated),
    "pads": pad_report,
}
# The values the Unity field menu reads (TrainingFieldSetup.SeatedAedData, JsonUtility): all in the patient prefab root's local
# frame (= these file coordinates). Bounds are axis-aligned boxes of the mesh vertices in that frame, used to prove that the
# swapped meshes sit in the expected place before and after the swap.
v3 = lambda v: {"x": float(v[0]), "y": float(v[1]), "z": float(v[2])}
report["unity"] = {
    "version": VERSION,
    "upperRotationRow0": v3(T_rot[0]), "upperRotationRow1": v3(T_rot[1]), "upperRotationRow2": v3(T_rot[2]),
    "upperTranslation": v3(T_t), "thighDrop": drop, "floorY": floor_y,
    "seatTopY": report["seat_top_y"], "seatFrontZ": report["seat_front_z"], "seatBackZ": report["seat_back_z"],
    "seatCentreX": report["seat_centre_x"], "seatWidth": report["seat_width"], "backPlaneZ": report["back_plane_z"],
    "backTopY": report["back_top_y"],
    "bodyBeforeMin": v3(report["body_bounds_before"][0]), "bodyBeforeMax": v3(report["body_bounds_before"][1]),
    "bodyAfterMin": v3(report["body_bounds_after"][0]), "bodyAfterMax": v3(report["body_bounds_after"][1]),
    "pad01BeforeMin": v3(pad_report["Pad01_Attached"]["bounds_before"][0]), "pad01BeforeMax": v3(pad_report["Pad01_Attached"]["bounds_before"][1]),
    "pad01AfterMin": v3(pad_report["Pad01_Attached"]["bounds_after"][0]), "pad01AfterMax": v3(pad_report["Pad01_Attached"]["bounds_after"][1]),
    "pad02BeforeMin": v3(pad_report["Pad02_Attached"]["bounds_before"][0]), "pad02BeforeMax": v3(pad_report["Pad02_Attached"]["bounds_before"][1]),
    "pad02AfterMin": v3(pad_report["Pad02_Attached"]["bounds_after"][0]), "pad02AfterMax": v3(pad_report["Pad02_Attached"]["bounds_after"][1]),
}
json.dump(report, open(REPORT, "w"), indent=2)
print("REPORT", json.dumps({k: report[k] for k in ("plain_rewrite_byte_identical", "same_structure_except_arrays", "other_arrays_equal", "write_error_m",
                                                    "rig_fit_torso_spread_m", "thigh_drop_m", "upper_rigid_error_m", "upper_translation",
                                                    "hip_centre_lying", "hip_centre_seated", "seat_top_y", "seat_front_z", "seat_back_z",
                                                    "back_plane_z", "body_bounds_after")}))
