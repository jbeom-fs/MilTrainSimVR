# UV 이음새 줄이기 · 안 보이는 면 지우기
import bpy, bmesh, math, random
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

EPS = 1e-6


def _uv_same(a, b):
    return abs(a.x - b.x) < EPS and abs(a.y - b.y) < EPS


def edge_uv_continuous(e, uvl):
    ls = list(e.link_loops)
    if len(ls) != 2:
        return False
    l1, l2 = ls
    if l1.vert == l2.vert:   # 뒤집힌 이웃
        return False
    a1, b1 = l1[uvl].uv, l1.link_loop_next[uvl].uv
    b2, a2 = l2[uvl].uv, l2.link_loop_next[uvl].uv
    return _uv_same(a1, a2) and _uv_same(b1, b2)


def face_islands(bm, uvl):
    isl = {}
    n = 0
    for f in bm.faces:
        if f in isl:
            continue
        stack = [f]
        isl[f] = n
        while stack:
            g = stack.pop()
            for e in g.edges:
                if not edge_uv_continuous(e, uvl):
                    continue
                for h in e.link_faces:
                    if h not in isl:
                        isl[h] = n
                        stack.append(h)
        n += 1
    return isl, n


def _chains(edges):
    """변 묶음을 이어진 사슬로 나눈다 (꼭짓점 공유)"""
    edges = list(edges)
    by_v = {}
    for e in edges:
        for v in e.verts:
            by_v.setdefault(v, []).append(e)
    seen = set()
    out = []
    for e in edges:
        if e in seen:
            continue
        stack = [e]
        seen.add(e)
        chain = []
        while stack:
            x = stack.pop()
            chain.append(x)
            for v in x.verts:
                for y in by_v[v]:
                    if y not in seen:
                        seen.add(y)
                        stack.append(y)
        out.append(chain)
    return out


def unfold_seams(ob, smart_angle_deg=66.0, margin=0.003, log=None):
    """Smart UV 로 평평한 조각(chart)을 만든 뒤, 맞닿은 조각을 가장 긴 공유 변 사슬 하나로 이어 붙여(나무 모양)
    상자는 전개도처럼 한 섬이 되게 한다. 펼친 뒤 겹치거나 많이 늘어난 섬은 원래 조각으로 되돌린다."""
    me = ob.data
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(smart_angle_deg), island_margin=margin,
                             area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    uvl = bm.loops.layers.uv.active
    chart, n_chart = face_islands(bm, uvl)
    chart_of_face = [chart[f] for f in bm.faces]

    pair_edges = {}
    for e in bm.edges:
        lf = list(e.link_faces)
        if len(lf) != 2:
            continue
        a, b = chart[lf[0]], chart[lf[1]]
        if a == b:
            continue
        if lf[0].normal.dot(lf[1].normal) < -0.95:   # 거의 맞붙은 앞뒷면(얇은 판 가장자리 아닌 접힘) — 잇지 않는다
            continue
        key = (a, b) if a < b else (b, a)
        pair_edges.setdefault(key, []).append(e)
    cand = []
    for key, es in pair_edges.items():
        best = max(_chains(es), key=lambda ch: sum(x.calc_length() for x in ch))
        cand.append((len(best), sum(x.calc_length() for x in best), key, best))
    cand.sort(key=lambda c: (-c[0], -c[1]))

    parent = list(range(n_chart))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    unseal = set()   # 변 index
    for _, _, (a, b), chain in cand:
        ra, rb = find(a), find(b)
        if ra == rb:
            continue
        parent[ra] = rb
        unseal.update(x.index for x in chain)

    chart_seams = set()
    for e in bm.edges:
        lf = list(e.link_faces)
        if len(lf) != 2 or chart[lf[0]] != chart[lf[1]]:
            chart_seams.add(e.index)
    seams = chart_seams - unseal   # 변 index — bmesh 를 새로 만들어도 위상이 같으니 그대로 쓴다
    bm.free()
    reverted = 0
    for it in range(4):
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.edges.ensure_lookup_table()
        for e in bm.edges:
            e.seam = e.index in seams
        bm.to_mesh(me)
        bm.free()
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.unwrap(method="ANGLE_BASED", fill_holes=True, correct_aspect=True, margin_method="SCALED", margin=margin)
        bpy.ops.object.mode_set(mode="OBJECT")
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        uvl = bm.loops.layers.uv.active
        isl, n_isl = face_islands(bm, uvl)
        bad = _bad_islands(bm, uvl, isl, n_isl)
        if not bad:
            bm.free()
            break
        # 나쁜 섬: 그 섬 안의 조각 경계를 모두 다시 이음새로
        for f in bm.faces:
            if isl[f] in bad:
                for e in f.edges:
                    lf = list(e.link_faces)
                    if len(lf) == 2 and chart_of_face[lf[0].index] != chart_of_face[lf[1].index]:
                        seams.add(e.index)
        reverted += len(bad)
        bm.free()
    # 섬 모아 담기 (돌리기 허용)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method="FRACTION", margin=margin, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")
    if log is not None:
        log["unfold"] = {"charts": n_chart, "tree_links": len(cand), "unsealed_edges": len(unseal), "reverted_islands": reverted}


