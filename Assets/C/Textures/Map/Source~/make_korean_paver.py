# Usage: blender -b --factory-startup --python make_korean_paver.py -- <out_dir> [preview_dir]
#   보통 <out_dir> = Assets/C/Textures/Map/Paver. preview_dir 을 주면 거기에 미리보기 그림을 그린다 (Unity 폴더 밖으로 — 가져오지 않게)
# 한국식 보도블록 텍스처를 만든다 (11/03) — 200 × 100 mm 직사각형 블록을 90° 헤링본으로 깐 2 m × 2 m 한 장, 이음새 없이 반복된다.
# Unity 재질에서 Tiling 0.5 (맵 UV 는 1 = 1 m). 색 · 무늬 · 크기는 아래 숫자를 고쳐 다시 돌린다
#   Paver_Color.png (sRGB) · Paver_Normal.png (OpenGL, Unity 와 같음) · Paver_MetallicSmoothness.png (A = 매끄러움) · Paver_AO.png
#   블록마다 색 · 밝기가 조금씩 다르고(회색 4가지 + 따뜻한 회색), 모서리는 둥글게 깎이고 군데군데 이가 빠졌다.
#   줄눈은 모래, 겉면은 골재 알갱이 · 기공, 넓게 얼룩. 노이즈는 FFT 로 만들어 가장자리가 저절로 이어진다
# preview_dir 를 주면 Cycles 로 눈높이 · 발밑 · 위에서 세 장을 그린다
import bpy, sys, os, math
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
out_dir = argv[0]
preview_dir = argv[1] if len(argv) > 1 else None
os.makedirs(out_dir, exist_ok=True)

SIZE = 2048            # px
AREA_MM = 2000.0       # 한 장이 덮는 크기 (2 m — Unity 에서 Tiling 0.5)
UNIT_MM = 100.0        # 블록 200 × 100 mm = 2 × 1 칸
JOINT_MM = 2.0         # 줄눈 반폭
BEVEL_MM = 5.0         # 모서리 둥글게 깎인 폭
BEVEL_DEPTH_MM = 4.5
SAND_DEPTH_MM = 6.0    # 줄눈 모래가 블록 윗면보다 낮은 정도
SEED = 1103

# 회색 보도블록 색 (sRGB) · 비율 — 한 블록 안은 한 색. 처음엔 블록끼리 차이가 커서 체크무늬처럼 보였다
PALETTE = np.array([
    [0.600, 0.600, 0.585],   # 보통 회색
    [0.650, 0.645, 0.625],   # 밝은 회색
    [0.555, 0.555, 0.545],   # 어두운 회색
    [0.500, 0.500, 0.500],   # 숯색 (드물게)
    [0.620, 0.605, 0.580],   # 따뜻한 회색
])
PALETTE_WEIGHTS = np.array([0.40, 0.22, 0.24, 0.06, 0.08])
SAND = np.array([0.42, 0.39, 0.34])

px_mm = AREA_MM / SIZE
units = int(round(AREA_MM / UNIT_MM))
assert units % 4 == 0, "헤링본은 4칸마다 되풀이 — 한 장이 4칸의 배수여야 이어진다"
rng = np.random.default_rng(SEED)


def noise(sigma_px, seed):
    # 가우스로 흐린 흰 노이즈 (표준편차 1). FFT 라 가장자리가 이어진다
    g = np.random.default_rng(SEED * 31 + seed).standard_normal((SIZE, SIZE))
    fy = np.fft.fftfreq(SIZE)[:, None]
    fx = np.fft.rfftfreq(SIZE)[None, :]
    filt = np.exp(-2.0 * (np.pi * sigma_px) ** 2 * (fx ** 2 + fy ** 2))
    n = np.fft.irfft2(np.fft.rfft2(g) * filt, s=(SIZE, SIZE))
    return n / (n.std() + 1e-12)


# ---------------------------------------------------------------- 블록 배치 (90° 헤링본, 칸 단위)
# 칸 (cx, cy) 의 r = (cx − cy) mod 4: 0 · 1 = 가로 블록의 왼쪽 · 오른쪽 칸, 2 · 3 = 세로 블록의 위 · 아래 칸.
# 가로 블록 하나와 세로 블록 하나가 (1, 1) 씩 비켜 계단을 이루고, (4, 0) · (0, 4) 마다 되풀이된다
yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
fx = (xx + 0.5) * px_mm / UNIT_MM
fy = (yy + 0.5) * px_mm / UNIT_MM
cx = np.floor(fx).astype(np.int64)
cy = np.floor(fy).astype(np.int64)
r = np.mod(cx - cy, 4)
horiz = r < 2
x0 = np.where(r == 1, cx - 1, cx)
y0 = np.where(r == 2, cy - 1, cy)
w = np.where(horiz, 2.0, 1.0)
h = np.where(horiz, 1.0, 2.0)
lx = fx - x0
ly = fy - y0
ex = np.minimum(lx, w - lx) * UNIT_MM      # 가까운 세로 · 가로 모서리까지 (mm)
ey = np.minimum(ly, h - ly) * UNIT_MM
d = np.minimum(ex, ey)
key = (np.mod(x0, units) * units + np.mod(y0, units)) * 2 + horiz.astype(np.int64)   # 블록 번호 — 한 장 너머에서도 같다
nkeys = units * units * 2

# 블록마다 정하는 값
brick_color = PALETTE[rng.choice(len(PALETTE), size=nkeys, p=PALETTE_WEIGHTS / PALETTE_WEIGHTS.sum())]
brick_bright = rng.uniform(0.96, 1.04, nkeys)
brick_tilt = rng.uniform(-0.004, 0.004, (nkeys, 2))        # 블록이 아주 조금 기울어 깔린 정도 (기울기)
brick_chip = rng.random(nkeys) < 0.35                      # 모서리 이가 빠진 블록
brick_chip_r = rng.uniform(7.0, 16.0, nkeys)               # 빠진 크기 (mm)

