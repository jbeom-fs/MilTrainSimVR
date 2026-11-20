# 가챠 기계(A + 진열 카드) 줄이기 — 11/06 가구 방식(접기 → UV 새로 펴기 → 원본 색 굽기)에 창을 다시 붙인다.
#   1) 원본 Tripo 메시를 접어 줄인다 (평평한 곳 먼저 합치고 → 목표 삼각형 수까지 접기 → 뒤집힌 면 · 안 보이는 면 정리)
#   2) 줄인 메시에 창을 낸다: 캡슐 그림 자리를 depth 만큼 안으로 → 위 · 아래 카드(재질 따로, 0~1 UV) + 앞에 유리 한 장
#   3) 몸통만 UV 를 새로 펴고 원본 텍스처 색을 굽는다. 옆면 반원판(회색 무늬)은 굽기 전에 원본 텍스처에서 옆면 흰색으로 덮는다
#   4) 창 옆벽은 구운 텍스처의 흰 점 하나를 쓴다. 재질 칸: 0 몸통 · 1 유리 · 2 위 카드 · 3 아래 카드
# blender -b --factory-startup --python make_gacha_lowpoly.py -- spec.json
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))          # Source~ (uvtools — 11/06)
import uvtools

spec = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
OUT = spec["out_dir"]
os.makedirs(OUT, exist_ok=True)
HALF_W, BANDS, DEPTH = spec["half_w"], spec["bands"], spec["depth"]
FS = -1.0                                          # 앞 = -Y (원본에서 11/09 확인 — 창 그림 면이 -Y 쪽)
F = Vector((0.0, FS, 0.0))
NAME = spec.get("name", "Gacha_Capsule_Machine")
log = {"spec": spec}


def unity_vertex_count(me):
    n = len(me.loops)
    pos = np.empty(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", pos)
    pos = pos.reshape(-1, 3)
    vi = np.empty(n, np.int32)
    me.loops.foreach_get("vertex_index", vi)
    nor = np.empty(n * 3, np.float32)
    me.corner_normals.foreach_get("vector", nor)
    nor = nor.reshape(-1, 3)
    uv = np.zeros((n, 2), np.float32)
    if me.uv_layers.active:
        a = np.empty(n * 2, np.float32)
        me.uv_layers.active.data.foreach_get("uv", a)
        uv = a.reshape(-1, 2)
    key = np.concatenate([pos[vi], np.round(nor, 5), uv], axis=1)
    return int(len(np.unique(key, axis=0)))


def tri_count(me):
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def local_bounds(me):
    co = np.empty(len(me.vertices) * 3, np.float64)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    return co.min(0), co.max(0)


def set_active(ob):
    for o in bpy.context.view_layer.objects:
        if o is not None:                            # 물체를 지운 직후엔 빈 칸이 남아 있을 수 있다
            o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)


def surface_distance(me_from, me_to, n=6000, seed=1):
    me_from.calc_loop_triangles()
    co = np.array([v.co for v in me_from.vertices])
    tris = np.array([t.vertices for t in me_from.loop_triangles])
    a, b, c = co[tris[:, 0]], co[tris[:, 1]], co[tris[:, 2]]
    area = np.linalg.norm(np.cross(b - a, c - a), axis=1) * 0.5
    rng = np.random.default_rng(seed)
    idx = rng.choice(len(tris), size=n, p=area / area.sum())
    r1, r2 = rng.random(n), rng.random(n)
    s = np.sqrt(r1)
    pts = a[idx] * (1 - s)[:, None] + b[idx] * (s * (1 - r2))[:, None] + c[idx] * (s * r2)[:, None]
    bm = bmesh.new()
    bm.from_mesh(me_to)
    tree = BVHTree.FromBMesh(bm)
    d = np.array([tree.find_nearest(Vector(p))[3] for p in pts])
    bm.free()
    return float(np.percentile(d, 99)), float(d.mean()), float(d.max())


def img_array(img):
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)                       # Blender 순서 — 아래 줄부터


def sample(a, u, v):
    h, w = a.shape[:2]
    return a[int(np.clip(v % 1.0 * h, 0, h - 1)), int(np.clip(u % 1.0 * w, 0, w - 1)), :3]


def face_uv(f, uvl):
    n = len(f.loops)
    return sum(l[uvl].uv[0] for l in f.loops) / n, sum(l[uvl].uv[1] for l in f.loops) / n


# ---------------------------------------------------------------- 원본
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=spec["src_fbx"])
hi = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
set_active(hi)
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.context.scene.objects):
    if o != hi:
        bpy.data.objects.remove(o, do_unlink=True)
