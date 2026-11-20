"""User-wear change (2026-11-03): remove the olive hood, shoulder cape and folded hood; finish the jacket neckline with a camo
stand collar; skin the six worn meshes to a protective-clothing copy of the mannequin skeleton (PC_Armature, same bone names
and rest pose); review deformation poses; re-export both FBX files; re-import and compare.

Input: the backup made before this change (never the live file), so the script can be re-run. Output: ProtectiveClothing.blend,
../ProtectiveClothing_Worn.fbx, ../ProtectiveClothing_Pickup.fbx, FBX_Roundtrip_Review.blend, UserWear*.json, Previews/UserWear/.
The mannequin reference (Armature, Ch36) is only read; its pose is restored before anything is saved.
Run: blender -b --factory-startup --python userwear_model.py
"""
import bpy, bmesh, math, json, time
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

T0 = time.time()
S = Path(__file__).resolve().parent
P = S.parent
BACKUP = S / 'Backups' / '20261103_userwear_before' / 'Model' / 'Source~' / 'ProtectiveClothing.blend'
PREV = S / 'Previews' / 'UserWear'
PREV.mkdir(parents=True, exist_ok=True)
PFX = 'mixamorig1:'
PARTS = ['Jacket', 'Trousers', 'Glove_L', 'Glove_R', 'Overboot_L', 'Overboot_R']
report = {'input': str(BACKUP.relative_to(S)), 'blender': bpy.app.version_string}

bpy.ops.wm.open_mainfile(filepath=str(BACKUP))
sc = bpy.context.scene
vl = bpy.context.view_layer
wc = bpy.data.collections['Worn']
pc = bpy.data.collections['Pickup']
pc.hide_viewport = False
vl.update()
mat = bpy.data.materials['PC_Shared']
for img in bpy.data.images:
    if img.name.startswith('PC_') and img.packed_file:
        img.filepath = '//../Textures/' + Path(img.filepath).name

objects_before = set(bpy.data.objects.keys())
contract = {o.name: ([list(r) for r in o.matrix_local], o.parent.name if o.parent else None)
            for o in bpy.data.objects if o.type in {'EMPTY', 'MESH'} and (o.name.startswith(('Attach_', 'Grip_', 'ProtectiveClothing_')) or o.name.endswith(('_Worn', '_Pickup')))}
mesh_names = {o.name: o.data.name for o in bpy.data.objects if o.type == 'MESH'}
tri_before = {o.name: len(o.data.polygons) for o in bpy.data.objects if o.type == 'MESH' and o.name.endswith(('_Worn', '_Pickup'))}

arm_src = bpy.data.objects['Armature']
body = bpy.data.objects['Ch36']
src_basis = {pb.name: pb.matrix_basis.copy() for pb in arm_src.pose.bones}


def body_bvh():
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    tree = BVHTree.FromPolygons([ev.matrix_world @ v.co for v in me.vertices], [tuple(f.vertices) for f in me.polygons])
    ev.to_mesh_clear()
    return tree


REF = body_bvh()

# ---------- helpers (same UV conventions as upgrade_model.py) ----------


def make(name, verts, faces):
    m = bpy.data.meshes.new(name + '_tmp')
    m.from_pydata(verts, [], faces)
    m.update()
    o = bpy.data.objects.new('_tmp_' + name, m)
    sc.collection.objects.link(o)
    return o