def _bad_islands(bm, uvl, isl, n_isl, stretch_limit=2.2, raster=768):
    """겹침(UV 삼각형이 서로 덮음) · 뒤집힘 · 넓이 늘어남이 큰 섬"""
    bad = set()
    faces_by = [[] for _ in range(n_isl)]
    for f in bm.faces:
        faces_by[isl[f]].append(f)
    for i, fs in enumerate(faces_by):
        if len(fs) < 2:
            continue
        a3, a2, sgn = [], [], []
        for f in fs:
            uv = [l[uvl].uv for l in f.loops]
            s = 0.0
            for k in range(1, len(uv) - 1):
                p, q, r = uv[0], uv[k], uv[k + 1]
                s += (q.x - p.x) * (r.y - p.y) - (r.x - p.x) * (q.y - p.y)
            a2.append(abs(s) * 0.5)
            sgn.append(1 if s > 0 else -1)
            a3.append(f.calc_area())
        a3, a2 = np.array(a3), np.array(a2)
        if a2.sum() <= 0 or a3.sum() <= 0:
            bad.add(i)
            continue
        # 뒤집힌 면이 섞였으면 겹친 것
        if len(set(sgn)) > 1:
            bad.add(i)
            continue
        ratio = (a2 / a2.sum()) / np.maximum(a3 / a3.sum(), 1e-12)
        w = a3 / a3.sum()
        if (w[(ratio > stretch_limit) | (ratio < 1 / stretch_limit)]).sum() > 0.08:
            bad.add(i)
            continue
        # 같은 방향이어도 겹칠 수 있다 → 래스터로 확인
        if _raster_overlap(fs, uvl, raster) > 0.01:
            bad.add(i)
    return bad


def _raster_overlap(faces, uvl, res):
    uvs = [[(l[uvl].uv.x, l[uvl].uv.y) for l in f.loops] for f in faces]
    allp = np.array([p for f in uvs for p in f])
    mn, mx = allp.min(0), allp.max(0)
    span = np.maximum(mx - mn, 1e-9)
    s = (res - 1) / span.max()
    grid = np.zeros((res, res), np.int16)
    total = 0
    for f in uvs:
        P = (np.array(f) - mn) * s
        for k in range(1, len(P) - 1):
            tri = np.array([P[0], P[k], P[k + 1]])
            x0, y0 = np.floor(tri.min(0)).astype(int)
            x1, y1 = np.ceil(tri.max(0)).astype(int)
            x0, y0 = max(x0, 0), max(y0, 0)
            x1, y1 = min(x1, res - 1), min(y1, res - 1)
            if x1 < x0 or y1 < y0:
                continue
            xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = tri
            d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(d) < 1e-12:
                continue
            l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
            l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
            l3 = 1 - l1 - l2
            m = (l1 > 1e-3) & (l2 > 1e-3) & (l3 > 1e-3)
            grid[y0:y1 + 1, x0:x1 + 1] += m.astype(np.int16)
            total += int(m.sum())
    if total == 0:
        return 0.0
    return float(np.clip(grid - 1, 0, None).sum()) / total