hi.name = "HI"
hi_min, hi_max = local_bounds(hi.data)
log["orig"] = {"unity_verts": unity_vertex_count(hi.data), "tris": tri_count(hi.data), "min": hi_min.round(4).tolist(), "max": hi_max.round(4).tolist()}

bh = bmesh.new()
bh.from_mesh(hi.data)
bh.faces.ensure_lookup_table()
uvh = bh.loops.layers.uv.active


def in_rect(c):
    return abs(c.x) < HALF_W and any(z0 < c.z < z1 for z0, z1 in BANDS)


ys = [f.calc_center_median().y for f in bh.faces if f.normal.dot(F) > 0.8 and in_rect(f.calc_center_median())]
PLANE = float(np.median(ys))
log["window_plane_y"] = round(PLANE, 4)

# ---------------------------------------------------------------- 줄이기
lo = hi.copy()
lo.data = hi.data.copy()
lo.name = NAME
bpy.context.scene.collection.objects.link(lo)
me = lo.data
bm = bmesh.new()
bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(spec.get("planar_deg", 4)), use_dissolve_boundaries=False,
                         verts=bm.verts[:], edges=bm.edges[:], delimit=set())
bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
bm.to_mesh(me)
bm.free()
cur = tri_count(me)
target = spec.get("target_tris", 1500)
if target < cur:
    mod = lo.modifiers.new("dec", "DECIMATE")
    mod.decimate_type = "COLLAPSE"
    mod.ratio = target / cur
    mod.use_collapse_triangulate = True
    set_active(lo)
    bpy.ops.object.modifier_apply(modifier=mod.name)
bm = bmesh.new()
bm.from_mesh(me)
bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges[:])
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3], quad_method="BEAUTY", ngon_method="BEAUTY")
bm.to_mesh(me)
bm.free()
log["flipped_fixed"] = uvtools.fix_flipped_by_visibility(lo)
uvtools.remove_hidden_faces(lo, up_axis=2, floor=float(hi_min[2]) + 0.002, log=log, seal_eps=0.0008, seal_floor=float(hi_min[2]))
# 경계 상자를 원본과 똑같이
lo_min, lo_max = local_bounds(me)
co = np.empty(len(me.vertices) * 3, np.float64)
me.vertices.foreach_get("co", co)
co = (co.reshape(-1, 3) - lo_min) / (lo_max - lo_min) * (hi_max - hi_min) + hi_min
me.vertices.foreach_set("co", co.astype(np.float32).ravel())
me.update()
d = surface_distance(hi.data, me)
log["shape_error_orig_to_low"] = {"p99": round(d[0], 4), "mean": round(d[1], 5), "max": round(d[2], 4), "mm_per_unit_at_1.6m": 1600}

# ---------------------------------------------------------------- 창: 카드 자리 · 옆벽 · 유리
mats = {}
for nm in ("Gacha_Body", "Gacha_Glass", "Gacha_Card_Top", "Gacha_Card_Bottom", "WALL_TMP"):
    mats[nm] = bpy.data.materials.new(nm)
me.materials.clear()
for nm in ("Gacha_Body", "Gacha_Glass", "Gacha_Card_Top", "Gacha_Card_Bottom", "WALL_TMP"):
    me.materials.append(mats[nm])
for p in me.polygons:
    p.material_index = 0

bm = bmesh.new()
bm.from_mesh(me)
uvl = bm.loops.layers.uv.active


def near_front(f):
    return f.normal.dot(F) > 0.5 and f.calc_center_median().y * FS > PLANE * FS - 0.03


for z0, z1 in BANDS:
    for pco, pno in [((-HALF_W, 0, 0), (1, 0, 0)), ((HALF_W, 0, 0), (1, 0, 0)), ((0, 0, z0), (0, 0, 1)), ((0, 0, z1), (0, 0, 1))]:
        sel = []
        for f in bm.faces:
            if not near_front(f):
                continue
            xs = [v.co.x for v in f.verts]
            zs = [v.co.z for v in f.verts]
            if pno[0] == 1:
                ok = max(zs) > z0 - 0.002 and min(zs) < z1 + 0.002 and min(xs) <= pco[0] <= max(xs)
            else:
                ok = max(xs) > -HALF_W - 0.002 and min(xs) < HALF_W + 0.002 and min(zs) <= pco[2] <= max(zs)
            if ok:
                sel.append(f)
        if sel:
            geom = list({e for f in sel for e in f.edges}) + sel + list({v for f in sel for v in f.verts})
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=pco, plane_no=pno, dist=1e-6)
top_center = max((a + b) * 0.5 for a, b in BANDS)
# 자른 창 둘레 정리: 창 안 면에 카드 재질을 먼저 칠하고, 같은 평면 · 같은 재질끼리 합쳐 둘레의 일직선 위 꼭짓점을 없앤다
for z0, z1 in BANDS:
    top = (z0 + z1) * 0.5 > top_center - 1e-6
    for f in bm.faces:
        c = f.calc_center_median()
        if near_front(f) and f.normal.dot(F) > 0.8 and abs(c.x) < HALF_W and z0 < c.z < z1:
            f.material_index = 2 if top else 3
