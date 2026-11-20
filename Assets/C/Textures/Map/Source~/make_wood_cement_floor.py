# Usage: blender -b --factory-startup --python make_wood_cement_floor.py -- <out_dir> [preview_dir]
#   보통 <out_dir> = Assets/C/Textures/Map/Interior5. preview_dir 을 주면 거기에 미리보기 그림을 그린다 (Unity 폴더 밖으로 — 가져오지 않게)
# 나무 무늬 시멘트 바닥 (11/03) — 멀리서는 나무 마루처럼 보이지만 실은 회색 시멘트 타일인 실내 바닥.
# 200 × 1200 mm 판을 줄마다 다르게 엇갈려 깐 2.4 m × 2.4 m 한 장, 이음새 없이 반복된다 (Unity 재질 Tiling 0.4167 — 맵 UV 는 1 = 1 m).
#   · 색: 판마다 조금씩 다른 회색 시멘트 + 옅은 나뭇결 · 옹이, 구름 같은 얼룩, 기공 (갈색이 아니라 시멘트 색)
#   · 요철: 나뭇결이 눌려 찍힌 결 · 옹이, 판 모서리 살짝 깎임, 3 mm 줄눈
#   · 나뭇결: 판마다 아치(곡률) · 결 간격 · 흔들림이 다르고, 옹이 둘레로 결이 휘어 돈다. 노이즈는 FFT 라 가장자리가 저절로 이어진다
#   WoodCement_Color.png (sRGB) · _NormalGL.png (OpenGL, Unity 와 같음) · _AmbientOcclusion.png · _MetallicSmoothness.png (A = 매끄러움)
import bpy, sys, os, math
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir = argv[0]
preview_dir = argv[1] if len(argv) > 1 else None
os.makedirs(out_dir, exist_ok=True)

SIZE = 2048              # px
AREA_MM = 2400.0         # 한 장이 덮는 크기 (2.4 m)
PLANK_W_MM = 200.0
PLANK_L_MM = 1200.0
GROUT_MM = 1.5           # 줄눈 반폭
BEVEL_MM = 2.5           # 판 모서리 깎인 폭
BEVEL_DEPTH_MM = 1.0
GROUT_DEPTH_MM = 2.0
SEED = 1104

# 시멘트 판 색 (sRGB) · 비율 — 한 판 안은 한 색
PALETTE = np.array([
    [0.620, 0.612, 0.590],   # 밝은 시멘트 회색 (조금 따뜻)
    [0.585, 0.580, 0.560],
    [0.650, 0.642, 0.620],
    [0.555, 0.552, 0.538],
    [0.605, 0.590, 0.560],   # 베이지 기 도는 회색
])
PALETTE_WEIGHTS = np.array([0.30, 0.25, 0.18, 0.15, 0.12])
GROUT = np.array([0.47, 0.46, 0.44])

px_mm = AREA_MM / SIZE
rows = int(round(AREA_MM / PLANK_W_MM))
per_row = int(round(AREA_MM / PLANK_L_MM))
assert abs(rows * PLANK_W_MM - AREA_MM) < 1e-6 and abs(per_row * PLANK_L_MM - AREA_MM) < 1e-6, "판이 한 장에 딱 맞아야 이어진다"
rng = np.random.default_rng(SEED)


def noise(sigma_x, sigma_y, seed):
    # 가로 sigma_x · 세로 sigma_y px 로 흐린 흰 노이즈 (표준편차 1). FFT 라 가장자리가 이어진다
    g = np.random.default_rng(SEED * 31 + seed).standard_normal((SIZE, SIZE))
    fy = np.fft.fftfreq(SIZE)[:, None]
    fx = np.fft.rfftfreq(SIZE)[None, :]
    filt = np.exp(-2.0 * np.pi ** 2 * ((sigma_x * fx) ** 2 + (sigma_y * fy) ** 2))
    n = np.fft.irfft2(np.fft.rfft2(g) * filt, s=(SIZE, SIZE))
    return n / (n.std() + 1e-12)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------- 판 배치 (가로로 긴 판, 줄마다 아무렇게나 엇갈림)
yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
x_mm = (xx + 0.5) * px_mm
y_mm = (yy + 0.5) * px_mm
row = np.floor(y_mm / PLANK_W_MM).astype(np.int64)
row_offset = rng.uniform(0.0, PLANK_L_MM, rows)
xs = np.mod(x_mm - row_offset[row], AREA_MM)               # 한 줄은 한 장 너비에서 감아 돈다 — 그래서 이어진다
col = np.floor(xs / PLANK_L_MM).astype(np.int64)
lx = xs - col * PLANK_L_MM                                  # 판 안 자리 (mm)
ly = y_mm - row * PLANK_W_MM
plank = row * per_row + col
n_planks = rows * per_row
d = np.minimum(np.minimum(lx, PLANK_L_MM - lx), np.minimum(ly, PLANK_W_MM - ly))

