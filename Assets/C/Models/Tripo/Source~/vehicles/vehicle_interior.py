# 차량 실내 만들기 (11/10) — vehicle_finish.py 가 부른다
#   Tripo 차는 겉껍데기뿐이라 유리를 투명하게 하면 안이 비어 보인다. 좌석 · 대시보드 · 운전대 · 콘솔 · 팔걸이 · 바닥 · 앞뒤 격벽 ·
#   뒷선반을 진짜 치수(m)로 만들고(모서리를 둥글린 덩어리), 마지막에 모델 칸으로 옮긴다. 색은 작은 색판 텍스처(8칸)의 칸을 UV 로 고른다.
#   차체 밖으로 나간 꼭짓점은 가장 가까운 몸통 면 안쪽으로 밀어 넣고, 남은 것이 있는지 세어 기록한다(0 이어야 한다).
#   좌표(m): X = 앞(+), Y = 왼쪽(+) — 운전석은 왼쪽, Z = 바닥에서 위
import bpy, bmesh, math
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

PALETTE = [(58, 60, 64), (80, 82, 86), (27, 27, 29), (44, 45, 48), (21, 21, 23), (9, 11, 13), (150, 152, 156), (110, 22, 22)]
FABRIC, FABRIC_LIGHT, PLASTIC, PLASTIC_GREY, CARPET, SCREEN, METAL, ACCENT = range(8)