area_faces = [f for f in bm.faces if near_front(f) and abs(f.calc_center_median().x) < HALF_W + 0.04
              and any(z0 - 0.04 < f.calc_center_median().z < z1 + 0.04 for z0, z1 in BANDS)]
_n0 = len(bm.verts)
bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.0), use_dissolve_boundaries=False,
                         verts=list({v for f in area_faces for v in f.verts}), edges=list({e for f in area_faces for e in f.edges}),
                         delimit={"MATERIAL"})
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3], quad_method="BEAUTY", ngon_method="BEAUTY")
log["window_dissolve_removed_verts"] = _n0 - len(bm.verts)
win_log = []
for z0, z1 in BANDS:
    top = (z0 + z1) * 0.5 > top_center - 1e-6
    region = [f for f in bm.faces if f.material_index == (2 if top else 3)]
    before = set(bm.faces)
    ret = bmesh.ops.extrude_face_region(bm, geom=region)
    nverts = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
    nfaces = set(g for g in ret["geom"] if isinstance(g, bmesh.types.BMFace))
    bmesh.ops.translate(bm, verts=nverts, vec=-F * DEPTH)
    top = (z0 + z1) * 0.5 > top_center - 1e-6
    for f in nfaces:
        f.material_index = 2 if top else 3
        for l in f.loops:
            l[uvl].uv = ((l.vert.co.x + HALF_W) / (2 * HALF_W), (l.vert.co.z - z0) / (z1 - z0))
    sides = [f for f in bm.faces if f not in before and f not in nfaces]
    center = Vector((0.0, PLANE - FS * DEPTH * 0.5, (z0 + z1) * 0.5))
    for f in sides:
        f.normal_update()
        to_c = center - f.calc_center_median()
        to_c.y = 0.0
        if f.normal.dot(to_c) < 0:
            bmesh.ops.reverse_faces(bm, faces=[f])
        f.material_index = 4
    gy = PLANE - FS * 0.001
    vs = [bm.verts.new((x, gy, z)) for x, z in ((-HALF_W, z0), (HALF_W, z0), (HALF_W, z1), (-HALF_W, z1))]
    g = bm.faces.new(vs)
    g.normal_update()
    if g.normal.dot(F) < 0:
        bmesh.ops.reverse_faces(bm, faces=[g])
    g.material_index = 1
    for l in g.loops:
        l[uvl].uv = (0.0, 0.0)
    win_log.append({"band": [z0, z1], "card_faces": len(nfaces), "wall_faces": len(sides)})
bm.to_mesh(me)
bm.free()
me.update()
log["windows"] = win_log

# 몸통이 아닌 면(유리 · 카드 · 옆벽)은 따로 떼어 둔다 — 몸통만 UV 를 펴고 굽는다
extra = lo.copy()
extra.data = me.copy()
extra.name = "EXTRA"
bpy.context.scene.collection.objects.link(extra)
for ob_, keep_body in ((lo, True), (extra, False)):
    b2 = bmesh.new()
    b2.from_mesh(ob_.data)
    kill = [f for f in b2.faces if (f.material_index == 0) != keep_body]
    bmesh.ops.delete(b2, geom=kill, context="FACES")
    b2.to_mesh(ob_.data)
    b2.free()
    ob_.data.update()

# ---------------------------------------------------------------- 몸통 법선 (11/06 과 같은 규칙)
for name in ("sharp_edge", "sharp_face", "custom_normal"):
    if name in me.attributes:
        me.attributes.remove(me.attributes[name])
