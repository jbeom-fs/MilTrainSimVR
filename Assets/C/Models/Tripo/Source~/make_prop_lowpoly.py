# 소품 줄이기 (일반, 11/09) — 11/06 가구 · 11/09 가챠 방식: 접기 → UV 새로 펴기 → 원본 색 굽기. 새 FBX 로 내보낸다(GUID 를 지킬 인스턴스가 없는 새 모델용)
#   선택 1) 법선 · 금속 · 거칠기 텍스처가 있으면 같이 굽는다 → 법선 맵 · Unity 금속/매끄러움 맵(R = 금속, A = 1 - 거칠기)
#   선택 2) glass_sides: 뚫린 창(앞 · 뒤 · 왼 · 오른)을 원본에 광선을 쏴 찾아 그 자리에 유리 판(재질 칸 1)
#   선택 3) decals: 판(plate) · 글자(text)를 원본 앞면 바로 앞에 놓고 색 굽기에 같이 넣는다 — 깨진 생성 글자 덮기
#   좌표: Blender 기준 (앞 = -Y, 위 = +Z). 앞에서 볼 때 오른쪽 = +X
# blender -b --factory-startup --python make_prop_lowpoly.py -- spec.json
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import uvtools

spec = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
OUT = spec["out_dir"]
os.makedirs(OUT, exist_ok=True)
NAME = spec["name"]
MAPS = spec["maps"]
FS = -1.0 if spec.get("front", "-Y") == "-Y" else 1.0
F = Vector((0.0, FS, 0.0))
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


def set_active(ob, extra=()):
    for o in bpy.context.view_layer.objects:
        if o is not None:
            o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    for o in extra:
        o.select_set(True)


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
    return a.reshape(h, w, 4)


def sample(a, u, v):
    h, w = a.shape[:2]
    return a[int(np.clip(v % 1.0 * h, 0, h - 1)), int(np.clip(u % 1.0 * w, 0, w - 1)), :3]


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
th = BVHTree.FromBMesh(bh)
base_img = bpy.data.images.load(MAPS["basecolor"])
opx = img_array(base_img)


def hit_color(origin, direction):
    loc, nor, idx, dist = th.ray_cast(Vector(origin), Vector(direction), 10.0)
    if loc is None:
        return None, None
    gl = list(bh.faces[idx].loops)
    q = barycentric_transform(loc, gl[0].vert.co, gl[1].vert.co, gl[2].vert.co,
                              Vector((*gl[0][uvh].uv, 0)), Vector((*gl[1][uvh].uv, 0)), Vector((*gl[2][uvh].uv, 0)))
    return sample(opx, q.x, q.y), loc


