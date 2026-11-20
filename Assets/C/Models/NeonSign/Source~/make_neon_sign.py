# Usage: blender -b --factory-startup --python make_neon_sign.py -- <model_dir> [preview_dir]
#   보통 <model_dir> = Assets/C/Models/NeonSign — C_NeonSign.fbx 와 Textures/C_NeonGlow.png 를 쓴다.
#   preview_dir 을 주면 밤 · 낮 미리보기를 거기에 그린다 (Unity 폴더 밖으로)
# 한글 네온 간판 '친구 / 맥주' (11/03 사용자 — 가게 앞 벽에 붙은 빨간 네온 글자 사진처럼, 한 개만).
#   · 글자 획을 하나하나 네온관이 지나갈 길(가운뎃줄)로 잡았다 — 글꼴 외곽선을 쓰면 속이 빈 '테두리 글자'가 된다.
#     꺾이는 곳은 유리관을 구부린 것처럼 둥글게(반지름 3.5 cm), 관끼리는 2.5 cm 넘게 띄운다
#   · 관 지름 1.8 cm, 벽에서 5 cm 앞. 관 끝마다 벽으로 들어가는 검은 전극 덮개, 20 cm 마다 관을 잡는 받침
#   · 뒤판: 글자보다 14 cm 넓은 검은 판(두께 2 cm) — 회색 벽에서도 사진의 어두운 벽처럼 보이게. Unity 에서 끌 수 있다
#   · 빛 번짐: 씬에 화면 번짐(Bloom)이 없어서 글자 뒤 벽에 빨간 빛 판(더하기 섞기)을 둔다 — 획에서 멀어질수록 옅게.
#     빛 판 텍스처는 흑백 밝기만 (색은 Unity 재질 C_NeonGlow 의 Base Color)
#   앞 = Blender −Y (Unity +Z), 원점 = 글자 아래 가운데의 벽 면. 크기 1.06 × 1.15 m
#   물체(= Unity 재질): NeonTubes(NeonTube) · NeonCaps(NeonBlack) · NeonSupports(NeonSupport) · NeonBoard(NeonBoard) · NeonGlow(NeonGlow)
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
model_dir = argv[0]
preview_dir = argv[1] if len(argv) > 1 else None
os.makedirs(os.path.join(model_dir, "Textures"), exist_ok=True)

CELL = 0.5            # 글자 한 칸 (m)
GAP = 0.10            # 글자 사이
LINE_GAP = 0.16       # 줄 사이
TUBE_R = 0.009        # 관 반지름 — 처음 7.5 mm 는 사진보다 가늘어 보였다
BEND_R = 0.035        # 구부린 곳 반지름
FRONT = 0.05          # 벽에서 관 가운데까지
SUPPORT_EVERY = 0.2
BOARD_T = 0.02        # 뒤판 두께 — 벽 ~ 2 cm 를 채운다 (관은 판 앞 3 cm)
BOARD_MARGIN = 0.14   # 뒤판이 글자보다 넓은 만큼
BOARD_R = 0.03        # 뒤판 모서리 반지름
GLOW_MARGIN = 0.3     # 빛 판이 글자보다 넓은 만큼
GLOW_PX = 1024

# 글자 칸 안의 획 (u 오른쪽, v 위, 0 ~ 1). 획 하나 = 관 하나
GLYPHS = {
    "친": [
        [(0.25, 0.96), (0.41, 0.96)],                      # ㅊ 꼭지
        [(0.07, 0.85), (0.59, 0.85)],                      # ㅊ 가로
        [(0.08, 0.46), (0.33, 0.76), (0.60, 0.46)],        # ㅊ 아래 (ㅅ)
        [(0.78, 0.97), (0.78, 0.38)],                      # ㅣ
        [(0.14, 0.32), (0.14, 0.05), (0.90, 0.05)],        # ㄴ 받침
    ],
    "구": [
        [(0.12, 0.88), (0.84, 0.88), (0.84, 0.60)],        # ㄱ
        [(0.04, 0.47), (0.96, 0.47)],                      # ㅜ 가로
        [(0.50, 0.40), (0.50, 0.06)],                      # ㅜ 세로
    ],
    "맥": [
        [(0.14, 0.46), (0.52, 0.46), (0.52, 0.93), (0.07, 0.93), (0.07, 0.53)],   # ㅁ — 왼쪽 아래 모서리에서 열림(전극 자리)
        [(0.66, 0.98), (0.66, 0.38)],                      # ㅐ 첫 세로
        [(0.71, 0.68), (0.85, 0.68)],                      # ㅐ 가운데
        [(0.90, 0.98), (0.90, 0.38)],                      # ㅐ 둘째 세로
        [(0.12, 0.30), (0.86, 0.30), (0.86, 0.03)],        # ㄱ 받침
    ],
    "주": [
        [(0.14, 0.88), (0.86, 0.88)],                      # ㅈ 가로
        [(0.18, 0.56), (0.50, 0.80), (0.82, 0.56)],        # ㅈ 아래 (ㅅ)
        [(0.04, 0.47), (0.96, 0.47)],                      # ㅜ 가로
        [(0.50, 0.40), (0.50, 0.06)],                      # ㅜ 세로
    ],
}
LINES = ["친구", "맥주"]


