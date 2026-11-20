# Tripo 가구 FBX 를 가볍게 만든다 — 원본 FBX 의 메시 배열만 바꿔 써서 노드 · 재질 이름 · 변환 · 단위는 그대로.
# blender -b --factory-startup --python make_lowpoly.py -- spec.json
# spec: {"src_fbx","tex","out_fbx","out_tex"(bake 일 때),"mode": "bake"|"keepuv_planar"|"keepuv_collapse",
#        "planar_deg":4,"target_tris":1200,"tex_res":4096,"smart_angle":66,"island_margin":0.003,
#        "bake_margin":16,"cage":0.006,"ray":0.012,"jpeg_quality":95,"real_height_m":1.8}
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(__file__))
import fbxio
import uvtools

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0], encoding="utf-8"))
MODE = spec.get("mode", "bake")
log = {"spec": spec}

UP = None   # 메시 좌표에서 위쪽 축 — 가져온 뒤 노드 회전으로 정한다 (Tripo 는 파일마다 Y-up 데이터 / Z-up 데이터 + 루트 -90° 가 섞여 있다)


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
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)


def apply_modifier(ob, mod):
    set_active(ob)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def hausdorff(me_a, me_b):
    """a 꼭짓점 → b 면 거리 (최대 · 평균). 로컬 좌표."""
    bm = bmesh.new()
    bm.from_mesh(me_b)
    tree = BVHTree.FromBMesh(bm)
    d = []
    for v in me_a.vertices:
        hit = tree.find_nearest(v.co)
        if hit[0] is not None:
            d.append(hit[3])
    bm.free()
    d = np.array(d)
    return float(d.max()), float(d.mean())


def sample_surface(me, n=6000, seed=1):
    """면 위 고른 점 (넓이 비례) — 겉모양 거리 재기용"""
    me.calc_loop_triangles()
    co = np.array([v.co for v in me.vertices])
    tris = np.array([t.vertices for t in me.loop_triangles])
    a, b, c = co[tris[:, 0]], co[tris[:, 1]], co[tris[:, 2]]
    area = np.linalg.norm(np.cross(b - a, c - a), axis=1) * 0.5
    rng = np.random.default_rng(seed)
    idx = rng.choice(len(tris), size=n, p=area / area.sum())
    r1, r2 = rng.random(n), rng.random(n)
    s = np.sqrt(r1)
    return a[idx] * (1 - s)[:, None] + b[idx] * (s * (1 - r2))[:, None] + c[idx] * (s * r2)[:, None]


def surface_distance(me_from, me_to, n=6000):
    pts = sample_surface(me_from, n)
    bm = bmesh.new()
    bm.from_mesh(me_to)
    tree = BVHTree.FromBMesh(bm)
    d = np.array([tree.find_nearest(Vector(p))[3] for p in pts])
    bm.free()
    return float(np.percentile(d, 99)), float(d.mean()), float(d.max())


# ---------------------------------------------------------------- 원본 가져오기
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=spec["src_fbx"])
hi = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
hi_min, hi_max = local_bounds(hi.data)
_m3 = hi.matrix_world.to_3x3()
_ups = [(_m3 @ Vector(ax)).z for ax in ((1, 0, 0), (0, 1, 0), (0, 0, 1))]
UP = int(np.argmax(np.abs(_ups)))
assert _ups[UP] > 0.99, f"위쪽 축이 음수 방향이다: {_ups}"
log["up_axis"] = UP
log["orig"] = {"name": hi.name, "unity_verts": unity_vertex_count(hi.data), "tris": tri_count(hi.data),
               "positions": len(hi.data.vertices), "min": hi_min.tolist(), "max": hi_max.tolist()}

lo = hi.copy()
lo.data = hi.data.copy()
lo.name = "LOW"
bpy.context.scene.collection.objects.link(lo)
me = lo.data

# ---------------------------------------------------------------- 줄이기
if MODE == "keepuv_planar":
    # 원본 UV 를 지킨다: UV 이음새를 넘지 않고 평평한 곳만 합친다
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(spec.get("planar_deg", 4)), use_dissolve_boundaries=False,
                             verts=bm.verts[:], edges=bm.edges[:], delimit={"UV"})
    bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(me)
    bm.free()
