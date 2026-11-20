# Usage: blender -b --factory-startup --python make_dept_wall.py -- <map_dir> [preview_dir]
#   <map_dir> = Assets/C/Textures/Map — 그 아래 DeptWall1 폴더에 쓴다 (원본: Marble/Marble021 · Interior2/Wood051 · MarbleBlack/Marble016)
# 청운백화점(N05) 실내 벽 (사용자 11/06 — 목업 'B 원목'). 한 층 높이(3.4 m) 한 장에 아래부터:
#   0 ~ 0.25 m  검정 대리석 걸레받이 (Marble016, 10 mm 도드라짐 · 윗모서리 둥글게)
#   0.25 ~ 1.15 m 호두나무 판벽 (Wood051, 6 mm 도드라짐 · 판 1.2 m 마다 V 줄눈)
#   1.15 ~ 1.20 m 황동 손잡이 띠 (둥글게 8 mm 도드라짐, 금속)
#   1.20 ~ 3.4 m 크림 대리석 (Marble021 에 크림색을 곱함 · 판 1.2 × 1.1 m 가는 줄눈)
#   한 장 = 가로 2.4 m × 세로 3.4 m (2048 × 2904 px). Unity 재질 Tiling (1/2.4, 1/3.4), 아래 끝 = 층 바닥 높이 — 빌더가 맞춘다
#   벽마다 판을 덧대지 않고 이 한 장으로 모든 실내 벽(칸막이 · 계단실 · 바깥벽 안쪽)에 같은 높이로 깔린다 (맵 좌표 UV)
#   DeptWall1_Color.png (sRGB) · _NormalGL.png · _AmbientOcclusion.png · _MetallicSmoothness.png (R = 금속, A = 매끄러움)
import bpy, sys, os, math
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
map_dir = argv[0]
preview_dir = argv[1] if len(argv) > 1 else None
W_M, H_M, W_PX, H_PX = 2.4, 3.4, 2048, 2904
mx, my = W_M / W_PX, H_M / H_PX
CREAM = np.array([1.0, 0.93, 0.82])
BRASS = np.array([198, 158, 90]) / 255.0


def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    # JPG(바이트 그림)의 pixels 는 파일에 적힌 sRGB 값 그대로 — 그대로 섞어 sRGB 로 저장한다
    # (처음엔 선형으로 보고 한 번 더 sRGB 로 바꿔서 색이 허옇게 바랬다)
    return np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., :3]


def sample(tex, x_m, y_m, tile_m):
    h, w = tex.shape[:2]
    ix = (np.floor(x_m / tile_m * w).astype(np.int64)) % w
    iy = (np.floor(y_m / tile_m * h).astype(np.int64)) % h
    return tex[iy, ix]


marble = load(os.path.join(map_dir, "Marble", "Marble021_2K-JPG_Color.jpg"))
wood = load(os.path.join(map_dir, "Interior2", "Wood051_2K-JPG_Color.jpg"))
black = load(os.path.join(map_dir, "MarbleBlack", "Marble016_2K-JPG_Color.jpg"))

yy, xx = np.mgrid[0:H_PX, 0:W_PX].astype(np.float64)
x, y = (xx + 0.5) * mx, (yy + 0.5) * my
BASE, WAIN, RAIL = 0.25, 1.15, 1.20
z_base = y < BASE
z_wood = (y >= BASE) & (y < WAIN)
z_rail = (y >= WAIN) & (y < RAIL)
z_marble = y >= RAIL

# ---- 높이 (mm)
height = np.zeros_like(x)
# 대리석 판 줄눈: 1.2 m 마다 세로, 2.3 m 에 가로 (2 mm · 1.5 mm 깊이)
jx = np.minimum(np.mod(x, 1.2), 1.2 - np.mod(x, 1.2)) < 0.001
jy = np.abs(y - 2.3) < 0.001
height = np.where(z_marble & (jx | jy), -1.5, height)
# 판벽: 6 mm 도드라짐, 판 1.2 m 마다 V 줄눈 (폭 8 mm), 위아래 끝 4 mm 모따기
px = np.minimum(np.mod(x + 0.6, 1.2), 1.2 - np.mod(x + 0.6, 1.2))
v_groove = np.clip(px / 0.004, 0, 1)
bevel = np.clip(np.minimum(y - BASE, WAIN - y) / 0.004, 0, 1)
height = np.where(z_wood, 6.0 * np.minimum(v_groove, bevel), height)
# 황동 띠: 둥글게 8 mm
t = np.clip((y - (WAIN + RAIL) / 2) / ((RAIL - WAIN) / 2), -1, 1)
height = np.where(z_rail, 8.0 * np.sqrt(np.clip(1 - t * t, 0, 1)), height)
# 걸레받이: 10 mm, 윗모서리 반지름 5 mm
top_r = np.clip((BASE - y) / 0.005, 0, 1)
height = np.where(z_base, 10.0 * np.sqrt(np.clip(1 - (1 - top_r) ** 2, 0, 1)), height)