def repaint_faces_nearest(px, faces, uvl, tree_hi, bm_hi, uvl_hi, orig_px, dilate=1.5):
    """굽기가 빗나간 면을 텍셀마다 다시 칠한다: 텍셀 → 저폴리 면 위 3D 점 → 가장 가까운 원본 표면 점 → 원본 UV → 원본 텍스처 색.
    px: (res,res,4) 구운 이미지 (Blender 순서, 아래 줄부터) · orig_px: 원본 텍스처 배열."""
    from mathutils.geometry import barycentric_transform
    res = px.shape[0]
    oh, ow = orig_px.shape[:2]
    painted = 0
    for f in faces:
        ls = list(f.loops)
        if len(ls) != 3:
            continue
        P = [Vector((l[uvl].uv.x * res, l[uvl].uv.y * res, 0.0)) for l in ls]
        X = [l.vert.co.copy() for l in ls]
        x0 = int(math.floor(min(p.x for p in P) - dilate))
        x1 = int(math.ceil(max(p.x for p in P) + dilate))
        y0 = int(math.floor(min(p.y for p in P) - dilate))
        y1 = int(math.ceil(max(p.y for p in P) + dilate))
        for y in range(max(y0, 0), min(y1, res - 1) + 1):
            for x in range(max(x0, 0), min(x1, res - 1) + 1):
                q = Vector((x + 0.5, y + 0.5, 0.0))
                # 삼각형 밖 텍셀은 가장 가까운 삼각형 위 점으로 (dilate 픽셀 안만)
                from mathutils.geometry import closest_point_on_tri
                c = closest_point_on_tri(q, P[0], P[1], P[2])
                if (c - q).length > dilate:
                    continue
                p3 = barycentric_transform(c, P[0], P[1], P[2], X[0], X[1], X[2])
                hit = tree_hi.find_nearest(p3)
                if hit[0] is None:
                    continue
                gl = list(bm_hi.faces[hit[2]].loops)
                u = barycentric_transform(hit[0], gl[0].vert.co, gl[1].vert.co, gl[2].vert.co,
                                          Vector((*gl[0][uvl_hi].uv, 0)), Vector((*gl[1][uvl_hi].uv, 0)), Vector((*gl[2][uvl_hi].uv, 0)))
                ox = int(np.clip(u.x % 1.0 * ow, 0, ow - 1))
                oy = int(np.clip(u.y % 1.0 * oh, 0, oh - 1))
                px[y, x, :3] = orig_px[oy, ox, :3]
                painted += 1
    return painted


def _sphere_dirs(n):
    golden = math.pi * (3 - math.sqrt(5))
    out = []
    for i in range(n):
        z = 1 - (i + 0.5) / n * 2
        r = math.sqrt(max(0.0, 1 - z * z))
        t = golden * i
        out.append(Vector((r * math.cos(t), r * math.sin(t), z)))
    return out