def activate(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.hide_set(False)
    o.select_set(True)
    vl.objects.active = o


def normals(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(o.data); bm.free()


def solidify(o, t):
    activate(o)
    m = o.modifiers.new('ThinRims', 'SOLIDIFY'); m.thickness = t; m.offset = -1
    bpy.ops.object.modifier_apply(modifier=m.name)


def finish_uv(o, style='camo'):
    if not o.data.uv_layers:
        o.data.uv_layers.new(name='UVMap')
    uv = o.data.uv_layers.active.data
    for p in o.data.polygons:
        vals = []
        for i in p.vertices:
            x, y, z = o.data.vertices[i].co
            if style == 'thread':
                vals.append((.80 + ((x * 3.7 + y * 2.1) % 1) * .15, .035 + ((z * 4.3 + y * .7) % 1) * .17))
            else:  # torso camo, centre branch of upgrade_model.finish_uv(kind='torso')
                ang = math.atan2(y + .018, x) + math.pi
                vals.append((.018 + ang / (2 * math.pi) * 1.18 / 1.5 * .72, .025 + (z - .90) / 1.5 * .72))
        if style == 'camo' and max(v[0] for v in vals) - min(v[0] for v in vals) > .20:
            period = 1.18 / 1.5 * .72
            lo = min(v[0] for v in vals)
            vals = [(u + period if u < lo + period * .35 else u, v) for u, v in vals]
        for li, co in zip(p.loop_indices, vals):
            uv[li].uv = co
        p.use_smooth = True


def clamp_to_body(o, margin):
    for v in o.data.vertices:
        loc, no, idx, dist = REF.find_nearest(v.co)
        if loc is not None and (v.co - loc).dot(no) < margin:
            v.co = loc + no * margin


def uv_region(u, v):
    if u >= .785 and .275 <= v <= .475:
        return 'olive'
    if u >= .785 and v >= .555:
        return 'rubber'
    if u >= .795 and v <= .21:
        return 'thread'
    return 'camo'


def face_islands(bm):
    bm.faces.ensure_lookup_table()
    seen = set(); out = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack = [f]; seen.add(f.index); faces = []
        while stack:
            g = stack.pop(); faces.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index); stack.append(h)
        out.append(faces)
    return out


def island_info(faces, uv):
    regs = {}
    for g in faces:
        c = sum((l[uv].uv for l in g.loops), Vector((0, 0))) / len(g.loops)
        r = uv_region(c.x, c.y); regs[r] = regs.get(r, 0) + 1
    vs = {v for g in faces for v in g.verts}
    lo = Vector([min(v.co[a] for v in vs) for a in range(3)]); hi = Vector([max(v.co[a] for v in vs) for a in range(3)])
    return max(regs, key=regs.get), lo, hi


def append_into(target, parts):
    """Append the parts' faces (with UVs) to the target mesh in place: object, mesh name, anchors and material kept."""
    bm = bmesh.new(); bm.from_mesh(target.data)
    uv = bm.loops.layers.uv.active
    inv = target.matrix_world.inverted()
    for o in parts:
        verts = [bm.verts.new(inv @ (o.matrix_world @ v.co)) for v in o.data.vertices]
        src_uv = o.data.uv_layers.active.data
        for p in o.data.polygons:
            f = bm.faces.new([verts[i] for i in p.vertices])
            f.material_index = 0
            f.smooth = True
            for l, li in zip(f.loops, p.loop_indices):
                l[uv].uv = src_uv[li].uv
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
    bm.to_mesh(target.data); bm.free()
    for o in parts:
        bpy.data.objects.remove(o, do_unlink=True)


# ---------- 1. Hood, cape and folded hood removal ----------

def remove_islands(name, decide):
    o = bpy.data.objects[name]
    bm = bmesh.new(); bm.from_mesh(o.data)
    uv = bm.loops.layers.uv.active
    removed = []; kill = []
    for faces in face_islands(bm):
        region, lo, hi = island_info(faces, uv)
        why = decide(region, lo, hi)
        if why:
            removed.append({'what': why, 'faces': len(faces), 'min': [round(x, 4) for x in lo], 'max': [round(x, 4) for x in hi]})
            kill.extend(faces)
    tri0 = len(bm.faces)
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(o.data); bm.free()
    return {'triangles_before': tri0, 'triangles_after': len(o.data.polygons), 'removed_islands': removed}


def jacket_worn_rule(region, lo, hi):
    if region == 'olive':
        if lo.z >= 1.535:
            return 'olive hood'
        if hi.x - lo.x > .5:
            return 'olive shoulder cape'
        return 'olive hood-to-cape neck join'
    if region == 'thread' and lo.z >= 1.54:
        return 'hood face binding stitch'
    if region == 'thread' and hi.x - lo.x > .5 and lo.z > 1.38:
        return 'cape hem stitch'
    return None


report['removal'] = {
    'Jacket_Worn': remove_islands('Jacket_Worn', jacket_worn_rule),
    'Jacket_Pickup': remove_islands('Jacket_Pickup', lambda r, lo, hi: 'folded olive hood' if r == 'olive' else None),
}

# ---------- 2. Neckline: camo stand collar over the cut edge, with a stitch line ----------

jo = bpy.data.objects['Jacket_Worn']
bm = bmesh.new(); bm.from_mesh(jo.data)
main = max(face_islands(bm), key=len)
top = [v.co.copy() for v in {v for f in main for v in f.verts} if v.co.z > 1.505 and math.hypot(v.co.x, v.co.y - .01) < .17]
bm.free()
cx = sum(p.x for p in top) / len(top); cy = sum(p.y for p in top) / len(top)
N = 72
radius = [0.0] * N; edge_z = [0.0] * N
for p in top:
    a = int(((math.atan2(p.y - cy, p.x - cx) + 2 * math.pi) % (2 * math.pi)) / (2 * math.pi) * N) % N
    r = math.hypot(p.x - cx, p.y - cy)
    radius[a] = max(radius[a], r); edge_z[a] = max(edge_z[a], p.z)
for i in range(N):  # fill empty bins from neighbours
    if radius[i] == 0:
        k = 1
        while radius[(i - k) % N] == 0 and radius[(i + k) % N] == 0:
            k += 1
        radius[i] = max(radius[(i - k) % N], radius[(i + k) % N]); edge_z[i] = max(edge_z[(i - k) % N], edge_z[(i + k) % N])
smooth = [sum(radius[(i + k) % N] for k in range(-3, 4)) / 7 for i in range(N)]
cut = max(edge_z)
# (height relative to the cut, radial offset): from just above the storm flap top up over the rim and rolled inside.
profile = [(-.0125, .0030), (-.0060, .0048), (.0010, .0052), (.0075, .0040), (.0105, .0005), (.0095, -.0040), (.0060, -.0060)]
cv = []; cf = []
for j, (dz, dr) in enumerate(profile):
    for i in range(N):
        a = 2 * math.pi * (i + .5) / N
        r = smooth[i] + dr
        cv.append((cx + r * math.cos(a), cy + r * math.sin(a), cut + dz + .0012 * math.sin(a * 6 + j)))
        if j:
            cf.append(((j - 1) * N + i, (j - 1) * N + (i + 1) % N, j * N + (i + 1) % N, j * N + i))
collar = make('NeckCollar', cv, cf)
normals(collar)
probe = collar.data.polygons[N // 4]  # a face of the outer bottom row; must face away from the neck axis
if (probe.center - Vector((cx, cy, probe.center.z))).dot(probe.normal) < 0:
    collar.data.flip_normals()
clamp_to_body(collar, .004)
solidify(collar, .0018)
finish_uv(collar, 'camo')
sv = []; sf = []
for i in range(N + 1):
    a = 2 * math.pi * (i % N + .5) / N
    r = smooth[i % N] + .0052
    for dz in (-.0102, -.0088):
        sv.append((cx + r * math.cos(a), cy + r * math.sin(a), cut + dz))
    if i:
        sf.append((2 * i - 2, 2 * i - 1, 2 * i + 1, 2 * i))
stitch = make('NeckCollarStitch', sv, sf)
normals(stitch); clamp_to_body(stitch, .0045); finish_uv(stitch, 'thread')
append_into(jo, [collar, stitch])
report['neckline'] = {'cut_height_m': round(cut, 4), 'centre_xy_m': [round(cx, 4), round(cy, 4)],
                      'opening_radius_m': [round(min(smooth), 4), round(max(smooth), 4)],
                      'collar': 'camo stand collar from 12.5 mm below the cut to 10.5 mm above it, rolled 6 mm inside, 1.8 mm cloth, '
                                'clamped 4 mm off the neck skin; thread stitch line 9-10 mm below the cut',
                      'jacket_triangles_after_collar': len(jo.data.polygons)}

# ---------- 3. Skeleton copy and skinning ----------

root = bpy.data.objects['ProtectiveClothing_Worn']
pa = arm_src.copy()
pa.data = arm_src.data.copy()
pa.name = 'PC_Armature'; pa.data.name = 'PC_Armature'
for c in list(pa.users_collection):
    c.objects.unlink(pa)
wc.objects.link(pa)
pa.animation_data_clear()
mw = arm_src.matrix_world.copy()
pa.parent = root
pa.matrix_parent_inverse = root.matrix_world.inverted()
pa.matrix_world = mw
for pb in pa.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
pa.data.pose_position = 'POSE'
vl.update()
body_groups = [g.name for g in body.vertex_groups]
report['skeleton'] = {'object': 'PC_Armature', 'parent': root.name, 'bones': len(pa.data.bones),
                      'deform_groups_of_reference_body': len(body_groups),
                      'rest_matrix_world_equals_mannequin_armature': max(abs(pa.matrix_world[i][j] - mw[i][j]) for i in range(4) for j in range(4)) < 1e-9}


def short(n):
    return n[len(PFX):] if n.startswith(PFX) else n


def side_of(b):
    return 'Left' if b.startswith('Left') else ('Right' if b.startswith('Right') else '')


def remap(part, b):
    s = side_of(b)
    if part == 'Jacket':
        if b == 'Head':
            return 'Neck'
        if 'Hand' in b and b not in ('LeftHand', 'RightHand'):
            return s + 'Hand'
        if any(k in b for k in ('UpLeg', 'Leg', 'Foot', 'Toe')):
            return 'Hips'
        return b
    if part == 'Trousers':
        if b in ('Hips', 'Spine'):
            return b
        if 'Toe' in b:
            return s + 'Foot'
        if any(k in b for k in ('UpLeg', 'Leg', 'Foot')):
            return b
        return 'Spine'
    own = 'Left' if part.endswith('_L') else 'Right'
    if part.startswith('Glove'):
        return b if (b.startswith(own + 'Hand') or b == own + 'ForeArm') else own + 'ForeArm'
    return b if b in (own + 'Leg', own + 'Foot', own + 'ToeBase') else own + 'Leg'


def skin(part):
    o = bpy.data.objects[part + '_Worn']
    for g in list(o.vertex_groups):
        o.vertex_groups.remove(g)
    for n in body_groups:
        o.vertex_groups.new(name=n)
    activate(o)
    m = o.modifiers.new('WeightTransfer', 'DATA_TRANSFER')
    m.object = body
    m.use_object_transform = True
    m.use_vert_data = True
    m.data_types_verts = {'VGROUP_WEIGHTS'}
    m.vert_mapping = 'POLYINTERP_NEAREST'
    m.layers_vgroup_select_src = 'ALL'
    m.layers_vgroup_select_dst = 'NAME'
    bpy.ops.object.modifier_apply(modifier=m.name)
    names = [short(g.name) for g in o.vertex_groups]
    weights = []
    for v in o.data.vertices:
        w = {}
        for ge in v.groups:
            if ge.weight > 0:
                b = remap(part, names[ge.group])
                w[b] = w.get(b, 0) + ge.weight
        weights.append(w)
    if part in ('Jacket', 'Trousers'):  # loose cloth: two Laplacian passes against hard weight steps
        adj = [[] for _ in o.data.vertices]
        for e in o.data.edges:
            a, b = e.vertices; adj[a].append(b); adj[b].append(a)
        for _ in range(2):
            new = []
            for i, w in enumerate(weights):
                if not adj[i]:
                    new.append(w); continue
                avg = {}
                for j in adj[i]:
                    for k, x in weights[j].items():
                        avg[k] = avg.get(k, 0) + x / len(adj[i])
                keys = set(w) | set(avg)
                new.append({k: .5 * w.get(k, 0) + .5 * avg.get(k, 0) for k in keys})
            weights = new
    unweighted = 0
    final = []
    for w in weights:
        top4 = sorted(((k, x) for k, x in w.items() if x > 1e-4), key=lambda t: -t[1])[:4]
        total = sum(x for _, x in top4)
        if total <= 0:
            unweighted += 1
            top4 = [(remap(part, 'Hips') if part in ('Jacket', 'Trousers') else remap(part, 'X'), 1.0)]; total = 1.0
        final.append([(k, x / total) for k, x in top4])
    for g in list(o.vertex_groups):
        o.vertex_groups.remove(g)
    used = sorted({k for f in final for k, _ in f})
    groups = {k: o.vertex_groups.new(name=PFX + k) for k in used}
    for i, f in enumerate(final):
        for k, x in f:
            groups[k].add([i], x, 'REPLACE')
    for mod in [x for x in o.modifiers if x.type == 'ARMATURE']:
        o.modifiers.remove(mod)
    am = o.modifiers.new('PC_Armature', 'ARMATURE')
    am.object = pa
    am.use_vertex_groups = True
    am.use_deform_preserve_volume = False
    missing = [k for k in used if PFX + k not in pa.data.bones]
    return {'bones': used, 'vertices': len(o.data.vertices), 'vertices_without_body_weight': unweighted, 'missing_bones': missing,
            'max_influences': 4}


report['skinning'] = {p: skin(p) for p in PARTS}
report['skinning_method'] = ('Data Transfer (nearest face interpolated) of the reference body Ch36 weights, then per part: jacket = hips, '
                             'spine, neck, shoulders, arms, hands (legs to hips, head to neck, fingers to hand); trousers = hips, spine, '
                             'legs, feet (upper body to spine, toes to foot); gloves = own forearm, hand and fingers; overboots = own '
                             'leg, foot and toe base; jacket and trousers smoothed twice; at most 4 influences, normalized.')
vl.update()

# Rest check: the skinned rest must equal the mesh data (no deformation in the bind pose).
dg = bpy.context.evaluated_depsgraph_get()
rest_err = {}
for p in PARTS:
    o = bpy.data.objects[p + '_Worn']
    ev = o.evaluated_get(dg); me = ev.to_mesh()
    rest_err[p] = max((a.co - b.co).length for a, b in zip(o.data.vertices, me.vertices))
    ev.to_mesh_clear()
report['rest_pose_skin_error_m'] = rest_err

# ---------- 4. Contract checks ----------

problems = []
for n, (m, parent) in contract.items():
    o = bpy.data.objects.get(n)
    if o is None:
        problems.append(n + ' missing'); continue
    if (o.parent.name if o.parent else None) != parent:
        problems.append(n + ' parent changed')
    d = max(abs(o.matrix_local[i][j] - m[i][j]) for i in range(4) for j in range(4))
    if d > 0:
        problems.append(f'{n} matrix {d}')
for n, mn in mesh_names.items():
    if bpy.data.objects[n].data.name != mn:
        problems.append(f'{n} mesh data renamed')
added = sorted(set(bpy.data.objects.keys()) - objects_before)
removed = sorted(objects_before - set(bpy.data.objects.keys()))
report['contract'] = {'objects_checked': len(contract), 'problems': problems, 'max_matrix_difference': 0.0 if not problems else None,
                      'objects_added': added, 'objects_removed': removed,
                      'attach_worn_world_m': {n: [round(x, 6) for x in bpy.data.objects['Attach_' + n + '_Worn'].matrix_world.translation] for n in PARTS}}
assert not problems and added == ['PC_Armature'] and not removed, report['contract']

tri_after = {o.name: len(o.data.polygons) for o in bpy.data.objects if o.type == 'MESH' and o.name.endswith(('_Worn', '_Pickup'))}
report['triangles'] = {n: [tri_before[n], tri_after[n]] for n in tri_before}

# ---------- 5. Save and export ----------

pc.hide_viewport = True; pc.hide_render = True
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(S / 'ProtectiveClothing.blend'), relative_remap=False)
pc.hide_viewport = False


def export(collection, filename, types, extra):
    bpy.ops.object.select_all(action='DESELECT')
    for o in collection.objects:
        if o.type in types:
            o.hide_set(False); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(P / filename), use_selection=True, object_types=types, global_scale=1, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', use_space_transform=True,
                             bake_space_transform=False, bake_anim=False, add_leaf_bones=False, use_custom_props=True, path_mode='RELATIVE',
                             embed_textures=False, mesh_smooth_type='FACE', **extra)