else:
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    if MODE == "bake" and spec.get("planar_deg", 0) > 0:
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(spec["planar_deg"]), use_dissolve_boundaries=False,
                                 verts=bm.verts[:], edges=bm.edges[:], delimit=set())
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(me)
    bm.free()
    cur = tri_count(me)
    target = spec.get("target_tris", 1200)
    pc = spec.get("protect_curved")
    if pc and target < cur:
        # 작은 곡면 부품(병 · 캔 · 얇은 벽 통 · 방아쇠)은 많이 줄이면 무너진다 → 따로 덜 줄이고(작은 건 그대로),
        # 남은 예산을 평평한 부품(상자 · 판 · 틀 · 걸쇠 · 공구함)에 몰아 준다. 둘 다 Blender 접기(편집 모드 decimate).
        bmc = bmesh.new()
        bmc.from_mesh(me)
        grp = bmc.faces.layers.int.new("grp")
        seen_c = set()
        n_curved = n_keep = 0
        for f in bmc.faces:
            if f in seen_c:
                continue
            stack = [f]
            seen_c.add(f)
            fs = []
            while stack:
                g = stack.pop()
                fs.append(g)
                for e in g.edges:
                    for h in e.link_faces:
                        if h not in seen_c:
                            seen_c.add(h)
                            stack.append(h)
            A = sum(g.calc_area() for g in fs)
            flat = sum(g.calc_area() for g in fs if max(abs(g.normal.x), abs(g.normal.y), abs(g.normal.z)) > math.cos(math.radians(10)))
            curved = A > 0 and flat / A < pc.get("flat_max", 0.5)
            # 0: 평평 (함께 줄임) · 1: 곡면 (덜 줄임) · 2: 작은 곡면 (그대로)
            gval = 0 if not curved else (2 if len(fs) < pc.get("min_faces", 64) else 1)
            for g in fs:
                g[grp] = gval
            n_curved += len(fs) if gval == 1 else 0
            n_keep += len(fs) if gval == 2 else 0
        bmc.to_mesh(me)
        bmc.free()

        def _decimate_group(gvals, ratio, weighted=False):
            set_active(lo)
            if weighted:
                # 곡면 부품 꼭짓점에 낮은 무게 → 접기 비용에 벌점(변 길이 × (2 - w1 - w2) × factor) — 나중에 접힌다
                wcur = {}
                for p in me.polygons:
                    gv = me.attributes["grp"].data[p.index].value
                    for vi in p.vertices:
                        w = 1.0 if gv == 0 else pc.get("curved_w", 0.3)
                        wcur[vi] = min(wcur.get(vi, 1.0), w)
                vg = lo.vertex_groups.new(name="w")
                for vi, w in wcur.items():
                    vg.add([vi], w, "REPLACE")
                lo.vertex_groups.active = vg
            bpy.ops.object.mode_set(mode="EDIT")
            bme = bmesh.from_edit_mesh(me)
            gl_ = bme.faces.layers.int.get("grp")
            for v in bme.verts:
                v.select = False
            for e in bme.edges:
                e.select = False
            for f in bme.faces:
                f.select = f[gl_] in gvals
            bme.select_flush_mode()
            bmesh.update_edit_mesh(me)
            if ratio < 1.0 and any(f.select for f in bme.faces):
                if weighted:
                    bpy.ops.mesh.decimate(ratio=ratio, use_vertex_group=True, vertex_group_factor=pc.get("vg_factor", 0.002))
                else:
                    bpy.ops.mesh.decimate(ratio=ratio)
            bpy.ops.object.mode_set(mode="OBJECT")
            if weighted and lo.vertex_groups.get("w"):
                lo.vertex_groups.remove(lo.vertex_groups["w"])

        n_flat = cur - n_curved - n_keep
        if pc.get("joint", False):
            # 작은 곡면 부품만 그대로 두고, 나머지(평평 + 큰 곡면)는 한 번에 함께 줄인다 — 예산 배분은 접기 비용에 맡김
            r_flat = max(0.05, min(1.0, (target - n_keep) / max(n_flat + n_curved, 1)))
            _decimate_group({0, 1}, r_flat, weighted=pc.get("curved_w") is not None)
        else:
            keep = pc.get("keep", 0.4)
            _decimate_group({1}, keep)
            budget_flat = target - int(n_curved * keep) - n_keep
            r_flat = max(0.05, min(1.0, budget_flat / max(n_flat, 1)))
            _decimate_group({0}, r_flat)
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
        bpy.ops.object.mode_set(mode="OBJECT")
        # grp 속성은 숨은 면 지우기까지 남겨 둔다 (그대로 둔 작은 곡면 부품 = 2 는 지우지 않는다 — 얇은 벽 통의 안쪽 벽 등)
        log["protect_curved"] = {"curved_faces": n_curved, "kept_small_curved": n_keep, "flat_faces": n_flat, "flat_ratio": r_flat}
    elif target < cur:
        mod = lo.modifiers.new("dec", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = target / cur
        mod.use_collapse_triangulate = True
        if spec.get("lock_boundary", 0) > 0:
            # 열린 테두리(경계 · 겹친 변) 꼭짓점은 움직이지 않게 — 움직이면 열린 껍질 안쪽(뒷면)이 틈으로 보인다.
            # Blender 접기: 비용 += 변 길이 × (2 - w1 - w2) × factor → 무게 0 인 꼭짓점이 보호된다
            bmv = bmesh.new()
            bmv.from_mesh(me)
            edge_v = set()
            for e in bmv.edges:
                if len(e.link_faces) != 2:
                    edge_v.update(v.index for v in e.verts)
            bmv.free()
            vg = lo.vertex_groups.new(name="free")
            vg.add([i for i in range(len(me.vertices)) if i not in edge_v], 1.0, "REPLACE")
            vg.add(list(edge_v), 0.0, "REPLACE")
            mod.vertex_group = "free"
            mod.vertex_group_factor = spec["lock_boundary"]
            log["locked_boundary_verts"] = len(edge_v)
        apply_modifier(lo, mod)
        if lo.vertex_groups.get("free"):
            lo.vertex_groups.remove(lo.vertex_groups["free"])
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges[:])
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3], quad_method="BEAUTY", ngon_method="BEAUTY")
    # 같은 꼭짓점 · 같은 방향으로 겹친 면(접기가 가끔 만든다) — 하나만 남긴다. 반대 방향 쌍은 양면 얇은 판이라 둔다
    seen_f, dup_f = {}, []
    bm.verts.index_update()
    for f in bm.faces:
        seq = [v.index for v in f.verts]
        key = frozenset(seq)
        if key in seen_f:
            prev = seen_f[key]
            k = prev.index(seq[0]) if seq[0] in prev else -1
            if k >= 0 and prev[k:] + prev[:k] == seq:
                dup_f.append(f)
                continue
        seen_f.setdefault(key, seq)
    if dup_f:
        bmesh.ops.delete(bm, geom=dup_f, context="FACES_ONLY")
    log["duplicate_faces_removed"] = len(dup_f)
    bm.to_mesh(me)
    bm.free()
    # 접기가 작은 부품(캔 뚜껑 등)에서 면을 뒤집는 일이 있다 — Unity 는 뒷면을 안 그려 구멍이 된다.
    # 바깥에서 보이는 쪽이 뒷면인 면(앞쪽 광선은 거의 다 막히고 뒤쪽 광선은 빠져나가는 면)만 되돌린다.
    if spec.get("fix_flips", True):
        log["flipped_fixed"] = uvtools.fix_flipped_by_visibility(lo)
    if spec.get("doubleside", False):
        log["doublesided_faces"] = uvtools.doubleside_visible_backs(
            lo, back_min=spec.get("doubleside_min", 0.04), floor_axis=UP, floor=float(hi_min[UP]) + 0.002)
    # 줄인 모양 오차는 숨은 면을 지우기 전에 잰다 (지운 면은 원래 안 보이는 곳이라 오차로 치지 않는다)
    _o2l = surface_distance(hi.data, me)
    log["shape_error_before_hidden_removal"] = {"orig_to_low_p99": _o2l[0], "orig_to_low_mean": _o2l[1], "orig_to_low_max": _o2l[2]}
    if spec.get("hide_remove", False):
        uvtools.remove_hidden_faces(lo, up_axis=UP, floor=float(hi_min[UP]) + spec.get("floor_eps", 0.002), log=log,
                                    seal_eps=spec.get("seal_eps", 0.0008), seal_floor=float(hi_min[UP]))
    if "grp" in me.attributes:
        me.attributes.remove(me.attributes["grp"])