me.shade_smooth()
me.set_sharp_from_angle(angle=math.radians(spec.get("sharp_deg", 100)))
pinched = 0
for _ in range(3):
    nl = len(me.loops)
    cn_ = np.empty(nl * 3, np.float32)
    me.corner_normals.foreach_get("vector", cn_)
    cn_ = cn_.reshape(-1, 3)
    fn_ = np.empty(len(me.polygons) * 3, np.float32)
    me.polygons.foreach_get("normal", fn_)
    fn_ = fn_.reshape(-1, 3)
    lf_ = np.empty(nl, np.int32)
    for p in me.polygons:
        lf_[p.loop_start:p.loop_start + p.loop_total] = p.index
    lv_ = np.empty(nl, np.int32)
    me.loops.foreach_get("vertex_index", lv_)
    bad_v = set(lv_[(cn_ * fn_[lf_]).sum(1) < math.cos(math.radians(spec.get("pinch_deg", 75)))].tolist())
    if not bad_v:
        break
    pinched += len(bad_v)
    bmp = bmesh.new()
    bmp.from_mesh(me)
    bmp.verts.ensure_lookup_table()
    lim = math.cos(math.radians(spec.get("cluster_deg", 80)))
    for vi_ in bad_v:
        v = bmp.verts[vi_]
        groups = []
        for f in sorted(v.link_faces, key=lambda f: -f.calc_area()):
            for gq in groups:
                if gq[0].normalized().dot(f.normal) >= lim:
                    gq[0] += f.normal * f.calc_area()
                    gq[1].append(f)
                    break
            else:
                groups.append([f.normal * f.calc_area(), [f]])
        gid = {f: i for i, gq in enumerate(groups) for f in gq[1]}
        for e in v.link_edges:
            lf = list(e.link_faces)
            if len(lf) == 2 and gid.get(lf[0]) != gid.get(lf[1]):
                e.smooth = False
    bmp.to_mesh(me)
    bmp.free()
    me.update()
log["pinched_verts"] = pinched
# 넓은 평면이 둥글게 부풀어 보이지 않게 — 넓은 면 쪽으로 법선을 맞춘다 (Weighted Normal, 끊은 변은 유지). 꼭짓점 수는 그대로
if spec.get("weighted_normals", True):
    wn = lo.modifiers.new("wn", "WEIGHTED_NORMAL")
    wn.mode = "FACE_AREA"
    wn.weight = spec.get("wn_weight", 50)
    wn.keep_sharp = True
    set_active(lo)
    bpy.ops.object.modifier_apply(modifier=wn.name)
# 창 쪽 면: 같은 평면끼리는 꼭짓점을 나눠 쓰고, 꺾이는 곳(카드 ↔ 옆벽 · 옆벽 모서리)만 끊는다
em = extra.data
for name in ("sharp_edge", "sharp_face", "custom_normal"):
    if name in em.attributes:
        em.attributes.remove(em.attributes[name])
em.shade_smooth()
em.set_sharp_from_angle(angle=math.radians(30))

# ---------------------------------------------------------------- 몸통 UV
while me.uv_layers:
    me.uv_layers.remove(me.uv_layers[0])
me.uv_layers.new(name="UVMap")
uvtools.unfold_seams(lo, spec.get("smart_angle", 66), spec.get("island_margin", 0.003), log)
em.uv_layers.active.name = "UVMap"

# ---------------------------------------------------------------- 굽기 전에 원본 텍스처의 옆면 반원판을 옆면 흰색으로
src_img = bpy.data.images.load(spec["tex"])
opx = img_array(src_img)
OH, OW = opx.shape[:2]
side = [f for f in bh.faces if abs(f.normal.x) > 0.9 and abs(f.calc_center_median().x) > spec["ear_absx"]
        and f.calc_center_median().y > 0.05 and 0.1 < f.calc_center_median().z < 0.95]
# 옆면 흰색 = 채도 낮은 옆면 면들의 넓이 가중 중앙값 (분홍 띠 같은 곡면은 작은 면이 많아 면 수로 세면 그쪽으로 쏠린다)
_cs = np.array([sample(opx, *face_uv(f, uvh)) for f in side])
_ar = np.array([f.calc_area() for f in side])
_sat = (_cs.max(1) - _cs.min(1)) / np.maximum(_cs.max(1), 1e-6)
_keep = _sat < 0.12
_cs, _ar = _cs[_keep], _ar[_keep]
side_white = np.array([_cs[np.argsort(_cs[:, k])][np.searchsorted(np.cumsum(_ar[np.argsort(_cs[:, k])]), _ar.sum() * 0.5)][k] for k in range(3)])
ear = []
for z0, z1 in BANDS:
    for f in bh.faces:
        c = f.calc_center_median()
        if abs(f.normal.x) > 0.6 and abs(c.x) > spec["ear_absx"] and c.y < spec["ear_y"] and z0 - spec["ear_dz"] < c.z < z1 + spec["ear_dz"]:
            ear.append(f)