export(wc, 'ProtectiveClothing_Worn.fbx', {'MESH', 'EMPTY', 'ARMATURE'},
       dict(use_armature_deform_only=True, primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL'))
export(pc, 'ProtectiveClothing_Pickup.fbx', {'MESH', 'EMPTY'}, {})
report['export'] = {'worn': 'MESH, EMPTY, ARMATURE (deform bones only, no leaf bones, no animation)', 'pickup': 'MESH, EMPTY (no rig)',
                    'settings': 'Global Scale 1, apply_unit_scale, FBX_SCALE_UNITS, -Z forward, Y up, space transform not baked'}

# ---------- 6. Pose review (not saved) ----------


def rot_world(a, bone, axis, deg):
    pb = a.pose.bones[PFX + bone]
    vl.update()
    M = a.matrix_world @ pb.matrix
    h = M.translation.copy()
    R = Matrix.Translation(h) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-h)
    pb.matrix = a.matrix_world.inverted() @ R @ M
    vl.update()


def aim(a, bone, direction):
    pb = a.pose.bones[PFX + bone]
    vl.update()
    M = a.matrix_world @ pb.matrix
    h = M.translation.copy()
    cur = (a.matrix_world @ pb.tail) - h
    q = cur.normalized().rotation_difference(Vector(direction).normalized())
    R = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h)
    pb.matrix = a.matrix_world.inverted() @ R @ M
    vl.update()