# 경계 상자를 원본과 똑같이 — 씬의 인스턴스는 원본 경계로 크기 · 자리를 정해 두었다
lo_min, lo_max = local_bounds(me)
log["bounds_before_fix"] = {"min": lo_min.tolist(), "max": lo_max.tolist()}
span_o, span_n = hi_max - hi_min, lo_max - lo_min
co = np.empty(len(me.vertices) * 3, np.float64)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
co = (co - lo_min) / span_n * span_o + hi_min
me.vertices.foreach_set("co", co.astype(np.float32).ravel())
me.update()

if MODE == "bake":
    # 법선: 원본처럼 상자 모서리(90°)까지는 부드럽게 (Tripo 원본 평균 39°) — 그보다 많이 꺾인 곳(얇은 판 앞뒤가
    # 한 점에 모인 곳 등)만 끊는다. 다 부드럽게 하면 그런 점의 법선이 옆을 봐서 어둡게 칠해지고 굽기 광선도 빗나간다.
    for name in ("sharp_edge", "sharp_face", "custom_normal"):
        if name in me.attributes:
            me.attributes.remove(me.attributes[name])
    me.shade_smooth()
    if spec.get("sharp_deg", 100) < 180:
        me.set_sharp_from_angle(angle=math.radians(spec.get("sharp_deg", 100)))
    # 얇은 판의 위 · 앞 · 아래가 한 점에 모인 곳: 모서리 법선이 자기 면과 크게 어긋난다 → 그 점의 변을 모두 끊는다
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
            # 그 점의 면을 방향끼리 묶는다 (넓은 면부터) — 다른 무리 사이 변만 끊어 법선을 최소로 나눈다
            groups = []   # [합 법선, [면]]
            for f in sorted(v.link_faces, key=lambda f: -f.calc_area()):
                for g in groups:
                    if g[0].normalized().dot(f.normal) >= lim:
                        g[0] += f.normal * f.calc_area()
                        g[1].append(f)
                        break
                else:
                    groups.append([f.normal * f.calc_area(), [f]])
            gid = {f: i for i, g in enumerate(groups) for f in g[1]}
            for e in v.link_edges:
                lf = list(e.link_faces)
                if len(lf) == 2 and gid.get(lf[0]) != gid.get(lf[1]):
                    e.smooth = False
        bmp.to_mesh(me)
        bmp.free()
        me.update()
    log["pinched_verts"] = pinched
    # 새 UV
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name="UVMap")
    set_active(lo)
    if spec.get("unfold", False):
        uvtools.unfold_seams(lo, spec.get("smart_angle", 66), spec.get("island_margin", 0.003), log)
    else:
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(spec.get("smart_angle", 66)),
                                 island_margin=spec.get("island_margin", 0.003), area_weight=0.0,
                                 correct_aspect=True, scale_to_bounds=False)
        bpy.ops.uv.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method="FRACTION",
                                margin=spec.get("island_margin", 0.003), shape_method="CONCAVE")
        bpy.ops.object.mode_set(mode="OBJECT")
    _bm = bmesh.new()
    _bm.from_mesh(me)
    log["uv_islands"] = uvtools.face_islands(_bm, _bm.loops.layers.uv.active)[1]
    _bm.free()