def fix_flipped_by_visibility(ob, n_dirs=160, front_max=0.02, back_min=0.08, rounds=2):
    """면 앞(법선 쪽) 반구와 뒤 반구에서 광선이 빠져나가는 비율을 비교 — 앞은 막히고 뒤는 트인 면은 뒤집힌 것.
    두 쪽 다 막히거나 다 트인 면(틈 사이 · 얇은 판 한 겹)은 건드리지 않는다."""
    me = ob.data
    dirs = _sphere_dirs(n_dirs)
    total = 0
    for _ in range(rounds):
        bm = bmesh.new()
        bm.from_mesh(me)
        tree = BVHTree.FromBMesh(bm)
        co = [v.co for v in bm.verts]
        mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
        mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
        far = (mx - mn).length * 3
        flip = []
        for f in bm.faces:
            if f.calc_area() < 1e-10:
                continue
            n = f.normal
            c = f.calc_center_median()
            pts = [c] + [c.lerp(v.co, 0.6) for v in f.verts]
            esc = {1: 0, -1: 0}
            cnt = {1: 0, -1: 0}
            for p in pts:
                for d in dirs:
                    s = 1 if d.dot(n) > 0.05 else (-1 if d.dot(n) < -0.05 else 0)
                    if s == 0:
                        continue
                    cnt[s] += 1
                    if tree.ray_cast(p + n * (s * 1e-4), d, far)[0] is None:
                        esc[s] += 1
            fr = esc[1] / max(cnt[1], 1)
            bk = esc[-1] / max(cnt[-1], 1)
            if fr <= front_max and bk >= back_min:
                flip.append(f)
        if not flip:
            bm.free()
            break
        bmesh.ops.reverse_faces(bm, faces=flip)
        bm.to_mesh(me)
        bm.free()
        me.update()
        total += len(flip)
    return total


def doubleside_visible_backs(ob, n_dirs=160, back_min=0.04, floor_axis=None, floor=None):
    """뒷면이 바깥에서 보이는 면(접기가 얇은 부품을 한 겹 지느러미로 만든 곳 · 작은 구멍 너머)에
    뒤집은 사본을 붙여 양면으로 만든다 — Unity 는 뒷면을 안 그려서 그 쪽에서 보면 사라지거나 구멍이 된다.
    원본은 닫힌 껍질이라 이런 면이 거의 없다 (자홍 검사 11 px)."""
    me = ob.data
    dirs = _sphere_dirs(n_dirs)
    bm = bmesh.new()
    bm.from_mesh(me)
    tree = BVHTree.FromBMesh(bm)
    co = [v.co for v in bm.verts]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    far = (mx - mn).length * 3
    pick = []
    for f in bm.faces:
        if f.calc_area() < 1e-10:
            continue
        n = f.normal
        c = f.calc_center_median()
        pts = [c] + [c.lerp(v.co, 0.6) for v in f.verts]
        esc = cnt = 0
        for p in pts:
            o = p - n * 1e-4
            for d in dirs:
                if d.dot(n) > -0.05:
                    continue
                if floor_axis is not None and d[floor_axis] < 0 and o[floor_axis] <= floor:
                    continue
                cnt += 1
                if tree.ray_cast(o, d, far)[0] is None:
                    esc += 1
        if cnt and esc / cnt >= back_min:
            pick.append(f)
    if pick:
        dup = bmesh.ops.duplicate(bm, geom=pick)
        new_faces = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)]
        bmesh.ops.reverse_faces(bm, faces=new_faces)
        bm.to_mesh(me)
        me.update()
    bm.free()
    return len(pick)