# ---------------------------------------------------------------- 뚫린 창 찾기 → 유리 자리
glass_rects = []
SIDES = {"front": (1, FS), "back": (1, -FS), "left": (0, -1.0), "right": (0, 1.0)}
step = spec.get("glass_step", 0.008)
for side in spec.get("glass_sides", []):
    ax, sg = SIDES[side]
    ua = 0 if ax == 1 else 1
    outer = float(hi_max[ax] if sg > 0 else hi_min[ax])
    us = np.arange(hi_min[ua] + step * 0.5, hi_max[ua], step)
    zs = np.arange(hi_min[2] + step * 0.5, hi_max[2], step)
    deep = np.zeros((len(zs), len(us)), bool)
    hitc = np.full((len(zs), len(us)), np.nan)
    for iz, z in enumerate(zs):
        for iu, u in enumerate(us):
            o = [0.0, 0.0, z]
            o[ua] = u
            o[ax] = outer + sg * 0.05
            d = [0.0, 0.0, 0.0]
            d[ax] = -sg
            loc, nor, idx, dist = th.ray_cast(Vector(o), Vector(d), 10.0)
            if loc is not None:
                hitc[iz, iu] = loc[ax]
    # 깊이는 그 면의 '주된 판'(가장 많이 맞은 깊이)에서 잰다 — 조작판처럼 튀어나온 부품이 있어도 몸통 앞판이 깊게 잡히지 않게
    hv = hitc[~np.isnan(hitc)]
    # 쏜 쪽 절반에서 맞은 것만 — 창이 넓으면 창 너머 뒷판이 가장 많이 맞아 그게 '주된 판'으로 잡힌다 (11/09 인형뽑기)
    mid = float(hi_min[ax] + hi_max[ax]) * 0.5
    near = hv[(hv - mid) * (-sg) < 0]
    if len(near) > 20:
        hv = near
    hist, edges_ = np.histogram(hv, bins=max(8, int((hv.max() - hv.min()) / 0.005) + 1))
    main = float(edges_[np.argmax(hist)] + (edges_[1] - edges_[0]) * 0.5)
    depth = (hitc - main) * (-sg)
    deep = np.isnan(hitc) | (np.nan_to_num(depth, nan=0.0) > spec.get("glass_deep", 0.08))
    # 가장 큰 '깊은' 덩어리 (격자 가장자리에 닿은 덩어리 = 물체 바깥이라 뺀다)
    lab = np.zeros(deep.shape, np.int32)
    best, best_n, nl = None, 0, 0
    for iz in range(deep.shape[0]):
        for iu in range(deep.shape[1]):
            if not deep[iz, iu] or lab[iz, iu]:
                continue
            nl += 1
            stack, cells, edge = [(iz, iu)], [], False
            lab[iz, iu] = nl
            while stack:
                a, b = stack.pop()
                cells.append((a, b))
                if a in (0, deep.shape[0] - 1) or b in (0, deep.shape[1] - 1):
                    edge = True
                for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    na, nb = a + da, b + db
                    if 0 <= na < deep.shape[0] and 0 <= nb < deep.shape[1] and deep[na, nb] and not lab[na, nb]:
                        lab[na, nb] = nl
                        stack.append((na, nb))
            if not edge and len(cells) > best_n:
                best, best_n = cells, len(cells)
    if not best or best_n * step * step < spec.get("glass_min_area", 0.01):
        log.setdefault("glass", []).append({"side": side, "found": False})
        continue
    ca = np.array(best)
    z0, z1 = zs[ca[:, 0].min()] - step * 0.5, zs[ca[:, 0].max()] + step * 0.5
    u0, u1 = us[ca[:, 1].min()] - step * 0.5, us[ca[:, 1].max()] + step * 0.5
    # 창틀 면 = 네모 바로 바깥에서 맞은 깊이의 중앙값
    ring = []
    iz0, iz1, iu0, iu1 = ca[:, 0].min(), ca[:, 0].max(), ca[:, 1].min(), ca[:, 1].max()
    for iz in range(max(iz0 - 2, 0), min(iz1 + 3, len(zs))):
        for iu in (iu0 - 2, iu1 + 2):
            if 0 <= iu < len(us) and not np.isnan(hitc[iz, iu]):
                ring.append(hitc[iz, iu])
    for iu in range(max(iu0 - 2, 0), min(iu1 + 3, len(us))):
        for iz in (iz0 - 2, iz1 + 2):
            if 0 <= iz < len(zs) and not np.isnan(hitc[iz, iu]):
                ring.append(hitc[iz, iu])
    plane = float(np.median(ring)) - sg * spec.get("glass_inset", 0.004)
    glass_rects.append((side, ax, sg, ua, plane, u0, u1, z0, z1))
    log.setdefault("glass", []).append({"side": side, "found": True, "u": [round(u0, 4), round(u1, 4)], "z": [round(z0, 4), round(z1, 4)], "plane": round(plane, 4)})

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
if spec.get("hide_remove", True):
    uvtools.remove_hidden_faces(lo, up_axis=2, floor=float(hi_min[2]) + 0.002, log=log, seal_eps=0.0008, seal_floor=float(hi_min[2]))