# ---------------------------------------------------------------- 높이 (mm, 블록 윗면 = 0)
wear = noise(40, 1)
jw = JOINT_MM + 0.5 * wear                                  # 줄눈 폭이 고르지 않게
bev = np.clip(BEVEL_MM + 1.8 * noise(25, 2), 2.0, 9.0)
t = np.clip((d - jw) / bev, 0.0, 1.0)                       # 0 = 모서리 끝, 1 = 평평한 윗면
joint = d < jw
top = -BEVEL_DEPTH_MM * (1.0 - t) ** 2
tilt = (brick_tilt[key, 0] * (lx - w / 2) + brick_tilt[key, 1] * (ly - h / 2)) * UNIT_MM
grain = 0.12 * noise(1.0, 3) + 0.3 * noise(4.0, 4)
pit_noise = noise(1.2, 5)
pits = pit_noise > 2.6
surface = top + (grain - 0.7 * pits) * t + tilt
# 모서리 이 빠짐: 그 블록의 네 귀퉁이에서 반지름 안을 깎는다 (가장자리는 노이즈로 들쭉날쭉)
corner = np.hypot(ex, ey)
chip_r = brick_chip_r[key] * (1.0 + 0.35 * noise(3.0, 6))
chipped = brick_chip[key] & (corner < chip_r) & ~joint
surface = np.where(chipped, np.minimum(surface, -2.5 + 0.4 * noise(1.5, 7)), surface)
sand = -SAND_DEPTH_MM + 0.8 * noise(1.5, 8)
height = np.where(joint, sand, surface)

# ---------------------------------------------------------------- 노멀 (OpenGL: R = +X, G = +Y)
dhdx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) / (2.0 * px_mm)
dhdy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) / (2.0 * px_mm)
n = np.dstack([-dhdx, -dhdy, np.ones_like(height)])
n /= np.linalg.norm(n, axis=2, keepdims=True)

# ---------------------------------------------------------------- 색
col = brick_color[key] * brick_bright[key][..., None]
col = col * (1.0 + 0.035 * noise(1.0, 9) + 0.04 * noise(2.5, 15))[..., None]   # 골재 알갱이 (잔 것 + 굵은 것)
grain_pick = noise(0.8, 10)
col = col + np.where(grain_pick > 2.2, 0.07, 0.0)[..., None] - np.where(grain_pick < -2.3, 0.09, 0.0)[..., None]
dirt = 1.0 - 0.10 * np.clip(noise(180, 11), 0, None) - 0.05 * np.clip(noise(60, 12), 0, None)   # 넓은 얼룩
col = col * dirt[..., None] * (0.86 + 0.14 * t)[..., None]                 # 모서리에 낀 때
col = col * np.where(pits | chipped, 0.8, 1.0)[..., None]
sand_col = SAND * (0.8 + 0.12 * noise(1.5, 13))[..., None]
col = np.where(joint[..., None], sand_col, col)
ao = np.where(joint, 0.55, 0.8 + 0.2 * t) * np.where(chipped, 0.85, 1.0)
col = np.clip(col * (0.85 + 0.15 * ao)[..., None], 0.0, 1.0)
smooth = np.where(joint, 0.03, np.clip(0.14 + 0.04 * noise(30, 14), 0.05, 0.25))


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
color_path = save("Paver_Color", np.dstack([col, ones]), False)
normal_path = save("Paver_Normal", np.dstack([n * 0.5 + 0.5, ones]), True)
save("Paver_MetallicSmoothness", np.dstack([zeros, zeros, zeros, smooth]), True)
save("Paver_AO", np.dstack([ao, ao, ao, ones]), True)
print("JOINT share", round(float(joint.mean()), 3), "chipped share", round(float(chipped.mean()), 4))

# ---------------------------------------------------------------- 미리보기 (Cycles)
if preview_dir:
    os.makedirs(preview_dir, exist_ok=True)
    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    width_m, length_m = 2.5, 9.0
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    plane = bpy.context.active_object
    plane.scale = (width_m, length_m, 1.0)
    mat = bpy.data.materials.new("Paver")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex_coord = nt.nodes.new("ShaderNodeTexCoord")
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (width_m * 1000.0 / AREA_MM, length_m * 1000.0 / AREA_MM, 1.0)
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
    bsdf.inputs["Roughness"].default_value = 0.85
    plane.data.materials.append(mat)

    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.5
    sun_data.angle = math.radians(1.5)
    sun = bpy.data.objects.new("Sun", sun_data)
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.63, 0.65, 0.69, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8

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

    # 눈높이 (1.65 m) 에서 보도를 따라 앞을 본다 · 해는 오른쪽 앞 낮게
    shoot("preview_eye", (1280, 720), (0.35, -4.2, 1.65), (math.radians(66), 0.0, math.radians(-4)),
          (math.radians(58), 0.0, math.radians(35)))
    # 발밑 — VR 에서 서서 내려다볼 때
    shoot("preview_feet", (1280, 720), (0.0, -1.0, 1.65), (math.radians(30), 0.0, 0.0),
          (math.radians(58), 0.0, math.radians(35)))
    # 바로 위에서 1 m × 1 m — 해를 낮게 해서 모서리 · 줄눈 요철이 보이게
    shoot("preview_top", (1000, 1000), (0.0, 0.0, 2.0), (0.0, 0.0, 0.0),
          (math.radians(68), 0.0, math.radians(30)), ortho=1.0)