dhdx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) / (2 * mx * 1000)
dhdy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) / (2 * my * 1000)
nrm = np.dstack([-np.clip(dhdx, -3, 3), -np.clip(dhdy, -3, 3), np.ones_like(height)])
nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)

# ---- 그늘: 줄눈 · 판벽 V 줄 · 띠 아래 · 걸레받이 위 대리석 쪽
ao = np.ones_like(x)
ao = np.where(z_marble & (jx | jy), 0.55, ao)
ao = np.where(z_wood, 0.75 + 0.25 * np.minimum(v_groove, bevel), ao)
under_rail = (y >= RAIL) & (y < RAIL + 0.03)
ao = np.where(under_rail, 0.8 + 0.2 * (y - RAIL) / 0.03, ao)
above_base = (y >= BASE) & (y < BASE + 0.02)
ao = np.where(above_base & z_wood, ao * (0.8 + 0.2 * (y - BASE) / 0.02), ao)

# ---- 색
color = np.zeros(x.shape + (3,))
color = np.where(z_marble[..., None], sample(marble, x, y, 1.8) * CREAM, color)
color = np.where(z_wood[..., None], sample(wood, y, x, 2.4), color)          # 나뭇결을 세로로 (판벽은 결이 위아래)
color = np.where(z_rail[..., None], BRASS * (0.85 + 0.25 * np.sqrt(np.clip(1 - t * t, 0, 1)))[..., None], color)
color = np.where(z_base[..., None], sample(black, x, y, 1.4), color)
color = np.where((z_marble & (jx | jy))[..., None], color * 0.6, color)
color = np.clip(color * (0.8 + 0.2 * ao)[..., None], 0, 1)

metal = np.where(z_rail, 1.0, 0.0)
smooth = np.where(z_marble, 0.86, np.where(z_wood, 0.55, np.where(z_rail, 0.78, 0.88)))
smooth = np.where(z_marble & (jx | jy), 0.2, smooth)


def save(name, rgba, non_color):
    folder = os.path.join(map_dir, "DeptWall1")
    os.makedirs(folder, exist_ok=True)
    img = bpy.data.images.new(name, W_PX, H_PX, alpha=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    path = os.path.join(folder, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("WROTE", path, os.path.getsize(path), flush=True)
    return path


ones, zeros = np.ones_like(x), np.zeros_like(x)
color_path = save("DeptWall1_Color", np.dstack([color, ones]), False)
normal_path = save("DeptWall1_NormalGL", np.dstack([nrm * 0.5 + 0.5, ones]), True)
save("DeptWall1_AmbientOcclusion", np.dstack([ao, ao, ao, ones]), True)
save("DeptWall1_MetallicSmoothness", np.dstack([metal, zeros, zeros, smooth]), True)

if preview_dir:
    os.makedirs(preview_dir, exist_ok=True)
    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    p = bpy.context.active_object
    p.scale = (4.8, 3.4, 1.0)
    p.rotation_euler = (math.radians(90), 0, 0)
    p.location = (2.4, 0, 1.7)
    for loop in p.data.uv_layers.active.data:
        loop.uv = (loop.uv[0] * 2.0, loop.uv[1])
    mat = bpy.data.materials.new("Wall")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    ct = nt.nodes.new("ShaderNodeTexImage")
    ct.image = bpy.data.images.load(color_path)
    nt2 = nt.nodes.new("ShaderNodeTexImage")
    nt2.image = bpy.data.images.load(normal_path)
    nt2.image.colorspace_settings.name = "Non-Color"
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(ct.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(nt2.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.2
    p.data.materials.append(mat)
    light = bpy.data.objects.new("Light", bpy.data.lights.new("Light", "AREA"))
    light.data.energy = 900
    light.data.size = 3
    light.location = (2.4, -2.5, 3.6)
    light.rotation_euler = (math.radians(50), 0, 0)
    scene.collection.objects.link(light)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.25, 0.23, 0.2, 1.0)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 32
    cam.location = (0.6, -4.0, 1.4)
    cam.rotation_euler = (math.radians(88), 0, math.radians(-28))
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 860
    scene.render.filepath = os.path.join(preview_dir, "dept_wall_preview.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath, flush=True)