# 판마다 정하는 값
p_color = PALETTE[rng.choice(len(PALETTE), size=n_planks, p=PALETTE_WEIGHTS / PALETTE_WEIGHTS.sum())]
p_bright = rng.uniform(0.97, 1.03, n_planks)
p_spacing = rng.uniform(4.5, 9.0, n_planks)                # 결 간격 (mm)
p_curve = rng.uniform(-5e-4, 5e-4, n_planks)               # 아치 곡률 — 판 끝에서 결이 최대 약 18 cm 휜다 (겹친 아치)
p_center = rng.uniform(0.2, 0.8, n_planks) * PLANK_L_MM    # 아치 꼭대기 자리
p_yc = rng.uniform(-150.0, 350.0, n_planks)
p_phase = rng.random(n_planks)
p_warp = rng.uniform(4.0, 10.0, n_planks)                   # 결 흔들림 (mm)
p_knot = rng.random(n_planks) < 0.4                         # 옹이 있는 판
p_kx = rng.uniform(0.1, 0.9, n_planks) * PLANK_L_MM
p_ky = rng.uniform(0.3, 0.7, n_planks) * PLANK_W_MM
p_kr = rng.uniform(9.0, 18.0, n_planks)                     # 옹이 반지름 (mm)
p_flip = rng.choice([-1.0, 1.0], size=(n_planks, 2))        # 띠 · 얼룩 노이즈를 판마다 뒤집는다 — 안 그러면 판 경계를 넘어 이어져 보인다

# ---------------------------------------------------------------- 나뭇결 — 처음엔 결이 가늘고 옅어서 눈높이에서 나무로 안 보였다
warp = noise(160, 15, 1)         # 길이 방향으로 길게 늘어진 흔들림
warp_fine = noise(40, 4, 2)
fibers = noise(30, 0.8, 3)       # 가늘고 긴 결 줄무늬
streaks = noise(300, 12, 12)     # 결을 따라 길게 밝고 어두운 띠
knot = p_knot[plank]
kdx = lx - p_kx[plank]
kdy = ly - p_ky[plank]
kr = p_kr[plank]
around = np.exp(-((kdx / (kr * 4.0)) ** 2 + (kdy / (kr * 1.6)) ** 2)) * knot   # 옹이 둘레 — 결이 휘어 돈다
ring = (ly - p_yc[plank]) + p_curve[plank] * (lx - p_center[plank]) ** 2 \
    + p_warp[plank] * warp + 0.6 * warp_fine + kr * 2.0 * around * np.sign(kdy)
ring_jitter = noise(250, 25, 13)   # 결 간격이 군데군데 좁고 넓게 — 고르면 빗으로 그은 듯하다
f = np.mod(ring / p_spacing[plank] + p_phase[plank] + 0.45 * ring_jitter, 1.0)
late = smoothstep(0.55, 0.75, f) * (1.0 - smoothstep(0.85, 0.98, f))          # 진한 결 (늦재) — 한 결의 약 1/4
kd = np.hypot(kdx / kr, kdy / (kr * 0.75))
knot_core = knot & (kd < 1.0)
knot_rings = 0.5 + 0.5 * np.cos(2.0 * np.pi * kd * 3.0)

# ---------------------------------------------------------------- 높이 (mm, 판 윗면 = 0)
t = np.clip((d - GROUT_MM) / BEVEL_MM, 0.0, 1.0)          # 0 = 모서리 끝, 1 = 평평한 윗면
grout = d < GROUT_MM
pores = noise(1.0, 1.0, 5) > 2.8
grain_h = -0.25 * late - 0.06 * np.clip(fibers, 0.0, None) + 0.05 * noise(3, 3, 4)
knot_h = np.where(knot_core, -0.18 + 0.08 * knot_rings, 0.0)
top = -BEVEL_DEPTH_MM * (1.0 - t) ** 2 + (grain_h + knot_h - 0.35 * pores) * t
height = np.where(grout, -GROUT_DEPTH_MM + 0.3 * noise(1.5, 1.5, 6), top)

# ---------------------------------------------------------------- 노멀 (OpenGL: R = +X, G = +Y)
dhdx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) / (2.0 * px_mm)
dhdy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) / (2.0 * px_mm)
n = np.dstack([-dhdx, -dhdy, np.ones_like(height)])
n /= np.linalg.norm(n, axis=2, keepdims=True)