def arms_down(a, out=.25, fwd=.05):
    aim(a, 'LeftArm', (out, -fwd, -1)); aim(a, 'RightArm', (-out, -fwd, -1))


POSES = {
    'ArmsDown': lambda a: arms_down(a),
    'ElbowBend': lambda a: (arms_down(a, .35, .2), aim(a, 'LeftForeArm', (.15, -1, .05)), aim(a, 'RightForeArm', (-.15, -1, .05))),
    'TorsoTwist': lambda a: (arms_down(a), [rot_world(a, b, 'Z', 10) for b in ('Spine', 'Spine1', 'Spine2')]),
    'LightBend': lambda a: (arms_down(a, .2, .3), rot_world(a, 'Spine', 'X', 10), rot_world(a, 'Spine1', 'X', 10),
                            rot_world(a, 'LeftUpLeg', 'X', -20), rot_world(a, 'RightUpLeg', 'X', -20),
                            rot_world(a, 'LeftLeg', 'X', 35), rot_world(a, 'RightLeg', 'X', 35)),
}


def clear_pose():
    for pb in pa.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    for pb in arm_src.pose.bones:
        pb.matrix_basis = src_basis[pb.name]
    vl.update()


def worn_world(o):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg); me = ev.to_mesh()
    pts = [ev.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    return pts


rest_pts = {p: worn_world(bpy.data.objects[p + '_Worn']) for p in PARTS}
edges = {p: [tuple(e.vertices) for e in bpy.data.objects[p + '_Worn'].data.edges] for p in PARTS}
review = bpy.data.materials.get('Reference_ReviewGrey')
if review:
    for slot in body.material_slots:
        slot.link = 'OBJECT'; slot.material = review
pc.hide_render = True
for coll in bpy.data.collections:
    if coll.name not in ('Worn', 'Reference', 'Review_Studio', 'Pickup'):
        coll.hide_render = True
cam = sc.camera or bpy.data.objects.get('ReviewCamera')
sc.camera = cam
cam.data.type = 'ORTHO'
sc.render.engine = 'CYCLES'
sc.cycles.samples = 12
sc.cycles.use_denoising = True
sc.render.resolution_x = 760; sc.render.resolution_y = 900


def shot(name, eye, target, scale):
    cam.location = Vector(eye)
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = scale
    sc.render.filepath = str(PREV / (name + '.png'))
    bpy.ops.render.render(write_still=True)


pose_report = {}
posed_reference = {}
for pose, apply in POSES.items():
    clear_pose()
    apply(pa); apply(arm_src)
    tree = body_bvh()
    stats = {}
    for p in PARTS:
        pts = worn_world(bpy.data.objects[p + '_Worn'])
        if pose == 'ElbowBend':
            posed_reference[p] = pts
        sd = []
        for v in pts[::3]:
            loc, no, idx, dist = tree.find_nearest(v)
            sd.append((v - loc).dot(no))
        ratios = []
        for a, b in edges[p]:
            r0 = (rest_pts[p][a] - rest_pts[p][b]).length
            if r0 > 1e-5:
                ratios.append((pts[a] - pts[b]).length / r0)
        ratios.sort()
        stats[p] = {'sampled_vertices': len(sd), 'inside_body_over_5mm': sum(1 for x in sd if x < -.005),
                    'inside_body_over_5mm_ratio': round(sum(1 for x in sd if x < -.005) / len(sd), 4),
                    'min_signed_distance_mm': round(min(sd) * 1000, 2),
                    'edge_stretch_p99': round(ratios[int(len(ratios) * .99)], 3), 'edge_stretch_max': round(ratios[-1], 3),
                    'edges_over_1_5x': sum(1 for r in ratios if r > 1.5)}
    pose_report[pose] = stats
    shot('Pose_' + pose + '_Front', (0, -6, 1.0), (0, 0, .95), 2.1)
    shot('Pose_' + pose + '_ThreeQuarter', (4, -4.6, 1.6), (0, 0, .95), 2.1)
clear_pose()
shot('Neck_Front', (0, -3, 1.52), (0, 0, 1.47), .42)
shot('Neck_ThreeQuarter', (2.2, -2.2, 1.95), (0, 0, 1.47), .42)
shot('Neck_Back', (0, 3, 1.62), (0, 0, 1.47), .42)
shot('Rest_Front', (0, -6, 1.0), (0, 0, .95), 2.1)
for coll in (bpy.data.collections['Reference'], wc):
    coll.hide_render = True
pc.hide_render = False
jp = bpy.data.objects['Jacket_Pickup']
others = [o for o in pc.objects if o.type == 'MESH' and o != jp]
for o in others:
    o.hide_render = True
shot('Jacket_Pickup_Top', (jp.matrix_world.translation.x + .3, jp.matrix_world.translation.y - .5, .9), jp.matrix_world.translation, .5)
report['pose_review'] = {'poses': {
    'ArmsDown': 'both upper arms aimed down and slightly out from the T pose',
    'ElbowBend': 'arms down and out, forearms forward (controller holding)',
    'TorsoTwist': 'arms down, Spine/Spine1/Spine2 10 deg each about the vertical (30 deg)',
    'LightBend': 'arms down and forward, spine 20 deg forward, thighs 20 deg forward, knees 35 deg'},
    'metrics': pose_report,
    'note': 'Deformation review only (both armatures posed alike, reference body for penetration). Not a pass of fit or comfort.'}

# ---------- 7. Re-import into a separate scene and compare ----------

expected = {}
for o in wc.objects:
    expected[o.name] = {'type': o.type, 'parent': o.parent.name if o.parent else None, 'world': o.matrix_world.copy()}
bone_heads = {b.name: (pa.matrix_world @ b.head_local) for b in pa.data.bones if b.use_deform}
weights_ref = {}
for p in PARTS:
    o = bpy.data.objects[p + '_Worn']
    weights_ref[p] = ({g.index: g.name for g in o.vertex_groups}, [{ge.group: ge.weight for ge in v.groups} for v in o.data.vertices])
pick_expected = {o.name: (o.parent.name if o.parent else None, o.matrix_world.copy(), len(o.data.polygons) if o.type == 'MESH' else None)
                 for o in pc.objects}
rest_reference = rest_pts

bpy.ops.wm.read_homefile(use_empty=True)
sc = bpy.context.scene; vl = bpy.context.view_layer
for name, filename in (('Reimport_Worn', 'ProtectiveClothing_Worn.fbx'), ('Reimport_Pickup', 'ProtectiveClothing_Pickup.fbx')):
    coll = bpy.data.collections.new(name); sc.collection.children.link(coll)
    vl.active_layer_collection = vl.layer_collection.children[name]
    bpy.ops.import_scene.fbx(filepath=str(P / filename), use_custom_props=True, ignore_leaf_bones=True, automatic_bone_orientation=False)

rt = {'worn': {}, 'pickup': {}}
wcol = bpy.data.collections['Reimport_Worn']
imp = {o.name: o for o in wcol.objects}
missing = [n for n in expected if n not in imp]
mat_diff = 0.0
parent_bad = []
for n, e in expected.items():
    o = imp.get(n)
    if not o:
        continue
    if e['type'] != 'ARMATURE':
        mat_diff = max(mat_diff, max(abs(o.matrix_world[i][j] - e['world'][i][j]) for i in range(4) for j in range(4)))
    if (o.parent.name if o.parent else None) != e['parent']:
        parent_bad.append(n)
ia = imp.get('PC_Armature')
common = [n for n in bone_heads if ia and n in ia.data.bones]
bone_err = max(((ia.matrix_world @ ia.data.bones[n].head_local) - bone_heads[n]).length for n in common) if common else None
used_bones = {PFX + b for p in PARTS for b in report['skinning'][p]['bones']}
rt['worn'].update({'objects': len(imp), 'missing': missing, 'parent_mismatch': parent_bad, 'max_matrix_difference': mat_diff,
                   'deform_bones_expected': len(bone_heads), 'bones_imported': len(ia.data.bones) if ia else 0,
                   'deform_bones_not_reimported': sorted(set(bone_heads) - set(common)),
                   'weighted_bones_not_reimported': sorted(used_bones - set(common)),
                   'max_bone_head_difference_m': bone_err})
vl.update()
worst_v = 0.0; worst_w = 0.0; worst_pose = 0.0; groups_ok = True
for p in PARTS:
    o = imp[p + '_Worn']
    mods = [m for m in o.modifiers if m.type == 'ARMATURE']
    groups_ok &= bool(mods) and mods[0].object == ia
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg); me = ev.to_mesh()
    pts = [ev.matrix_world @ v.co for v in me.vertices]; ev.to_mesh_clear()
    kd = KDTree(len(rest_reference[p]))
    for i, v in enumerate(rest_reference[p]):
        kd.insert(v, i)
    kd.balance()
    near = [kd.find(v) for v in pts]
    worst_v = max(worst_v, max(d for _, _, d in near))
    names, ref_w = weights_ref[p]
    gnames = {g.index: g.name for g in o.vertex_groups}
    for v, (_, j, _) in zip(o.data.vertices, near):
        mine = {gnames[ge.group]: ge.weight for ge in v.groups}
        theirs = {names[g]: w for g, w in ref_w[j].items()}
        for k in set(mine) | set(theirs):
            worst_w = max(worst_w, abs(mine.get(k, 0) - theirs.get(k, 0)))
    rt['worn'][p] = {'vertices': len(pts), 'armature_modifier': bool(mods), 'groups': len(o.vertex_groups)}
