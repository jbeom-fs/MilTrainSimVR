# Usage: blender -b --factory-startup --python make_window_band.py -- <out_dir> [preview_dir]
#   보통 <out_dir> = Assets/C/Textures/Map/Window1. preview_dir 을 주면 거기에 텍스처를 낮은 해 · 하늘 아래 그린 미리보기를 둔다
# 일반 건물 창 (11/03) — 한국 상가 · 사무 건물에 흔한 띠창: 알루미늄 창틀, 1.2 m 마다 세로 창살, 위쪽 고정창 · 아래쪽 미닫이,
# 유리 뒤로 블라인드 · 커튼 · 어두운 실내가 칸마다 다르게 보인다. 막힌 위층 앞면에 붙는 창 띠(UrbanGreyboxBuilder 의 Windows_nF)에 쓴다.
#   한 장 = 가로 8칸(9.6 m) × 4개 층(13.6 m) — 층마다 칸 모양이 달라 위아래로 같은 블라인드가 겹쳐 보이지 않는다.
#   한 층(3.4 m) 안에서 바닥 위 0.9 ~ 2.2 m 가 창 띠, 나머지는 벽(흰색 — 나중에 앞면 전체에 쓸 때 벽 색을 곱한다)
#   Unity 재질: Tiling (1/9.6, 1/13.6), Offset y = −인도 높이/13.6 (맵 UV 는 1 = 1 m, 세로는 월드 높이) — 빌더가 맞춘다
#   Window1_Color.png (sRGB) · _NormalGL.png · _AmbientOcclusion.png · _MetallicSmoothness.png (R = 금속, A = 매끄러움)
import bpy, sys, os, math
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir = argv[0]
preview_dir = argv[1] if len(argv) > 1 else None
os.makedirs(out_dir, exist_ok=True)

SIZE = 2048
WIDTH_M = 9.6            # 가로 8칸
HEIGHT_M = 13.6          # 4개 층
STOREY_M = 3.4
BAY_M = 1.2
BAND = (0.9, 2.2)        # 한 층 바닥 위 창 띠 아래 · 위 (m)
FRAME_M = 0.06           # 바깥 창틀 · 세로 창살 폭
TRANSOM_Y = 1.80         # 위 고정창 / 아래 미닫이 나눔 (바닥 위 m)
TRANSOM_M = 0.04
STILE_M = 0.035          # 미닫이 두 짝이 겹치는 가운데 살
SEED = 1105

GLASS = np.array([0.105, 0.135, 0.165])                     # 어두운 청회색 유리
FRAME = np.array([0.62, 0.63, 0.65])                        # 알루미늄
WALL = np.array([0.92, 0.92, 0.91])                         # 흰 벽 (재질 색을 곱한다)
BLIND_COLORS = np.array([[0.78, 0.76, 0.71], [0.82, 0.82, 0.80], [0.70, 0.68, 0.62], [0.74, 0.76, 0.78]])
CURTAIN_COLORS = np.array([[0.62, 0.55, 0.45], [0.48, 0.52, 0.58], [0.70, 0.66, 0.60], [0.55, 0.42, 0.40]])

W_PX = SIZE
H_PX = SIZE
mx = WIDTH_M / W_PX
my = HEIGHT_M / H_PX
rng = np.random.default_rng(SEED)


def noise(sigma_x, sigma_y, seed):
    # 가로 sigma_x · 세로 sigma_y px 로 흐린 흰 노이즈 (표준편차 1). FFT 라 가장자리가 이어진다
    g = np.random.default_rng(SEED * 31 + seed).standard_normal((H_PX, W_PX))
    fy = np.fft.fftfreq(H_PX)[:, None]
    fx = np.fft.rfftfreq(W_PX)[None, :]
    filt = np.exp(-2.0 * np.pi ** 2 * ((sigma_x * fx) ** 2 + (sigma_y * fy) ** 2))
    n = np.fft.irfft2(np.fft.rfft2(g) * filt, s=(H_PX, W_PX))
    return n / (n.std() + 1e-12)


yy, xx = np.mgrid[0:H_PX, 0:W_PX].astype(np.float64)
x = (xx + 0.5) * mx
y = (yy + 0.5) * my
storey = np.floor(y / STOREY_M).astype(np.int64)
ly = y - storey * STOREY_M
bay = np.floor(x / BAY_M).astype(np.int64)
lx = x - bay * BAY_M

