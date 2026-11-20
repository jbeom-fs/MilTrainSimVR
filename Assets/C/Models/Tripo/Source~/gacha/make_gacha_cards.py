# 가챠 기계 A + 진열 카드 — 원본 Tripo 메시 · UV · 텍스처는 그대로, 창 그림 면을 안쪽으로 넣고 카드 자리(위 · 아래 단 따로)로 바꾼다.
#   A: 캡슐 그림 면을 안쪽으로 밀어 넣고(옆벽은 흰 몸통 색) 앞에 유리 한 장
#   B: A + 유리 뒤에 붙은 앞줄 캡슐(낮은 다각형 공, 위 반쪽 색 · 아래 반쪽 투명 흰색)
# blender -b --factory-startup --python make_gacha_variants.py -- spec.json
import bpy, bmesh, json, math, sys, os, random
import numpy as np
from mathutils import Vector, Matrix

spec = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
SRC, TEX, OUT = spec["src_fbx"], spec["tex"], spec["out_dir"]
HALF_W = spec["half_w"]            # 창 가로 반폭 (가운데 기준 대칭 — 좌우가 뒤집혀도 그림 안쪽)
BANDS = spec["bands"]              # [[z0, z1], ...] 창 높이 구간 (Blender Z = Unity Y)
DEPTH = spec["depth"]              # 그림을 밀어 넣는 깊이
CAP_R = spec["cap_r"]
log = {"spec": spec}

bpy.ops.wm.read_factory_settings(use_empty=True)
if hasattr(bpy.ops.import_scene, "fbx"):
    bpy.ops.import_scene.fbx(filepath=SRC)
else:
    bpy.ops.wm.fbx_import(filepath=SRC)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
log["imported"] = [(o.name, o.type, tuple(round(a, 4) for a in o.rotation_euler), tuple(round(s, 4) for s in o.scale)) for o in bpy.context.scene.objects]
for o in bpy.context.scene.objects:
    o.select_set(False)
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
if len(meshes) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
for o in list(bpy.context.scene.objects):
    if o != ob:
        bpy.data.objects.remove(o, do_unlink=True)
me = ob.data
co = np.array([v.co[:] for v in me.vertices])
log["bounds_min"], log["bounds_max"] = co.min(0).round(4).tolist(), co.max(0).round(4).tolist()
log["materials"] = [m.name for m in me.materials]
log["verts_in"], log["polys_in"] = len(me.vertices), len(me.polygons)

# ---- 텍스처 색 (1024 로 줄여서 본다)
img = bpy.data.images.load(TEX)
img.scale(1024, 1024)
px = np.empty(1024 * 1024 * 4, dtype=np.float32)
img.pixels.foreach_get(px)
px = px.reshape(1024, 1024, 4)

def uv_color(u, v):
    x = min(1023, max(0, int((u % 1.0) * 1024)))
    y = min(1023, max(0, int((v % 1.0) * 1024)))
    return px[y, x, :3]

def sat_val(c):
    mx, mn = float(c.max()), float(c.min())
    return (0.0 if mx < 1e-6 else (mx - mn) / mx), mx

bm = bmesh.new()
bm.from_mesh(me)
uvl = bm.loops.layers.uv.active

def face_uv(f):
    u = sum(l[uvl].uv[0] for l in f.loops) / len(f.loops)
    v = sum(l[uvl].uv[1] for l in f.loops) / len(f.loops)
    return u, v

def in_window(c, margin=0.0):
    if abs(c.x) > HALF_W + margin:
        return False
    return any(z0 - margin < c.z < z1 + margin for z0, z1 in BANDS)

# ---- 앞면 방향: 창 자리에서 -Y 쪽과 +Y 쪽 중 텍스처 채도가 높은 쪽
best = None
for sgn in (-1.0, 1.0):
    F = Vector((0.0, sgn, 0.0))
    sats = [sat_val(uv_color(*face_uv(f)))[0] for f in bm.faces if f.normal.dot(F) > 0.8 and in_window(f.calc_center_median())]
    m = float(np.mean(sats)) if sats else 0.0
    log[f"window_sat_{'neg' if sgn < 0 else 'pos'}Y"] = (round(m, 3), len(sats))
    if best is None or m > best[0]:
        best = (m, sgn)
FS = best[1]
F = Vector((0.0, FS, 0.0))
win_faces = [f for f in bm.faces if f.normal.dot(F) > 0.8 and in_window(f.calc_center_median())]
fronts = [f.calc_center_median().y * FS for f in win_faces]
PLANE = float(np.median(fronts)) * FS          # 창 그림 면의 y
log["front_sign"] = FS
log["window_plane_y"] = round(PLANE, 4)
log["window_front_spread"] = (round(min(fronts), 4), round(max(fronts), 4))

# ---- 흰 몸통 색 자리 (옆벽 · 캡슐 아래 반쪽에 쓴다): 앞면 중 가장 하얀 면
white_uv, white_score = None, -1.0
for f in bm.faces:
    if f.normal.dot(F) < 0.8:
        continue
    s, v = sat_val(uv_color(*face_uv(f)))
    score = v - 3.0 * s
    if score > white_score:
        white_score, white_uv = score, face_uv(f)