def layout():
    # 글자마다 칸의 왼쪽 아래 (x, z) — 두 줄, 가운데 맞춤, 아래 끝 z = 0
    width = 2 * CELL + GAP
    cells = []
    for row, line in enumerate(LINES):
        z = (len(LINES) - 1 - row) * (CELL + LINE_GAP)
        for col, ch in enumerate(line):
            cells.append((ch, -width / 2 + col * (CELL + GAP), z))
    return cells


def fillet(points, radius, seg=8):
    # 꺾인 곳을 반지름 radius 원호로 (양쪽 직선에 접하게). 짧은 직선은 반지름을 줄인다
    out = [points[0]]
    for i in range(1, len(points) - 1):
        p0, p1, p2 = points[i - 1], points[i], points[i + 1]
        d0, d1 = p1 - p0, p2 - p1
        l0, l1 = d0.length, d1.length
        d0.normalize()
        d1.normalize()
        turn = math.acos(max(-1.0, min(1.0, d0.dot(d1))))
        if turn < 1e-3:
            out.append(p1)
            continue
        t = min(radius * math.tan(turn / 2), l0 * 0.45, l1 * 0.45)
        r = t / math.tan(turn / 2)
        a, b = p1 - d0 * t, p1 + d1 * t
        left = d0.x * d1.y - d0.y * d1.x > 0
        normal = Vector((-d0.y, d0.x)) if left else Vector((d0.y, -d0.x))
        c = a + normal * r
        ang_a = math.atan2(a.y - c.y, a.x - c.x)
        ang_b = math.atan2(b.y - c.y, b.x - c.x)
        sweep = (ang_b - ang_a + math.pi) % (2 * math.pi) - math.pi
        for k in range(seg + 1):
            ang = ang_a + sweep * k / seg
            out.append(c + Vector((math.cos(ang), math.sin(ang))) * r)
    out.append(points[-1])
    return out


# 획 → 벽 면(x, z) 위 둥근 길
strokes = []
for ch, x0, z0 in layout():
    for stroke in GLYPHS[ch]:
        pts = [Vector((x0 + u * CELL, z0 + v * CELL)) for u, v in stroke]
        strokes.append(fillet(pts, BEND_R))

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
scene = bpy.context.scene
depsgraph = bpy.context.evaluated_depsgraph_get()


def material(name, rgb, emit=0.0, rough=0.4):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    if emit > 0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit
    return m


NEON = (1.0, 0.06, 0.04)
mat_tube = material("NeonTube", NEON, emit=12.0, rough=0.1)
mat_black = material("NeonBlack", (0.02, 0.02, 0.02), rough=0.6)
mat_support = material("NeonSupport", (0.7, 0.72, 0.74), rough=0.35)
mat_board = material("NeonBoard", (0.035, 0.035, 0.04), rough=0.55)


def mesh_object(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    scene.collection.objects.link(obj)
    me.materials.append(mat)
    return obj


# 관 — 곡선에 두께(bevel)를 주고 메시로 굽는다
curve = bpy.data.curves.new("NeonTubesCurve", "CURVE")
curve.dimensions = "3D"
curve.bevel_depth = TUBE_R
curve.bevel_resolution = 3
curve.use_fill_caps = True
for pts in strokes:
    spline = curve.splines.new("POLY")
    spline.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        spline.points[i].co = (p.x, -FRONT, p.y, 1.0)
curve_obj = bpy.data.objects.new("NeonTubesCurve", curve)
scene.collection.objects.link(curve_obj)
depsgraph = bpy.context.evaluated_depsgraph_get()
tube_mesh = bpy.data.meshes.new_from_object(curve_obj.evaluated_get(depsgraph))
tube_mesh.name = "NeonTubes"
tubes = bpy.data.objects.new("NeonTubes", tube_mesh)
scene.collection.objects.link(tubes)
tube_mesh.materials.clear()
tube_mesh.materials.append(mat_tube)
bpy.data.objects.remove(curve_obj)


def cylinder(bm, start, end, radius, segments=12):
    # start → end 원기둥 (양 끝 막음)
    axis = end - start
    length = axis.length
    mat = axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
    mat.translation = (start + end) / 2
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=length, matrix=mat)