mask = np.zeros((OH, OW), bool)
for f in ear:
    uv = np.array([[l[uvh].uv[0] * OW - 0.5, l[uvh].uv[1] * OH - 0.5] for l in f.loops])
    for k in range(1, len(uv) - 1):
        tri = uv[[0, k, k + 1]]
        x0, y0 = np.floor(tri.min(0)).astype(int) - 1
        x1, y1 = np.ceil(tri.max(0)).astype(int) + 1
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, OW - 1), min(y1, OH - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        (ax, ay), (bx, by), (cx_, cy_) = tri
        den = (by - cy_) * (ax - cx_) + (cx_ - bx) * (ay - cy_)
        if abs(den) < 1e-12:
            continue
        w1 = ((by - cy_) * (gx - cx_) + (cx_ - bx) * (gy - cy_)) / den
        w2 = ((cy_ - ay) * (gx - cx_) + (ax - cx_) * (gy - cy_)) / den
        w3 = 1 - w1 - w2
        inside = (w1 >= -0.02) & (w2 >= -0.02) & (w3 >= -0.02)    # 테두리 텍셀까지 조금 넉넉히 (아래에서 1 텍셀 더 넓힌다)
        mask[y0:y1 + 1, x0:x1 + 1] |= inside
grow = mask.copy()                                   # 1 텍셀 넓혀 섬 가장자리 번짐까지
grow[1:, :] |= mask[:-1, :]; grow[:-1, :] |= mask[1:, :]; grow[:, 1:] |= mask[:, :-1]; grow[:, :-1] |= mask[:, 1:]
opx[grow, :3] = side_white
src_img.pixels.foreach_set(opx.ravel())
log["ears"] = {"faces": len(ear), "texels": int(grow.sum()), "side_white": np.round(side_white, 3).tolist()}

# ---------------------------------------------------------------- 굽기
sc = bpy.context.scene
sc.render.engine = "CYCLES"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for dv in prefs.devices:
        dv.use = dv.type == "OPTIX"
    sc.cycles.device = "GPU"
    log["bake_device"] = "OPTIX"
except Exception as ex:
    sc.cycles.device = "CPU"
    log["bake_device"] = "CPU (" + str(ex)[:60] + ")"
sc.cycles.samples = spec.get("bake_samples", 16)
sc.cycles.use_denoising = False
sc.view_settings.view_transform = "Standard"

mh = bpy.data.materials.new("HI")
mh.use_nodes = True
nt = mh.node_tree
tx = nt.nodes.new("ShaderNodeTexImage")
tx.image = src_img
nt.links.new(tx.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
hi.data.materials.clear()
hi.data.materials.append(mh)

res = spec.get("tex_res", 2048)
img = bpy.data.images.new("BAKED", res, res, alpha=False, float_buffer=False)
img.generated_color = (*[float(a) for a in side_white], 1.0)    # 굽지 않은 바탕은 몸통 흰색 (먼 거리 밉맵 번짐 대비)
img.colorspace_settings.name = "sRGB"
body = mats["Gacha_Body"]
body.use_nodes = True
nl_ = body.node_tree
tn = nl_.nodes.new("ShaderNodeTexImage")
tn.image = img
nl_.nodes.active = tn
nl_.links.new(tn.outputs["Color"], nl_.nodes["Principled BSDF"].inputs["Base Color"])
for nm in ("Gacha_Glass", "Gacha_Card_Top", "Gacha_Card_Bottom", "WALL_TMP"):    # 몸통 물체에는 몸통 면만 남았지만 재질 칸은 있다
    m_ = mats[nm]
    m_.use_nodes = True
    dn = m_.node_tree.nodes.new("ShaderNodeTexImage")
    dn.image = img
    m_.node_tree.nodes.active = dn
for o in bpy.context.view_layer.objects:
    o.select_set(False)
hi.select_set(True)
lo.select_set(True)
bpy.context.view_layer.objects.active = lo
bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True,
                    cage_extrusion=spec.get("cage", 0.012), max_ray_distance=spec.get("ray", 0.03),
                    margin=spec.get("bake_margin", 24), margin_type="EXTEND", target="IMAGE_TEXTURES", use_clear=False)

# 검사 · 고치기 (11/06): 구운 색이 원본보다 아주 어두운 면은 텍셀마다 가장 가까운 원본 표면 색으로 다시 칠한다
px = img_array(img)
from mathutils.geometry import barycentric_transform
th = BVHTree.FromBMesh(bh)
bl = bmesh.new()
bl.from_mesh(me)
uvq = bl.loops.layers.uv.active
dark, diff = [], []
for f in bl.faces:
    ls = list(f.loops)
    if f.calc_area() < 1e-9:
        continue
    for wts in ((1 / 3, 1 / 3, 1 / 3), (0.6, 0.2, 0.2), (0.2, 0.6, 0.2), (0.2, 0.2, 0.6)):
        p = sum((l.vert.co * w for l, w in zip(ls, wts)), Vector())
        uv = sum((l[uvq].uv * w for l, w in zip(ls, wts)), Vector((0, 0)))
        c_low = sample(px, uv.x, uv.y)
        loc, nor, idx, dist = th.find_nearest(p)
        gl = list(bh.faces[idx].loops)
        q = barycentric_transform(loc, gl[0].vert.co, gl[1].vert.co, gl[2].vert.co,
                                  Vector((*gl[0][uvh].uv, 0)), Vector((*gl[1][uvh].uv, 0)), Vector((*gl[2][uvh].uv, 0)))
        c_org = sample(opx, q.x, q.y)
        diff.append(float(np.abs(c_low - c_org).mean()))
        if c_low.max() < 0.05 and c_org.max() > 0.15:
            dark.append(f.index)
            break
if dark:
    bl.faces.ensure_lookup_table()
    uvtools.repaint_faces_nearest(px, [bl.faces[i] for i in dark], uvq, th, bh, uvh, opx)
    img.pixels.foreach_set(px.ravel())
    px = img_array(img)
bl.free()
log["bake"] = {"res": res, "dark_faces_repaired": len(dark), "color_diff_mean": round(float(np.mean(diff)), 4),
               "color_diff_p95": round(float(np.percentile(diff, 95)), 4)}
tex_path = os.path.join(OUT, NAME + "_basecolor.jpg")
img.filepath_raw = tex_path
img.file_format = "JPEG"
img.save(filepath=tex_path, quality=spec.get("jpeg_quality", 95))

# ---------------------------------------------------------------- 창 옆벽 = 구운 텍스처의 흰 점 하나, 몸통과 합치기
bw = bmesh.new()
bw.from_mesh(me)
uvb = bw.loops.layers.uv.active
best, white_uv = -9.0, (0.5, 0.5)
for f in bw.faces:
    if f.normal.dot(F) < 0.8:
        continue
    u, v = face_uv(f, uvb)
    c = sample(px, u, v)
    mx, mn = float(c.max()), float(c.min())
    score = mx - 3.0 * ((mx - mn) / mx if mx > 1e-6 else 0.0)
    if score > best:
        best, white_uv = score, (u, v)
bw.free()
be = bmesh.new()
be.from_mesh(em)
uve = be.loops.layers.uv.active
for f in be.faces:
    if f.material_index == 4:
        f.material_index = 0
        for l in f.loops:
            l[uve].uv = white_uv
be.to_mesh(em)
be.free()
em.update()
log["wall_white_uv"] = [round(white_uv[0], 4), round(white_uv[1], 4)]
set_active(lo)
extra.select_set(True)
bpy.ops.object.join()
lo = bpy.context.view_layer.objects.active
me = lo.data
# 임시 재질 칸(WALL_TMP) 지우기 — 이제 쓰는 면이 없다
idx_tmp = [i for i, m_ in enumerate(me.materials) if m_ and m_.name.startswith("WALL_TMP")]
for i in reversed(idx_tmp):
    lo.active_material_index = i
    bpy.ops.object.material_slot_remove()
log["materials"] = [m_.name for m_ in me.materials]

log["low"] = {"unity_verts": unity_vertex_count(me), "tris": tri_count(me), "positions": len(me.vertices)}
bpy.data.objects.remove(hi, do_unlink=True)
bpy.context.view_layer.update()
set_active(lo)
fbx_path = os.path.join(OUT, NAME + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_space_transform=True, apply_unit_scale=True, mesh_smooth_type="FACE",
                         add_leaf_bones=False, path_mode="STRIP", use_mesh_modifiers=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"))
json.dump(log, open(os.path.join(OUT, "log_lowpoly.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("LOWPOLY_DONE", json.dumps({k: log[k] for k in log if k != "spec"}, ensure_ascii=False))