def write_palette(path):
    w, h = 32, 4
    px = np.ones((h, w, 4), np.float32)
    for i, c in enumerate(PALETTE):
        px[:, i * 4:(i + 1) * 4, :3] = np.array(c, np.float32) / 255.0
    img = bpy.data.images.new("palette", w, h, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "sRGB"
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save(filepath=path)
    bpy.data.images.remove(img)


def build(name, body_me, p, log):
    """p: SX SY SZ FS mn_z gx0 gx1 belt z_ws z_rg iw fz half_w z_top + cfg(dict)"""
    c = p.get("cfg", {})
    SX, SY, SZ, FS = p["SX"], p["SY"], p["SZ"], p["FS"]
    gx0, gx1, belt, z_ws, z_rg, iw, fz, z_top = p["gx0"], p["gx1"], p["belt"], p["z_ws"], p["z_rg"], p["iw"], p["fz"], p["z_top"]
    to_model = Matrix.Translation((0.0, 0.0, p["mn_z"])) @ Matrix.Diagonal((FS / SX, FS / SY, 1.0 / SZ, 1.0))
    main = bmesh.new()
    uvl = main.loops.layers.uv.new("UVMap")
    conform = []                                           # (꼭짓점 시작, 끝) 구간 — 차체 안쪽으로 맞출 부품
    stats = {"parts": 0}

    def commit(tmp, color, smooth, fit):
        """임시 메시(진짜 m)를 색 · 음영을 정해 본 메시에 붙인다"""
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        tl = tmp.loops.layers.uv.verify()
        u = ((color + 0.5) / len(PALETTE), 0.5)
        for f in tmp.faces:
            f.smooth = smooth
            for l in f.loops:
                l[tl].uv = u
        bmesh.ops.transform(tmp, matrix=to_model, verts=tmp.verts[:])
        m = bpy.data.meshes.new("tmp_part")
        tmp.to_mesh(m)
        tmp.free()
        n0 = len(main.verts)
        main.from_mesh(m)
        bpy.data.meshes.remove(m)
        if fit:
            conform.append((n0, len(main.verts)))
        stats["parts"] += 1

    def rbox(center, size, r, color, tilt=0.0, segs=2, fit=False):
        """모서리를 둥글린 상자. tilt(도): Y 축으로 돌린다 — 양수면 위쪽이 뒤로 눕는다"""
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        bmesh.ops.scale(tmp, vec=Vector(size), verts=tmp.verts[:])
        if r > 0:
            bmesh.ops.bevel(tmp, geom=tmp.edges[:], offset=min(r, min(size) * 0.45), offset_type="OFFSET", segments=segs,
                            profile=0.5, affect="EDGES", clamp_overlap=True)
        mat = Matrix.Translation(Vector(center)) @ Matrix.Rotation(math.radians(-tilt), 4, "Y")
        bmesh.ops.transform(tmp, matrix=mat, verts=tmp.verts[:])
        commit(tmp, color, r > 0, fit)

    def prism(profile, y0, y1, color, fit=False):
        """옆에서 본 단면(X, Z 점들)을 폭 방향으로 밀어낸 덩어리"""
        tmp = bmesh.new()
        a = [tmp.verts.new((x, y0, z)) for x, z in profile]
        b = [tmp.verts.new((x, y1, z)) for x, z in profile]
        n = len(profile)
        for i in range(n):
            tmp.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
        tmp.faces.new(a)
        tmp.faces.new(b[::-1])
        commit(tmp, color, False, fit)

    def sheet(p0, pu, pv, color, step=0.16):
        """한 겹 판 — 잘게 나눠 차체 안쪽에 맞춘다. 앞면은 (pu - p0) x (pv - p0) 쪽"""
        tmp = bmesh.new()
        p0, pu, pv = Vector(p0), Vector(pu), Vector(pv)
        nu = max(1, int(math.ceil((pu - p0).length / step)))
        nv = max(1, int(math.ceil((pv - p0).length / step)))
        g = [[tmp.verts.new(p0 + (pu - p0) * (i / nu) + (pv - p0) * (j / nv)) for j in range(nv + 1)] for i in range(nu + 1)]
        for i in range(nu):
            for j in range(nv):
                tmp.faces.new((g[i][j], g[i + 1][j], g[i + 1][j + 1], g[i][j + 1]))
        tl = tmp.loops.layers.uv.verify()
        u = ((color + 0.5) / len(PALETTE), 0.5)
        for f in tmp.faces:
            f.smooth = False
            for l in f.loops:
                l[tl].uv = u
        bmesh.ops.transform(tmp, matrix=to_model, verts=tmp.verts[:])
        m = bpy.data.meshes.new("tmp_sheet")
        tmp.to_mesh(m)
        tmp.free()
        n0 = len(main.verts)
        main.from_mesh(m)
        bpy.data.meshes.remove(m)
        conform.append((n0, len(main.verts)))
        stats["parts"] += 1

    def wheel(center, radius, tube, tilt):
        """운전대 — 고리 · 가운데 뭉치 · 살 셋 · 기둥. 고리는 YZ 평면, tilt 만큼 위쪽이 앞으로 눕는다"""
        tmp = bmesh.new()
        seg, sides = 20, 6
        rings = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring = []
            for j in range(sides):
                b = 2 * math.pi * j / sides
                r = radius + tube * math.cos(b)
                ring.append(tmp.verts.new((tube * math.sin(b), r * math.cos(a), r * math.sin(a))))
            rings.append(ring)
        for i in range(seg):
            for j in range(sides):
                tmp.faces.new((rings[i][j], rings[(i + 1) % seg][j], rings[(i + 1) % seg][(j + 1) % sides], rings[i][(j + 1) % sides]))
        hub = bmesh.ops.create_cone(tmp, cap_ends=True, segments=14, radius1=0.072, radius2=0.060, depth=0.055)["verts"]
        bmesh.ops.transform(tmp, matrix=Matrix.Translation((0.02, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, "Y"), verts=hub)
        for ang in (0.0, 180.0, 270.0):                   # 살: 왼쪽 · 오른쪽 · 아래
            sp = bmesh.ops.create_cube(tmp, size=1.0)["verts"]
            bmesh.ops.scale(tmp, vec=Vector((0.018, radius - 0.05, 0.036)), verts=sp)
            bmesh.ops.transform(tmp, matrix=Matrix.Rotation(math.radians(ang), 4, "X") @ Matrix.Translation((0.012, (radius + 0.05) / 2, 0)), verts=sp)
        col = bmesh.ops.create_cube(tmp, size=1.0)["verts"]  # 기둥 — 앞(대시보드 쪽)으로
        bmesh.ops.scale(tmp, vec=Vector((0.30, 0.075, 0.075)), verts=col)
        bmesh.ops.transform(tmp, matrix=Matrix.Translation((0.17, 0, 0)), verts=col)
        mat = Matrix.Translation(Vector(center)) @ Matrix.Rotation(math.radians(tilt), 4, "Y")
        bmesh.ops.transform(tmp, matrix=mat, verts=tmp.verts[:])
        commit(tmp, PLASTIC, True, False)

    drv = c.get("driver_y", 0.36)
    dash_x = gx1 - c.get("dash_from_glass", 0.02)         # 앞유리 앞 끝 바로 뒤
    seat_x = gx1 - c.get("seat_from_glass", 1.14)
    rear_x = seat_x - c.get("rear_gap", 0.84)
    wall = iw + 0.12                                       # 판은 차체보다 넉넉히 — 밖으로 나간 곳은 안쪽으로 밀려 들어온다
    xf, xb = dash_x + 0.05, gx0 + 0.02
    # 객실을 통으로 막는다: 바닥 · 앞 격벽 · 뒤 격벽 · 뒷선반 (옆은 안쪽 껍데기가 막는다)
    sheet((xb, -wall, fz), (xf, -wall, fz), (xb, wall, fz), CARPET)
    sheet((xf, -wall, fz - 0.08), (xf, -wall, z_ws + 0.02), (xf, wall, fz - 0.08), PLASTIC)
    sheet((xb, -wall, fz - 0.08), (xb, wall, fz - 0.08), (xb, -wall, z_rg + 0.02), PLASTIC)
    shelf_x1 = rear_x - 0.36
    if shelf_x1 - xb > 0.08:
        zs = z_rg - 0.015
        sheet((xb, -wall, zs), (shelf_x1, -wall, zs), (xb, wall, zs), PLASTIC_GREY)
    # 대시보드 — 옆에서 본 단면을 폭 방향으로
    top = z_ws - 0.02
    prof = [(dash_x + 0.04, top), (dash_x - 0.27, top - 0.025), (dash_x - 0.37, top - 0.085), (dash_x - 0.41, belt - 0.13),
            (dash_x - 0.37, fz + 0.36), (dash_x - 0.22, fz + 0.29), (dash_x + 0.04, fz + 0.29)]
    prism(prof, -(iw + 0.10), iw + 0.10, PLASTIC, fit=True)
    rbox((dash_x - 0.30, drv, top - 0.005), (0.22, 0.37, 0.10), 0.035, PLASTIC, fit=True)                   # 계기판 덮개
    rbox((dash_x - 0.385, drv, top - 0.075), (0.02, 0.27, 0.085), 0.008, SCREEN, tilt=-12.0, segs=1)        # 계기판
    rbox((dash_x - 0.415, 0.0, belt - 0.02), (0.03, 0.25, 0.15), 0.01, SCREEN, tilt=-8.0, segs=1)           # 가운데 화면
    rbox((dash_x - 0.40, 0.0, fz + 0.50), (0.07, 0.24, 0.20), 0.02, PLASTIC_GREY, segs=1)                   # 가운데 조작판
    # 가운데 콘솔 · 팔걸이 · 변속 손잡이
    cx0, cx1 = seat_x - 0.40, dash_x - 0.40
    rbox(((cx0 + cx1) / 2, 0.0, fz + 0.125), (cx1 - cx0, 0.19, 0.25), 0.03, PLASTIC_GREY)
    rbox((seat_x - 0.20, 0.0, fz + 0.295), (0.36, 0.17, 0.09), 0.03, FABRIC)
    rbox((seat_x + 0.33, 0.0, fz + 0.265), (0.10, 0.09, 0.04), 0.012, PLASTIC, segs=1)
    rbox((seat_x + 0.33, 0.0, fz + 0.33), (0.045, 0.045, 0.10), 0.018, METAL, segs=1)
    # 운전대
    wheel((dash_x - 0.66, drv, belt + 0.01), 0.185, 0.016, 22.0)
    # 앞좌석 둘
    for sy in (drv, -drv):
        rbox((seat_x + 0.01, sy, fz + 0.055), (0.44, 0.40, 0.11), 0.0, PLASTIC)                             # 받침
        rbox((seat_x + 0.02, sy, fz + 0.185), (0.50, 0.50, 0.14), 0.05, FABRIC, tilt=-5.0)                  # 방석
        rbox((seat_x - 0.30, sy, fz + 0.52), (0.13, 0.48, 0.62), 0.05, FABRIC, tilt=17.0)                   # 등받이
        rbox((seat_x - 0.415, sy, fz + 0.925), (0.10, 0.25, 0.18), 0.04, FABRIC_LIGHT, tilt=10.0)           # 머리 받침
    # 뒷좌석 — 바퀴집에 걸릴 수 있어 차체 안쪽에 맞춘다
    rw = 2 * (iw - 0.04)
    rbox((rear_x + 0.02, 0.0, fz + 0.17), (0.50, rw, 0.14), 0.05, FABRIC, tilt=-6.0, fit=True)
    rbox((rear_x - 0.30, 0.0, fz + 0.47), (0.13, rw, 0.58), 0.05, FABRIC, tilt=21.0, fit=True)
    for sy in (-0.38, 0.38):
        rbox((rear_x - 0.43, sy, fz + 0.845), (0.09, 0.24, 0.16), 0.035, FABRIC_LIGHT, tilt=12.0, fit=True)
    # 문 팔걸이 (앞 · 뒤 양쪽)
    for sx in (seat_x + 0.10, rear_x + 0.10):
        for sy in (-1.0, 1.0):
            rbox((sx, sy * (iw + 0.03), fz + 0.42), (0.52, 0.11, 0.07), 0.025, PLASTIC_GREY, segs=1, fit=True)
    # 실내 거울
    rbox((gx1 - 0.66, 0.0, z_top - 0.19), (0.035, 0.22, 0.065), 0.012, PLASTIC, segs=1, fit=True)

    # ---- 차체 안쪽에 맞추기: 가장 가까운 몸통 면의 바깥쪽(법선 쪽)이면 면 안쪽으로
    bb = bmesh.new()
    bb.from_mesh(body_me)
    tree = BVHTree.FromBMesh(bb)
    margin = c.get("conform_margin", 0.018) / SY
    main.verts.ensure_lookup_table()
    moved = 0
    for _ in range(10):
        for n0, n1 in conform:
            for i in range(n0, n1):
                v = main.verts[i]
                loc, nor, idx, dist = tree.find_nearest(v.co)
                if loc is None:
                    continue
                s = (v.co - loc).dot(nor)
                if s > -margin:
                    v.co = v.co - nor * (s + margin)
                    moved += 1
    # 꼭짓점은 안쪽이어도 면 가운데가 차체 밖일 수 있다(바퀴집처럼 차체가 객실 쪽으로 파고든 곳을 가로지르는 판) → 그런 면은 지운다.
    # 그 자리는 안쪽 껍데기(바퀴집 안쪽 면)가 막고 있다
    main.faces.ensure_lookup_table()
    dead = []
    for f in main.faces:
        probes = [f.calc_center_median()] + [(e.verts[0].co + e.verts[1].co) * 0.5 for e in f.edges]
        for q in probes:
            loc, nor, idx, dist = tree.find_nearest(q)
            if loc is not None and (q - loc).dot(nor) > 0.0005:
                dead.append(f)
                break
    if dead:
        bmesh.ops.delete(main, geom=dead, context="FACES")
        loose = [v for v in main.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(main, geom=loose, context="VERTS")
    main.verts.ensure_lookup_table()
    main.faces.ensure_lookup_table()
    faces_out = 0
    for f in main.faces:
        q = f.calc_center_median()
        loc, nor, idx, dist = tree.find_nearest(q)
        if loc is not None and (q - loc).dot(nor) > 0.0005:
            faces_out += 1
    outside = 0
    worst = 0.0
    for v in main.verts:                                   # 검사: 실내 꼭짓점이 몸통 겉면 밖에 남아 있으면 안 된다
        loc, nor, idx, dist = tree.find_nearest(v.co)
        if loc is None:
            continue
        s = (v.co - loc).dot(nor)
        if s > -0.0005:
            outside += 1
            worst = max(worst, s * SY)
    bb.free()
    me = bpy.data.meshes.new(name + "_Cabin")
    main.normal_update()
    main.to_mesh(me)
    main.free()
    mat = bpy.data.materials.get("Vehicle_Cabin") or bpy.data.materials.new("Vehicle_Cabin")
    me.materials.append(mat)
    ob = bpy.data.objects.new(name + "_Cabin", me)
    bpy.context.scene.collection.objects.link(ob)
    # 둥글린 모서리가 매끈하게 보이게 — 넓은 면 쪽으로 기운 법선
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    wn = ob.modifiers.new("wn", "WEIGHTED_NORMAL")
    wn.mode = "FACE_AREA"
    wn.weight = 50
    wn.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier=wn.name)
    me.calc_loop_triangles()
    log["cabin"] = {"parts": stats["parts"], "tris": len(me.loop_triangles), "conform_moves": moved, "faces_removed_outside": len(dead), "faces_outside_body": faces_out, "verts_outside_body": outside,
                    "worst_outside_m": round(worst, 4), "seat_x": round(seat_x, 3), "rear_x": round(rear_x, 3), "dash_x": round(dash_x, 3)}
    return ob