# 전극 덮개 — 관 끝마다 벽으로 들어가는 검은 통
bm_caps = bmesh.new()
for pts in strokes:
    for p in (pts[0], pts[-1]):
        cylinder(bm_caps, Vector((p.x, -FRONT - 0.004, p.y)), Vector((p.x, 0.0, p.y)), TUBE_R * 1.35)
caps = mesh_object("NeonCaps", bm_caps, mat_black)

# 받침 — 획 길이를 따라 20 cm 마다 (짧은 획은 가운데 하나)
bm_sup = bmesh.new()
support_count = 0
for pts in strokes:
    seglen = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]
    total = sum(seglen)
    n = max(1, round(total / SUPPORT_EVERY))
    for k in range(n):
        s = (k + 0.5) * total / n
        for i, l in enumerate(seglen):
            if s <= l:
                p = pts[i].lerp(pts[i + 1], s / l)
                break
            s -= l
        cylinder(bm_sup, Vector((p.x, -FRONT, p.y)), Vector((p.x, 0.0, p.y)), 0.003, 8)
        cylinder(bm_sup, Vector((p.x, -FRONT + TUBE_R + 0.002, p.y)), Vector((p.x, -FRONT + TUBE_R - 0.004, p.y)), 0.009, 12)  # 관 뒤 집게
        support_count += 1
supports = mesh_object("NeonSupports", bm_sup, mat_support)

xs = [p.x for pts in strokes for p in pts]
zs = [p.y for pts in strokes for p in pts]

# ---------------------------------------------------------------- 뒤판 (검은 판)
# 회색 벽에서도 사진처럼 어두운 바탕 위에 빨간 관이 보이게. 전극 덮개 · 받침은 판을 지나 벽(y = 0)까지 그대로 닿으니
# Unity 에서 판(NeonBoard)을 꺼도 뜨는 곳이 없다
bx0, bx1 = min(xs) - TUBE_R - BOARD_MARGIN, max(xs) + TUBE_R + BOARD_MARGIN
bz0, bz1 = min(zs) - TUBE_R - BOARD_MARGIN, max(zs) + TUBE_R + BOARD_MARGIN
mid = Vector(((bx0 + bx1) / 2, bz0))   # 아래 변 가운데에서 시작해 네 모서리를 모두 둥글게
ring = fillet([mid, Vector((bx1, bz0)), Vector((bx1, bz1)), Vector((bx0, bz1)), Vector((bx0, bz0)), mid], BOARD_R)[:-1]
bm_board = bmesh.new()
front = [bm_board.verts.new((p.x, -BOARD_T, p.y)) for p in ring]
back = [bm_board.verts.new((p.x, 0.0, p.y)) for p in ring]
bm_board.faces.new(front)
bm_board.faces.new(back)
for i in range(len(ring)):
    j = (i + 1) % len(ring)
    bm_board.faces.new((front[i], front[j], back[j], back[i]))
bmesh.ops.recalc_face_normals(bm_board, faces=bm_board.faces)
board = mesh_object("NeonBoard", bm_board, mat_board)

# ---------------------------------------------------------------- 빛 판 (판 · 벽 위 빨간 번짐) — 뒤판 앞면 바로 앞
gx0, gx1 = min(xs) - GLOW_MARGIN, max(xs) + GLOW_MARGIN
gz0, gz1 = min(zs) - GLOW_MARGIN, max(zs) + GLOW_MARGIN
gw, gh = gx1 - gx0, gz1 - gz0
px_w = GLOW_PX
px_h = int(round(GLOW_PX * gh / gw))
yy, xx = np.mgrid[0:px_h, 0:px_w].astype(np.float64)
wx = gx0 + (xx + 0.5) / px_w * gw
wz = gz0 + (yy + 0.5) / px_h * gh
dist = np.full(wx.shape, 1e9)
for pts in strokes:
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]
        abx, abz = b.x - a.x, b.y - a.y
        ll = abx * abx + abz * abz
        t = np.clip(((wx - a.x) * abx + (wz - a.y) * abz) / max(ll, 1e-12), 0.0, 1.0)
        dx = wx - (a.x + t * abx)
        dz = wz - (a.y + t * abz)
        dist = np.minimum(dist, np.sqrt(dx * dx + dz * dz))