lo_min, lo_max = local_bounds(me)
co = np.empty(len(me.vertices) * 3, np.float64)
me.vertices.foreach_get("co", co)
co = (co.reshape(-1, 3) - lo_min) / (lo_max - lo_min) * (hi_max - hi_min) + hi_min
me.vertices.foreach_set("co", co.astype(np.float32).ravel())
me.update()
d_ = surface_distance(hi.data, me)
log["shape_error_orig_to_low"] = {"p99": round(d_[0], 4), "mean": round(d_[1], 5), "max": round(d_[2], 4)}

# ---------------------------------------------------------------- 법선 (11/06 규칙 + Weighted Normal)
for name in ("sharp_edge", "sharp_face", "custom_normal"):
    if name in me.attributes:
        me.attributes.remove(me.attributes[name])
me.shade_smooth()
me.set_sharp_from_angle(angle=math.radians(spec.get("sharp_deg", 100)))
pinched = 0
for _ in range(3):
    nl_ = len(me.loops)
    cn_ = np.empty(nl_ * 3, np.float32)
    me.corner_normals.foreach_get("vector", cn_)
    cn_ = cn_.reshape(-1, 3)
    fn_ = np.empty(len(me.polygons) * 3, np.float32)
    me.polygons.foreach_get("normal", fn_)
    fn_ = fn_.reshape(-1, 3)
    lf_ = np.empty(nl_, np.int32)
    for p in me.polygons:
        lf_[p.loop_start:p.loop_start + p.loop_total] = p.index
    lv_ = np.empty(nl_, np.int32)
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
wn = lo.modifiers.new("wn", "WEIGHTED_NORMAL")
wn.mode = "FACE_AREA"
wn.weight = spec.get("wn_weight", 50)
wn.keep_sharp = True
set_active(lo)
bpy.ops.object.modifier_apply(modifier=wn.name)

# ---------------------------------------------------------------- UV
while me.uv_layers:
    me.uv_layers.remove(me.uv_layers[0])
me.uv_layers.new(name="UVMap")
uvtools.unfold_seams(lo, spec.get("smart_angle", 66), spec.get("island_margin", 0.003), log)
# 앞면 섬을 크게 — 가까이서 보는 앞면(글자 · 조작판)에 텍셀을 더 준다. 섬을 키운 뒤 다시 채워 넣으면 상대 크기가 남는다
boost = spec.get("uv_front_boost", 1.0)
if boost > 1.0:
    bmu = bmesh.new()
    bmu.from_mesh(me)
    uvu = bmu.loops.layers.uv.active
    isl, n_isl = uvtools.face_islands(bmu, uvu)
    acc = np.zeros(n_isl)
    area = np.zeros(n_isl)
    for f in bmu.faces:
        a = f.calc_area()
        acc[isl[f]] += a * f.normal.dot(F)
        area[isl[f]] += a
    front_isl = {i for i in range(n_isl) if area[i] > 0 and acc[i] / area[i] > 0.5}
    cen = {}
    for f in bmu.faces:
        if isl[f] in front_isl:
            for l in f.loops:
                c = cen.setdefault(isl[f], [0.0, 0.0, 0])
                c[0] += l[uvu].uv[0]; c[1] += l[uvu].uv[1]; c[2] += 1
    for f in bmu.faces:
        i = isl[f]
        if i in front_isl:
            cx_, cy_ = cen[i][0] / cen[i][2], cen[i][1] / cen[i][2]
            for l in f.loops:
                u, v = l[uvu].uv
                l[uvu].uv = (cx_ + (u - cx_) * boost, cy_ + (v - cy_) * boost)
    bmu.to_mesh(me)
    bmu.free()
    set_active(lo)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method="FRACTION", margin=spec.get("island_margin", 0.003), shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")
    log["uv_front_boost"] = {"factor": boost, "front_islands": len(front_isl), "islands": n_isl}

# ---------------------------------------------------------------- 데칼 (원본 앞면 바로 앞 — 색 굽기에만 넣는다)
decal_objs = []