# Same pose on the re-imported skeleton: the deformation must match the source (bind matrices survived the round trip).
a = ia
POSES['ElbowBend'](a)
for p in PARTS:
    o = imp[p + '_Worn']
    pts = worn_world(o)
    kd = KDTree(len(rest_reference[p]))
    for i, v in enumerate(rest_reference[p]):
        kd.insert(v, i)
    kd.balance()
    # vertex order may change: pair by rest position, compare posed positions
    dg = bpy.context.evaluated_depsgraph_get()
    rest_imp = [o.matrix_world @ v.co for v in o.data.vertices]
    for v_rest, v_pose in zip(rest_imp, pts):
        _, j, _ = kd.find(v_rest)
        worst_pose = max(worst_pose, (v_pose - posed_reference[p][j]).length)
for pb in a.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
rt['worn'].update({'max_rest_vertex_difference_m': worst_v, 'max_weight_difference': worst_w,
                   'max_posed_vertex_difference_ElbowBend_m': worst_pose, 'armature_modifiers_to_PC_Armature': groups_ok})
pimp = {o.name: o for o in bpy.data.collections['Reimport_Pickup'].objects}
pd = 0.0; pbad = []
for n, (parent, m, tris) in pick_expected.items():
    o = pimp.get(n)
    if not o:
        pbad.append(n + ' missing'); continue
    pd = max(pd, max(abs(o.matrix_world[i][j] - m[i][j]) for i in range(4) for j in range(4)))
    if (o.parent.name if o.parent else None) != parent:
        pbad.append(n + ' parent')
    if tris is not None:
        bmx = bmesh.new(); bmx.from_mesh(o.data)
        if len(bmx.faces) != tris:
            pbad.append(f'{n} triangles {len(bmx.faces)} != {tris}')
        bmx.free()
rt['pickup'] = {'objects': len(pimp), 'problems': pbad, 'max_matrix_difference': pd,
                'armatures': [o.name for o in pimp.values() if o.type == 'ARMATURE']}
report['roundtrip'] = rt
bpy.ops.wm.save_as_mainfile(filepath=str(S / 'FBX_Roundtrip_Review.blend'))
report['seconds'] = round(time.time() - T0, 1)
(S / 'UserWearReport.json').write_text(json.dumps(report, indent=1, default=str), encoding='utf8')
print('USERWEAR_DONE', json.dumps({'removal': {k: [v['triangles_before'], v['triangles_after']] for k, v in report['removal'].items()},
                                   'roundtrip': rt['worn'].get('max_posed_vertex_difference_ElbowBend_m')}, default=str))