glow = 0.55 * np.exp(-dist / 0.07) + 0.45 * np.exp(-(dist / 0.025) ** 2)
glow = np.clip(glow, 0.0, 1.0)
# 흑백(밝기만) — 색은 재질이 곱한다. 알파는 안 쓴다: Unity 더하기 섞기가 색에 알파를 곱하면 번짐이 제곱만큼 좁아진다
rgba = np.dstack([glow, glow, glow, np.ones_like(glow)])
glow_img = bpy.data.images.new("C_NeonGlow", px_w, px_h, alpha=False)
glow_img.pixels.foreach_set(rgba.astype(np.float32).ravel())
glow_path = os.path.join(model_dir, "Textures", "C_NeonGlow.png")
glow_img.filepath_raw = glow_path
glow_img.file_format = "PNG"
glow_img.save()
print("WROTE", glow_path, px_w, "x", px_h)

mat_glow = bpy.data.materials.new("NeonGlow")
mat_glow.use_nodes = True
nt = mat_glow.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputMaterial")
add = nt.nodes.new("ShaderNodeAddShader")
transp = nt.nodes.new("ShaderNodeBsdfTransparent")
emit = nt.nodes.new("ShaderNodeEmission")
tex = nt.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(glow_path)     # sRGB 로 읽는다 (Unity 도 sRGB) — 선형으로 읽으면 번짐이 너무 넓고 어두운 곳에 계단이 진다
tint = nt.nodes.new("ShaderNodeVectorMath")
tint.operation = "MULTIPLY"
tint.inputs[1].default_value = NEON
emit.inputs["Strength"].default_value = 3.0
nt.links.new(tex.outputs["Color"], tint.inputs[0])
nt.links.new(tint.outputs["Vector"], emit.inputs["Color"])
nt.links.new(transp.outputs[0], add.inputs[0])
nt.links.new(emit.outputs[0], add.inputs[1])
nt.links.new(add.outputs[0], out.inputs["Surface"])

gy = -BOARD_T - 0.002
bm_glow = bmesh.new()
v = [bm_glow.verts.new((gx0, gy, gz0)), bm_glow.verts.new((gx1, gy, gz0)),
     bm_glow.verts.new((gx1, gy, gz1)), bm_glow.verts.new((gx0, gy, gz1))]
face = bm_glow.faces.new(v)
uv = bm_glow.loops.layers.uv.new("UVMap")
for loop, uvv in zip(face.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
    loop[uv].uv = uvv
# 꼭짓점 순서(왼아래 → 오른아래 → 오른위)대로 이미 앞(−Y)을 본다. 뒤집으면 Unity 에서 벽 쪽을 봐서 안 보인다
glow_obj = mesh_object("NeonGlow", bm_glow, mat_glow)
print("STROKES", len(strokes), "SUPPORTS", support_count, "SIZE", round(max(xs) - min(xs) + 2 * TUBE_R, 3), "x", round(max(zs) - min(zs) + 2 * TUBE_R, 3))

# ---------------------------------------------------------------- FBX
for o in bpy.data.objects:
    o.select_set(o.name in ("NeonTubes", "NeonCaps", "NeonSupports", "NeonBoard", "NeonGlow"))
fbx_path = os.path.join(model_dir, "C_NeonSign.fbx")
# FBX_SCALE_ALL — Unity 에서 물체 크기가 100 이 아니라 1 로 들어온다
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={"MESH"}, mesh_smooth_type="FACE",
                         add_leaf_bones=False, bake_anim=False, path_mode="STRIP", apply_scale_options="FBX_SCALE_ALL")
print("WROTE", fbx_path, os.path.getsize(fbx_path))

# ---------------------------------------------------------------- 미리보기 (Cycles) — 어두운 벽에 붙인 밤 · 낮
if preview_dir:
    os.makedirs(preview_dir, exist_ok=True)
    bm_wall = bmesh.new()
    bmesh.ops.create_cube(bm_wall, size=1.0)
    for vert in bm_wall.verts:
        vert.co = Vector((vert.co.x * 3.0, vert.co.y * 0.2 + 0.1, vert.co.z * 2.6 + 0.55))
    wall = mesh_object("Wall", bm_wall, material("Wall", (0.16, 0.15, 0.15), rough=0.8))
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(55), 0.0, math.radians(30))
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 35
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (0.9, -3.2, 0.9)
    cam.rotation_euler = (math.radians(88), 0.0, math.radians(15))
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = "AgX"
    scene.render.resolution_x, scene.render.resolution_y = 1000, 800
    for name, sky, sun_power in (("neon_night", 0.01, 0.0), ("neon_day", 0.8, 3.0)):
        bg.inputs["Color"].default_value = (0.5, 0.6, 0.8, 1.0)
        bg.inputs["Strength"].default_value = sky
        sun_data.energy = sun_power
        scene.render.filepath = os.path.join(preview_dir, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDERED", scene.render.filepath)