else:
    for name in ("sharp_edge", "sharp_face"):
        if name in me.attributes:
            me.attributes.remove(me.attributes[name])
    if "custom_normal" in me.attributes:
        me.attributes.remove(me.attributes["custom_normal"])
    me.shade_smooth()

log["low"] = {"unity_verts": unity_vertex_count(me), "tris": tri_count(me), "positions": len(me.vertices),
              "uv_islands_hint": None}
lmin, lmax = local_bounds(me)
log["low"]["min"], log["low"]["max"] = lmin.tolist(), lmax.tolist()
h_lo_hi = hausdorff(me, hi.data)
p99_lo_hi = surface_distance(me, hi.data)
p99_hi_lo = surface_distance(hi.data, me)
height = float(span_o[UP])
real = spec.get("real_height_m")
k = (real / height * 1000.0) if real else None
log["error"] = {
    "low_to_orig_vertex_max": h_lo_hi[0],
    "low_to_orig_surface_p99": p99_lo_hi[0], "low_to_orig_surface_mean": p99_lo_hi[1],
    "orig_to_low_surface_p99": p99_hi_lo[0], "orig_to_low_surface_mean": p99_hi_lo[1], "orig_to_low_surface_max": p99_hi_lo[2],
    "mm_per_unit": k,
}