in_band = (ly >= BAND[0]) & (ly <= BAND[1])
upper = ly >= TRANSOM_Y + TRANSOM_M / 2
half = FRAME_M / 2
frame = in_band & (
    (ly < BAND[0] + FRAME_M) | (ly > BAND[1] - FRAME_M)                     # 위 · 아래 창틀
    | (lx < half) | (lx > BAY_M - half)                                      # 세로 창살 (칸 경계)
    | (np.abs(ly - TRANSOM_Y) < TRANSOM_M / 2)                               # 가로 살
    | (~upper & (np.abs(lx - BAY_M / 2) < STILE_M / 2)))                     # 미닫이 가운데 살
glass = in_band & ~frame
# 유리 한 장 안에서의 자리 (0 ~ 1) — 블라인드 · 커튼 · 반사 그림에 쓴다
pane_bottom = np.where(upper, TRANSOM_Y + TRANSOM_M / 2, BAND[0] + FRAME_M)
pane_top = np.where(upper, BAND[1] - FRAME_M, TRANSOM_Y - TRANSOM_M / 2)
pv = np.clip((ly - pane_bottom) / (pane_top - pane_bottom), 0.0, 1.0)
pu = np.clip((lx - half) / (BAY_M - FRAME_M), 0.0, 1.0)

# 칸(층 · 칸)마다 실내 모양 — 위아래 고정창 · 미닫이는 같은 칸이면 같은 방이라 같은 모양
storeys = int(round(HEIGHT_M / STOREY_M))
bays = int(round(WIDTH_M / BAY_M))
cell = storey * bays + bay
n_cells = storeys * bays
kind = rng.choice(5, size=n_cells, p=[0.32, 0.36, 0.14, 0.10, 0.08])    # 0 어두움 · 1 블라인드 · 2 커튼 · 3 세로 블라인드 · 4 불 켜진 방
blind_drop = rng.uniform(0.15, 0.95, n_cells)                            # 블라인드가 위에서 내려온 정도
blind_col = BLIND_COLORS[rng.integers(0, len(BLIND_COLORS), n_cells)]
curtain_col = CURTAIN_COLORS[rng.integers(0, len(CURTAIN_COLORS), n_cells)]
curtain_side = rng.random(n_cells) < 0.5
curtain_open = rng.uniform(0.25, 0.6, n_cells)                           # 커튼이 덮은 폭 (칸 너비 비율)
room_tone = rng.uniform(0.7, 1.3, n_cells)
glass_tint = rng.uniform(0.92, 1.08, n_cells)

k = kind[cell]
# 칸 안 가로 위치 (0 ~ 1, 칸 전체 기준) — 커튼 · 세로 블라인드
cu = lx / BAY_M
content = np.zeros((H_PX, W_PX, 3))
alpha = np.zeros((H_PX, W_PX))
# 블라인드: 위에서 내려와 살마다 밝고 어두운 줄 (살 2.5 cm)
band_top = BAND[1] - FRAME_M
blind_edge = band_top - blind_drop[cell] * (band_top - BAND[0] - FRAME_M)
slat = 0.75 + 0.25 * np.cos(2 * np.pi * (ly / 0.025))
is_blind = (k == 1) & (ly > blind_edge)
content = np.where(is_blind[..., None], blind_col[cell] * slat[..., None], content)
alpha = np.where(is_blind, 0.85, alpha)
# 커튼: 한쪽에서 주름진 천
fold = 0.78 + 0.22 * np.sin(2 * np.pi * cu * 14.0 + 0.6 * noise(6, 80, 1))
side = np.where(curtain_side[cell], cu, 1.0 - cu)
is_curtain = (k == 2) & (side < curtain_open[cell])
content = np.where(is_curtain[..., None], curtain_col[cell] * fold[..., None], content)
alpha = np.where(is_curtain, 0.8, alpha)
# 세로 블라인드: 10 cm 마다 세로 판
vslat = 0.8 + 0.2 * np.cos(2 * np.pi * (lx / 0.1))
is_vblind = k == 3
content = np.where(is_vblind[..., None], np.array([0.80, 0.80, 0.78]) * vslat[..., None], content)
alpha = np.where(is_vblind, 0.75, alpha)
# 불 켜진 방: 따뜻한 빛 + 천장 등 줄
lit_col = np.array([0.62, 0.56, 0.44]) * (0.85 + 0.15 * pv)[..., None]
is_lit = k == 4
content = np.where(is_lit[..., None], lit_col, content)
alpha = np.where(is_lit, 0.55, alpha)
# 어두운 방: 가구 · 칸막이 그림자만 희미하게
dark_shapes = 0.5 + 0.5 * np.clip(noise(30, 12, 2), -1, 1)
is_dark = k == 0
content = np.where(is_dark[..., None], np.array([0.22, 0.22, 0.23]) * dark_shapes[..., None] * room_tone[cell][..., None], content)
alpha = np.where(is_dark, 0.35, alpha)