# ---------------------------------------------------------------- 색
mottle = 1.0 + 0.02 * p_flip[plank, 0] * noise(90, 90, 7) + 0.015 * noise(25, 25, 8)   # 시멘트 구름 얼룩 (세면 결을 가린다)
grain_tone = (1.0 - 0.13 * late + 0.02 * (1.0 - late) - 0.012 * np.clip(fibers, 0.0, None)) * (1.0 + 0.035 * p_flip[plank, 1] * streaks)
knot_tone = np.where(knot_core, 0.8 + 0.08 * knot_rings, 1.0)
speck = 1.0 + 0.02 * noise(0.8, 0.8, 9)
color = p_color[plank] * (p_bright[plank] * mottle * grain_tone * knot_tone * speck)[..., None]
color = color * np.where(pores, 0.7, 1.0)[..., None] * (0.9 + 0.1 * t)[..., None]
color = np.where(grout[..., None], GROUT * (0.9 + 0.08 * noise(1.5, 1.5, 10))[..., None], color)
ao = np.where(grout, 0.6, 0.85 + 0.15 * t) * (1.0 - 0.05 * late)
color = np.clip(color * (0.88 + 0.12 * ao)[..., None], 0.0, 1.0)
smooth = np.where(grout, 0.05, np.clip(0.32 + 0.05 * noise(60, 60, 11) - 0.06 * late - 0.2 * pores, 0.05, 0.5))


def save(name, rgba, non_color):
    img = bpy.data.images.new(name, SIZE, SIZE, alpha=True)
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
color_path = save("WoodCement_Color", np.dstack([color, ones]), False)
normal_path = save("WoodCement_NormalGL", np.dstack([n * 0.5 + 0.5, ones]), True)
save("WoodCement_AmbientOcclusion", np.dstack([ao, ao, ao, ones]), True)
save("WoodCement_MetallicSmoothness", np.dstack([zeros, zeros, zeros, smooth]), True)
print("GROUT share", round(float(grout.mean()), 3), "knot share", round(float(knot_core.mean()), 4))

# ---------------------------------------------------------------- 미리보기 (Cycles)
if preview_dir:
    os.makedirs(preview_dir, exist_ok=True)
    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    size_m = 6.0
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    plane = bpy.context.active_object
    plane.scale = (size_m, size_m, 1.0)
    mat = bpy.data.materials.new("WoodCement")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex_coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (size_m * 1000.0 / AREA_MM, size_m * 1000.0 / AREA_MM, 1.0)
    nt.links.new(tex_coord.outputs["UV"], mapping.inputs["Vector"])
    color_tex = nt.nodes.new("ShaderNodeTexImage")
    color_tex.image = bpy.data.images.load(color_path)
    normal_tex = nt.nodes.new("ShaderNodeTexImage")
    normal_tex.image = bpy.data.images.load(normal_path)
    normal_tex.image.colorspace_settings.name = "Non-Color"
    normal_map = nt.nodes.new("ShaderNodeNormalMap")
    for tex in (color_tex, normal_tex):
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    nt.links.new(color_tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
    nt.links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.6
    plane.data.materials.append(mat)

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 2.5
    sun_data.angle = math.radians(3.0)
    sun = bpy.data.objects.new("Sun", sun_data)
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.66, 0.66, 0.66, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9

    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.view_settings.view_transform = "Standard"

    def shoot(name, res, cam_loc, cam_rot, sun_rot, ortho=None):
        scene.render.resolution_x, scene.render.resolution_y = res
        cam.location = cam_loc
        cam.rotation_euler = cam_rot
        if ortho:
            cam_data.type = "ORTHO"
            cam_data.ortho_scale = ortho
        else:
            cam_data.type = "PERSP"
            cam_data.lens = 24
        sun.rotation_euler = sun_rot
        scene.render.filepath = os.path.join(preview_dir, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDERED", scene.render.filepath)

    # 방 안 눈높이 (1.6 m) 에서 바닥을 본다 · 창으로 든 해처럼 비스듬히
    shoot("wood_cement_eye", (1280, 720), (0.2, -2.6, 1.6), (math.radians(58), 0.0, math.radians(12)),
          (math.radians(55), 0.0, math.radians(-40)))
    # 발밑
    shoot("wood_cement_feet", (1280, 720), (0.0, -0.9, 1.6), (math.radians(28), 0.0, 0.0),
          (math.radians(55), 0.0, math.radians(-40)))
    # 바로 위에서 0.6 m — 해를 낮게 해서 결 요철이 보이게
    shoot("wood_cement_top", (1000, 1000), (0.3, 0.1, 2.0), (0.0, 0.0, 0.0),
          (math.radians(72), 0.0, math.radians(20)), ortho=0.6)