log["white_uv"] = [round(white_uv[0], 4), round(white_uv[1], 4)]
log["white_rgb"] = [round(float(a), 3) for a in uv_color(*white_uv)]

# ---- 창 네모를 정확히 자르기 (창 높이 근처 앞면만)
def near_front(f):
    return f.normal.dot(F) > 0.5 and f.calc_center_median().y * FS > PLANE * FS - 0.03

for z0, z1 in BANDS:
    planes = [((-HALF_W, 0, 0), (1, 0, 0)), ((HALF_W, 0, 0), (1, 0, 0)), ((0, 0, z0), (0, 0, 1)), ((0, 0, z1), (0, 0, 1))]
    for pco, pno in planes:
        ff = [f for f in bm.faces if near_front(f)]
        # 그 선이 지나갈 띠 안의 면만 (몸통 전체를 가로지르지 않게)
        sel = []
        for f in ff:
            xs = [v.co.x for v in f.verts]
            zs = [v.co.z for v in f.verts]
            if pno[0] == 1:
                if max(zs) > z0 - 0.002 and min(zs) < z1 + 0.002 and min(xs) <= pco[0] <= max(xs):
                    sel.append(f)
            else:
                if max(xs) > -HALF_W - 0.002 and min(xs) < HALF_W + 0.002 and min(zs) <= pco[2] <= max(zs):
                    sel.append(f)
        if not sel:
            continue
        geom = list({e for f in sel for e in f.edges}) + sel + list({v for f in sel for v in f.verts})
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=pco, plane_no=pno, dist=1e-6)
bm.faces.ensure_lookup_table()

# ---- 재질 칸: 0 = 원본, 1 = 유리, 2 = 위 단 카드, 3 = 아래 단 카드
glass_mat = bpy.data.materials.new("Gacha_Glass")
card_top = bpy.data.materials.new("Gacha_Card_Top")
card_bottom = bpy.data.materials.new("Gacha_Card_Bottom")
me.materials.append(glass_mat)
me.materials.append(card_top)
me.materials.append(card_bottom)

# ---- 그림 면을 안쪽으로
recess_report = []
for z0, z1 in BANDS:
    region = [f for f in bm.faces if near_front(f) and f.normal.dot(F) > 0.8 and abs(f.calc_center_median().x) < HALF_W and z0 < f.calc_center_median().z < z1]
    before = set(bm.faces)
    ret = bmesh.ops.extrude_face_region(bm, geom=region)
    nverts = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    nfaces = set(g for g in ret["geom"] if isinstance(g, bmesh.types.BMFace))
    bmesh.ops.translate(bm, verts=nverts, vec=-F * DEPTH)
    top_band = (z0 + z1) * 0.5 > max((a + b) * 0.5 for a, b in BANDS) - 1e-6
    for f in nfaces:
        f.material_index = 2 if top_band else 3
        for l in f.loops:
            l[uvl].uv = ((l.vert.co.x + HALF_W) / (2 * HALF_W), (l.vert.co.z - z0) / (z1 - z0))
    sides = [f for f in bm.faces if f not in before and f not in nfaces]
    center = Vector((0.0, PLANE - FS * DEPTH * 0.5, (z0 + z1) * 0.5))
    flipped = 0
    for f in sides:
        f.normal_update()
        to_c = center - f.calc_center_median()
        to_c.y = 0.0
        if f.normal.dot(to_c) < 0:
            bmesh.ops.reverse_faces(bm, faces=[f])
            flipped += 1
        for l in f.loops:
            l[uvl].uv = white_uv
    # 유리 — 원래 그림 면보다 1 mm 안쪽
    gy = PLANE - FS * 0.001
    vs = [bm.verts.new((x, gy, z)) for x, z in ((-HALF_W, z0), (HALF_W, z0), (HALF_W, z1), (-HALF_W, z1))]
    g = bm.faces.new(vs)
    g.normal_update()
    if g.normal.dot(F) < 0:
        bmesh.ops.reverse_faces(bm, faces=[g])
    g.material_index = 1
    for l in g.loops:
        l[uvl].uv = (0.0, 0.0)
    recess_report.append({"band": [z0, z1], "region_faces": len(region), "side_faces": len(sides), "sides_flipped": flipped})
log["recess"] = recess_report

def save(path):
    bm.normal_update()
    bm.to_mesh(me)
    me.update()
    for o in bpy.context.scene.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, apply_unit_scale=True, mesh_smooth_type="FACE",
                             add_leaf_bones=False, path_mode="STRIP", use_mesh_modifiers=False)
    return {"verts": len(me.vertices), "polys": len(me.polygons), "tris": sum(len(p.vertices) - 2 for p in me.polygons)}

ob.name = me.name = "Gacha_A_Cards"
log["A_cards"] = save(os.path.join(OUT, "Gacha_A_Cards.fbx"))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "gacha_cards.blend"))
json.dump(log, open(os.path.join(OUT, "log_cards.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("GACHA_DONE", json.dumps(log, ensure_ascii=False))