def front_point(x, z):
    o = Vector((x, float(hi_min[1] if FS < 0 else hi_max[1]) + FS * 0.05, z))
    loc, nor, idx, dist = th.ray_cast(o, -F, 10.0)
    return loc


def to_linear(c):
    # 텍스처에서 읽은 값 · spec 의 색은 sRGB — Blender 재질 색은 선형이라 바꿔 넣어야 구운 뒤 같은 색이 된다
    c = float(c)
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def color_mat(rgb, name, vertex_color=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    if vertex_color:
        ca = nt.nodes.new("ShaderNodeVertexColor")
        ca.layer_name = "Col"
        nt.links.new(ca.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*[to_linear(c) for c in rgb], 1.0)
    return m


fonts = {}
for i, dcl in enumerate(spec.get("decals", [])):
    if dcl["type"] == "plate":
        x0, x1, z0, z1 = dcl["rect"]
        cx, cz = (x0 + x1) * 0.5, (z0 + z1) * 0.5
        p = front_point(cx, cz)
        if p is None:
            continue
        y = p.y + FS * 0.0006
        mesh = bpy.data.meshes.new(f"decal_{i}")
        corners = [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]
        mesh.from_pydata([(x, y, z) for x, z in corners], [], [(0, 1, 2, 3)])
        ob = bpy.data.objects.new(f"decal_{i}", mesh)
        bpy.context.scene.collection.objects.link(ob)
        rgb = dcl.get("color")
        if rgb is None:
            # 판 바깥 둘레의 원본 색 — 귀퉁이마다 가까운 둘레 점들의 중앙값을 꼭짓점 색으로 (판 안은 그 사이를 고르게 이어 배경에 녹는다)
            ring = []
            m_ = dcl.get("sample_margin", 0.003)
            for t in np.linspace(0, 1, 16):
                for (sx, sz) in ((x0 - m_, z0 + (z1 - z0) * t), (x1 + m_, z0 + (z1 - z0) * t), (x0 + (x1 - x0) * t, z0 - m_), (x0 + (x1 - x0) * t, z1 + m_)):
                    c_, _ = hit_color((sx, float(hi_min[1] if FS < 0 else hi_max[1]) + FS * 0.05, sz), (0, -FS, 0))
                    if c_ is not None:
                        ring.append((sx, sz, c_))
            col = mesh.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
            cols = []
            for k, (cx_, cz_) in enumerate(corners):
                near = sorted(ring, key=lambda r: (r[0] - cx_) ** 2 + (r[1] - cz_) ** 2)[:10]
                c = np.median(np.array([r[2] for r in near]), axis=0)
                col.data[k].color = (*[to_linear(a) for a in c], 1.0)
                cols.append([round(float(a), 3) for a in c])
            ob.data.materials.append(color_mat(None, f"decal_{i}", vertex_color=True))
            dcl["_rgb"] = cols
        else:
            ob.data.materials.append(color_mat(rgb, f"decal_{i}"))
            dcl["_rgb"] = rgb
        decal_objs.append(ob)
    else:
        fp = dcl["font"]
        if fp not in fonts:
            fonts[fp] = bpy.data.fonts.load(fp)
        cu = bpy.data.curves.new(f"text_{i}", "FONT")
        cu.body = dcl["text"]
        cu.font = fonts[fp]
        cu.size = dcl["height"] / dcl.get("glyph_ratio", 0.82)
        cu.align_x = dcl.get("align", "CENTER")
        cu.align_y = "CENTER"
        ob = bpy.data.objects.new(f"text_{i}", cu)
        bpy.context.scene.collection.objects.link(ob)
        cx, cz = dcl["center"]
        p = front_point(cx, cz)
        if p is None:
            continue
        ob.location = (cx, p.y + FS * 0.0011, cz)
        ob.rotation_euler = (math.radians(90) if FS < 0 else math.radians(-90), 0, 0 if FS < 0 else math.pi)
        set_active(ob)
        bpy.ops.object.convert(target="MESH")
        ob = bpy.context.view_layer.objects.active
        ob.data.materials.clear()
        ob.data.materials.append(color_mat(dcl["color"], f"text_{i}"))
        decal_objs.append(ob)
        bb = [ob.matrix_world @ Vector(c_) for c_ in ob.bound_box]
        dcl["_bbox_x"] = [round(min(v.x for v in bb), 4), round(max(v.x for v in bb), 4)]
        dcl["_bbox_z"] = [round(min(v.z for v in bb), 4), round(max(v.z for v in bb), 4)]
log["decals"] = [{k: v for k, v in d.items() if k in ("type", "text", "_rgb", "_bbox_x", "_bbox_z")} for d in spec.get("decals", [])]

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
    log["bake_device"] = "CPU"
sc.cycles.samples = spec.get("bake_samples", 16)
sc.cycles.use_denoising = False
sc.view_settings.view_transform = "Standard"
res = spec.get("tex_res", 2048)


def hi_material(kind):
    m = bpy.data.materials.new("HI_" + kind)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tx = nt.nodes.new("ShaderNodeTexImage")
    if kind == "color":
        tx.image = base_img
        nt.links.new(tx.outputs["Color"], bsdf.inputs["Base Color"])
    elif kind == "normal":
        tx.image = bpy.data.images.load(MAPS["normal"])
        tx.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tx.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    else:                                        # metallic / roughness → 빛나는 색으로 굽는다
        tx.image = bpy.data.images.load(MAPS[kind])
        tx.image.colorspace_settings.name = "Non-Color"
        bsdf.inputs["Base Color"].default_value = (0, 0, 0, 1)
        nt.links.new(tx.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0
    return m


body = bpy.data.materials.new(NAME + "_Body")
body.use_nodes = True
me.materials.clear()
me.materials.append(body)
targets = {}
for kind in ["color"] + [k for k in ("normal", "metallic", "roughness") if MAPS.get(k)]:
    img = bpy.data.images.new("BAKED_" + kind, res, res, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "sRGB" if kind == "color" else "Non-Color"
    if kind == "color":
        img.generated_color = (0.85, 0.85, 0.85, 1.0)
    elif kind == "normal":
        img.generated_color = (0.5, 0.5, 1.0, 1.0)
    else:
        img.generated_color = (0.0, 0.0, 0.0, 1.0)
    node = body.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    targets[kind] = (img, node)
for kind, (img, node) in targets.items():
    body.node_tree.nodes.active = node
    hi.data.materials.clear()
    hi.data.materials.append(hi_material(kind))
    extra = [hi] + (decal_objs if kind == "color" else [])
    set_active(lo, extra)
    btype = {"color": "DIFFUSE", "normal": "NORMAL"}.get(kind, "EMIT")
    kw = dict(type=btype, use_selected_to_active=True, cage_extrusion=spec.get("cage", 0.012), max_ray_distance=spec.get("ray", 0.03),
              margin=spec.get("bake_margin", 24), margin_type="EXTEND", target="IMAGE_TEXTURES", use_clear=False)
    if btype == "DIFFUSE":
        kw["pass_filter"] = {"COLOR"}
    if btype == "NORMAL":
        kw["normal_space"] = "TANGENT"
    if btype != "DIFFUSE":
        sc.cycles.samples = 1
    bpy.ops.object.bake(**kw)
    sc.cycles.samples = spec.get("bake_samples", 16)

# 색: 빗나간 면 고치기 (11/06)
img_c = targets["color"][0]
px = img_array(img_c)
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
# 문 · 틀 사이 진짜 어두운 틈도 '빗나감'으로 잡혀 가까운 색 점으로 덧칠되는 일이 있다(11/09 인형뽑기) → repair_dark 로 끈다
if dark and spec.get("repair_dark", True):
    bl.faces.ensure_lookup_table()
    uvtools.repaint_faces_nearest(px, [bl.faces[i] for i in dark], uvq, th, bh, uvh, opx)
    img_c.pixels.foreach_set(px.ravel())
bl.free()
log["bake"] = {"res": res, "maps": list(targets), "dark_faces": len(dark), "repaired": bool(dark and spec.get("repair_dark", True)),
               "color_diff_mean": round(float(np.mean(diff)), 4)}

saved = {}
p_ = os.path.join(OUT, NAME + "_basecolor.jpg")
img_c.filepath_raw = p_
img_c.file_format = "JPEG"
img_c.save(filepath=p_, quality=spec.get("jpeg_quality", 95))
saved["basecolor"] = p_
if "normal" in targets:
    p_ = os.path.join(OUT, NAME + "_normal.png")
    targets["normal"][0].filepath_raw = p_
    targets["normal"][0].file_format = "PNG"
    targets["normal"][0].save(filepath=p_)
    saved["normal"] = p_
if "metallic" in targets or "roughness" in targets:
    met = img_array(targets["metallic"][0])[..., 0] if "metallic" in targets else np.zeros((res, res), np.float32)
    rou = img_array(targets["roughness"][0])[..., 0] if "roughness" in targets else np.full((res, res), 0.5, np.float32)
    mg = bpy.data.images.new("METALLIC_SMOOTH", res, res, alpha=True, float_buffer=False)
    mg.colorspace_settings.name = "Non-Color"
    arr = np.zeros((res, res, 4), np.float32)
    arr[..., 0] = met
    arr[..., 1] = met
    arr[..., 2] = met
    arr[..., 3] = 1.0 - rou
    mg.pixels.foreach_set(arr.ravel())
    p_ = os.path.join(OUT, NAME + "_metallicSmoothness.png")
    mg.filepath_raw = p_
    mg.file_format = "PNG"
    mg.save(filepath=p_)
    saved["metallicSmoothness"] = p_
    log["metal_stats"] = {"metal_mean": round(float(met.mean()), 3), "smooth_mean": round(float((1 - rou).mean()), 3)}
log["saved"] = saved

# ---------------------------------------------------------------- 유리 판 붙이기 (재질 칸 1)
if glass_rects:
    glass = bpy.data.materials.new(NAME + "_Glass")
    me.materials.append(glass)
    bmg = bmesh.new()
    bmg.from_mesh(me)
    uvg = bmg.loops.layers.uv.active
    for side, ax, sg, ua, plane, u0, u1, z0, z1 in glass_rects:
        def P(u, z):
            v = [0.0, 0.0, z]
            v[ua] = u
            v[ax] = plane
            return bmg.verts.new(v)
        f = bmg.faces.new([P(u0, z0), P(u1, z0), P(u1, z1), P(u0, z1)])
        f.normal_update()
        n = [0.0, 0.0, 0.0]
        n[ax] = sg
        if f.normal.dot(Vector(n)) < 0:
            bmesh.ops.reverse_faces(bmg, faces=[f])
        f.material_index = 1
        f.smooth = False
        for l in f.loops:
            l[uvg].uv = (0.0, 0.0)
    bmg.to_mesh(me)
    bmg.free()
    me.update()

log["materials"] = [m_.name for m_ in me.materials]
log["low"] = {"unity_verts": unity_vertex_count(me), "tris": tri_count(me), "positions": len(me.vertices)}
for o in decal_objs:
    bpy.data.objects.remove(o, do_unlink=True)
bpy.data.objects.remove(hi, do_unlink=True)
bpy.context.view_layer.update()
set_active(lo)
fbx_path = os.path.join(OUT, NAME + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_space_transform=True, apply_unit_scale=True, mesh_smooth_type="FACE",
                         add_leaf_bones=False, path_mode="STRIP", use_mesh_modifiers=False, use_tspace=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"))
json.dump(log, open(os.path.join(OUT, "log_" + NAME + ".json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("PROP_DONE", json.dumps({k: log[k] for k in log if k != "spec"}, ensure_ascii=False))