# 유리 = 유리 색과 안쪽을 섞고, 위로 갈수록 하늘이 조금 비친다 (진짜 반사는 재질 매끄러움으로)
glass_col = GLASS * glass_tint[cell][..., None]
reflect = (0.035 + 0.05 * pv)[..., None] * np.array([0.75, 0.85, 1.0])
streak = 0.02 * np.clip(noise(40, 3, 3), 0, None)[..., None]
glass_rgb = glass_col * (1 - alpha[..., None]) + content * alpha[..., None] * 0.7 + reflect + streak

frame_rgb = FRAME[None, None, :] * (0.94 + 0.06 * noise(2, 2, 4))[..., None]
wall_rgb = WALL[None, None, :] * (0.97 + 0.03 * noise(3, 3, 5))[..., None]
slab = (ly < 0.06) | (ly > STOREY_M - 0.06)                               # 층 줄눈
wall_rgb = np.where(slab[..., None], wall_rgb * 0.85, wall_rgb)

color = np.where(glass[..., None], glass_rgb, np.where(frame[..., None], frame_rgb, wall_rgb))
color = np.clip(color, 0, 1)

# 높이 (mm): 창틀은 유리보다 12 mm 앞, 벽은 창 띠보다 40 mm 뒤 (나중에 앞면 전체에 쓸 때 창이 벽에서 살짝 나와 보이게)
height = np.where(frame, 12.0, np.where(glass, 0.0, -40.0 + 0.3 * noise(2, 2, 6)))
height = np.where(slab & ~in_band, height - 6.0, height)
px_x = mx * 1000.0
px_y = my * 1000.0
dhdx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) / (2.0 * px_x)
dhdy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) / (2.0 * px_y)
# 1 px 계단이라 기울기가 너무 세다 — 모서리를 2 px 정도로 무르게
nrm = np.dstack([-np.clip(dhdx, -1.5, 1.5), -np.clip(dhdy, -1.5, 1.5), np.ones_like(height)])
nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)

# 창틀 가까운 유리는 조금 어둡게 (AO)
near_frame = np.zeros_like(height)
for dx_, dy_ in ((4, 0), (-4, 0), (0, 3), (0, -3), (2, 0), (-2, 0), (0, 1), (0, -1)):
    near_frame = np.maximum(near_frame, np.roll(np.roll(frame, dx_, axis=1), dy_, axis=0).astype(np.float64))
ao = np.where(glass, 1.0 - 0.15 * near_frame, 1.0)
ao = np.where(slab & ~in_band, 0.8, ao)

metal = np.where(frame, 0.85, 0.0)
smooth = np.where(glass, 0.92, np.where(frame, 0.55, 0.15))


def save(name, rgba, non_color):
    img = bpy.data.images.new(name, W_PX, H_PX, alpha=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    path = os.path.join(out_dir, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("WROTE", path, os.path.getsize(path))
    return path


ones = np.ones_like(height)
zeros = np.zeros_like(height)
color_path = save("Window1_Color", np.dstack([color, ones]), False)
normal_path = save("Window1_NormalGL", np.dstack([nrm * 0.5 + 0.5, ones]), True)
save("Window1_AmbientOcclusion", np.dstack([ao, ao, ao, ones]), True)
save("Window1_MetallicSmoothness", np.dstack([metal, zeros, zeros, smooth]), True)
print("KINDS", np.bincount(kind, minlength=5).tolist())

# ---------------------------------------------------------------- 미리보기 — 창 띠 둘을 붙인 벽 (Cycles)
if preview_dir:
    os.makedirs(preview_dir, exist_ok=True)
    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    # 벽 판 (가로 9.6 m × 높이 6.8 m = 두 층) 에 텍스처를 그대로 — 아래 두 층만 보이게 UV 를 반으로
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    wall = bpy.context.active_object
    wall.scale = (9.6, 6.8, 1.0)
    wall.rotation_euler = (math.radians(90), 0.0, 0.0)
    for loop in wall.data.uv_layers.active.data:
        loop.uv = (loop.uv[0], loop.uv[1] * 0.5)
    mat = bpy.data.materials.new("Window")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    color_tex = nt.nodes.new("ShaderNodeTexImage")
    color_tex.image = bpy.data.images.load(color_path)
    normal_tex = nt.nodes.new("ShaderNodeTexImage")
    normal_tex.image = bpy.data.images.load(normal_path)
    normal_tex.image.colorspace_settings.name = "Non-Color"
    normal_map = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(color_tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
    nt.links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.15
    wall.data.materials.append(mat)

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(50), 0.0, math.radians(35))
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.45, 0.6, 0.85, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 35
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (3.0, -9.0, 0.6)
    cam.rotation_euler = (math.radians(88), 0.0, math.radians(14))
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    scene.render.filepath = os.path.join(preview_dir, "window_preview.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)