def remove_hidden_faces(ob, up_axis=1, floor=0.0, n_dirs=400, seed=3, log=None, seal_eps=0.0, seal_floor=0.0):
    """어느 쪽에서도 안 보이는 면을 지운다 — 바닥(floor 평면 아래)도 가림막으로 친다.
    면마다 가운데 · 모서리 쪽 몇 점에서 반구 방향으로 광선을 쏴서 하나라도 빠져나가면 보이는 면."""
    me = ob.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    rng = random.Random(seed)
    # 고른 구 방향 (피보나치)
    dirs = []
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(n_dirs):
        z = 1 - (i + 0.5) / n_dirs * 2
        r = math.sqrt(max(0.0, 1 - z * z))
        t = golden * i
        dirs.append(Vector((r * math.cos(t), r * math.sin(t), z)))
    co = [v.co for v in bm.verts]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    far = (mx - mn).length * 3
    hidden = []
    keep_layer = bm.faces.layers.int.get("grp")
    for f in bm.faces:
        n = f.normal
        if n.length < 0.5:
            continue
        if keep_layer is not None and f[keep_layer] == 2:
            continue
        c = f.calc_center_median()
        vs = [v.co for v in f.verts]
        pts = [c] + [c.lerp(v, 0.7) for v in vs] + [c.lerp((vs[i] + vs[(i + 1) % len(vs)]) * 0.5, 0.8) for i in range(len(vs))]
        vis = False
        for p in pts:
            o = p + n * 1e-4
            for d in dirs:
                if d.dot(n) < 0.03:
                    continue
                # 바닥 높이(floor)보다 낮은 점에서 아래로 가는 광선 끝엔 눈이 있을 수 없다
                if d[up_axis] < 0 and o[up_axis] <= floor:
                    continue
                if tree.ray_cast(o, d, far)[0] is not None:
                    continue
                vis = True
                break
            if vis:
                break
        if not vis:
            hidden.append(f)
    nf = len(bm.faces)
    n_cand = len(hidden)
    if seal_eps > 0 and hidden:
        hidden = _sealed_only(bm, hidden, up_axis, seal_floor, seal_eps)
    bmesh.ops.delete(bm, geom=hidden, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    if log is not None:
        log["hidden_removed"] = {"faces_before": nf, "candidates": n_cand, "removed": len(hidden)}


def _sealed_only(bm, hidden, up_axis, floor, eps):
    """숨은 면 덩어리를 지우면 테두리가 새로 열린다 — 그 테두리가 다른 부품(또는 바닥)에 eps 안으로 붙어 있을 때만 지운다.
    (상자 밑면을 지웠는데 테두리가 판에서 들려 있으면 그 틈으로 상자 안쪽 뒷면이 보인다)"""
    comp = {}
    cid = 0
    for f in bm.faces:
        if f in comp:
            continue
        stack = [f]
        comp[f] = cid
        while stack:
            g = stack.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h not in comp:
                        comp[h] = cid
                        stack.append(h)
        cid += 1
    H = set(hidden)
    remaining = [f for f in bm.faces if f not in H]
    vmap = {}
    verts, polys = [], []
    for f in remaining:
        poly = []
        for v in f.verts:
            if v not in vmap:
                vmap[v] = len(verts)
                verts.append(v.co.copy())
            poly.append(vmap[v])
        polys.append(poly)
    tree = BVHTree.FromPolygons(verts, polys) if polys else None
    rem_comp = [comp[f] for f in remaining]
    keep_removed = []
    seen = set()
    for f in hidden:
        if f in seen:
            continue
        region = []
        stack = [f]
        seen.add(f)
        while stack:
            g = stack.pop()
            region.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h in H and h not in seen:
                        seen.add(h)
                        stack.append(h)
        rs = set(region)
        c = comp[f]
        ok = True
        for g in region:
            for e in g.edges:
                lf = e.link_faces
                if not any(h not in rs for h in lf):
                    continue   # 덩어리 안쪽 변 · 원래 열린 변 — 새로 열리지 않는다
                a, b = e.verts[0].co, e.verts[1].co
                for t in (0.05, 0.27, 0.5, 0.73, 0.95):
                    p = a.lerp(b, t)
                    if p[up_axis] <= floor + eps:
                        continue
                    sealed = False
                    if tree is not None:
                        for hit in tree.find_nearest_range(p, eps):
                            if rem_comp[hit[2]] != c:
                                sealed = True
                                break
                    if not sealed:
                        ok = False
                        break
                if not ok:
                    break
            if not ok:
                break
        if ok:
            keep_removed.extend(region)
    return keep_removed