# ---------------------------------------------------------------- 텍스처 굽기
if MODE == "bake":
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == "OPTIX"
    sc.cycles.device = "GPU"
    sc.cycles.samples = spec.get("bake_samples", 16)
    sc.cycles.use_denoising = False
    sc.view_settings.view_transform = "Standard"

    # 원본 재질: 텍스처를 확실히 원본 경로에서
    mh = bpy.data.materials.new("HI")
    mh.use_nodes = True
    nt = mh.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tx = nt.nodes.new("ShaderNodeTexImage")
    tx.image = bpy.data.images.load(spec["tex"])
    tx.interpolation = "Closest" if spec.get("closest", False) else "Linear"
    nt.links.new(tx.outputs["Color"], bsdf.inputs["Base Color"])
    hi.data.materials.clear()
    hi.data.materials.append(mh)

    res = spec.get("tex_res", 4096)
    img = bpy.data.images.new("BAKED", res, res, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "sRGB"
    ml = bpy.data.materials.new("LO")
    ml.use_nodes = True
    nl = ml.node_tree
    tn = nl.nodes.new("ShaderNodeTexImage")
    tn.image = img
    nl.nodes.active = tn
    nl.links.new(tn.outputs["Color"], nl.nodes["Principled BSDF"].inputs["Base Color"])
    me.materials.clear()
    me.materials.append(ml)

    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    hi.select_set(True)
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, use_selected_to_active=True,
                        cage_extrusion=spec.get("cage", 0.006), max_ray_distance=spec.get("ray", 0.012),
                        margin=spec.get("bake_margin", 16), margin_type="EXTEND", target="IMAGE_TEXTURES",
                        use_clear=True)
    # 검사: 면 안 몇 점에서 구운 색이 원본(가장 가까운 원본 면의 색)보다 아주 어두우면 빗나간 것
    px = np.empty(res * res * 4, np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(res, res, 4)
    oimg = tx.image
    ow, oh = oimg.size
    opx = np.empty(ow * oh * 4, np.float32)
    oimg.pixels.foreach_get(opx)
    opx = opx.reshape(oh, ow, 4)
    from mathutils.geometry import barycentric_transform
    bh = bmesh.new()
    bh.from_mesh(hi.data)
    bh.faces.ensure_lookup_table()
    th = BVHTree.FromBMesh(bh)
    uvh = bh.loops.layers.uv.active
    bl = bmesh.new()
    bl.from_mesh(me)
    uvq = bl.loops.layers.uv.active

    def _px(a, u, v):
        h_, w_ = a.shape[:2]
        return a[int(np.clip(v % 1.0 * h_, 0, h_ - 1)), int(np.clip(u % 1.0 * w_, 0, w_ - 1)), :3]

    dark, diff = [], []
    for f in bl.faces:
        ls = list(f.loops)
        if f.calc_area() < 1e-9:
            continue
        for wts in ((1 / 3, 1 / 3, 1 / 3), (0.6, 0.2, 0.2), (0.2, 0.6, 0.2), (0.2, 0.2, 0.6)):
            p = sum((l.vert.co * w for l, w in zip(ls, wts)), Vector())
            uv = sum((l[uvq].uv * w for l, w in zip(ls, wts)), Vector((0, 0)))
            c_low = _px(px, uv.x, uv.y)
            loc, nor, idx, dist = th.find_nearest(p)
            gl = list(bh.faces[idx].loops)
            q = barycentric_transform(loc, gl[0].vert.co, gl[1].vert.co, gl[2].vert.co,
                                      Vector((*gl[0][uvh].uv, 0)), Vector((*gl[1][uvh].uv, 0)), Vector((*gl[2][uvh].uv, 0)))
            c_org = _px(opx, q.x, q.y)
            diff.append(float(np.abs(c_low - c_org).mean()))
            if c_low.max() < 0.05 and c_org.max() > 0.15:
                dark.append((f.index, c_org.copy()))
                break
    # 고치기: 굽기가 빗나간 면은 텍셀마다 가장 가까운 원본 표면 점의 색으로 다시 칠한다 (+1.5 텍셀 테두리)
    if dark:
        bl.faces.ensure_lookup_table()
        uvtools.repaint_faces_nearest(px, [bl.faces[fi] for fi, _ in dark], uvq, th, bh, uvh, opx)
        img.pixels.foreach_set(px.ravel())
    bh.free()
    bl.free()
    log["bake"] = {"res": res, "dark_faces_repaired": [d[0] for d in dark], "color_diff_mean": float(np.mean(diff)),
                   "color_diff_p95": float(np.percentile(diff, 95))}
    os.makedirs(os.path.dirname(spec["out_tex"]), exist_ok=True)
    img.filepath_raw = spec["out_tex"]
    img.file_format = "JPEG"
    img.save(filepath=spec["out_tex"], quality=spec.get("jpeg_quality", 95))

# ---------------------------------------------------------------- FBX 쓰기 (원본 파일에 메시 배열만 바꿔서)
me = lo.data
n = len(me.loops)
poly = []
for p in me.polygons:
    idx = [me.loops[li].vertex_index for li in range(p.loop_start, p.loop_start + p.loop_total)]
    idx[-1] = -idx[-1] - 1
    poly.extend(idx)
co = np.empty(len(me.vertices) * 3, np.float64)
me.vertices.foreach_get("co", co)
cn = np.empty(n * 3, np.float32)
me.corner_normals.foreach_get("vector", cn)
uvd = np.empty(n * 2, np.float32)
me.uv_layers.active.data.foreach_get("uv", uvd)
uvd = uvd.reshape(-1, 2).astype(np.float64)
uniq, inv = np.unique(uvd, axis=0, return_inverse=True)
root, ver = fbxio.load(spec["src_fbx"])
patch = fbxio.GeometryPatch(
    verts=co.tolist(), poly=poly, normals=cn.astype(np.float64).tolist(),
    uv=uniq.ravel().tolist(), uv_index=inv.ravel().astype(np.int64).tolist(), root=root)
os.makedirs(os.path.dirname(spec["out_fbx"]), exist_ok=True)
fbxio.save(root, spec["out_fbx"], ver, override=patch)

# 다시 읽어 확인
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=spec["out_fbx"])
chk = [o for o in bpy.context.scene.objects if o.type == "MESH"]
c = chk[0]
cmin, cmax = local_bounds(c.data)
log["reimport"] = {"objects": [o.name for o in bpy.context.scene.objects], "name_same": c.name == log["orig"]["name"],
                   "rot_deg": [round(math.degrees(v), 4) for v in c.rotation_euler], "scale": list(c.scale),
                   "materials": [m.name for m in c.data.materials],
                   "unity_verts": unity_vertex_count(c.data), "tris": tri_count(c.data),
                   "bounds_maxdiff": float(max(np.abs(cmin - hi_min).max(), np.abs(cmax - hi_max).max())),
                   "file_bytes": os.path.getsize(spec["out_fbx"])}
print("@@LOG" + json.dumps(log, ensure_ascii=False))
