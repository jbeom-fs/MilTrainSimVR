# Usage: blender -b --factory-startup --python make_ceramic_facade.py -- <map_dir> [preview_dir]
#   <map_dir> = Assets/C/Textures/Map — 그 아래 Ceramic1 · GraniteDark 폴더에 쓴다. preview_dir 을 주면 미리보기 렌더도 둔다
# 청운백화점(N05) 외벽 (사용자 11/06 — 목업 C 안: 아이보리 세라믹 + 청록 장식 + 금색 부조, 1층은 진회색 화강석)
#   Ceramic1: 유약 바른 세라믹 블록 60 × 30 cm · 줄눈 8 mm · 엇갈려 쌓기. 모서리는 반지름 10 mm 로 둥글고 면은 살짝 부푼다.
#             블록마다 아이보리 색이 조금씩 다르고(유약 얼룩), 면은 반짝 · 줄눈은 거칠다.
#             한 장 = 블록 6장 × 12줄 = 3.648 × 3.696 m. Unity 재질 Tiling (1/3.648, 1/3.696), 아래 끝 = 기단 위 띠 윗면(3.90 m) — 빌더가 맞춘다
#   GraniteDark: 갈아 낸 진회색 화강석 판 1.2 × 0.6 m · 줄눈 6 mm · 엇갈려 쌓기 · 모서리 3 mm 모따기. 한 장 = 2.4 × 2.4 m, 아래 끝 = 인도 높이
#   *_Color.png (sRGB) · *_NormalGL.png · *_AmbientOcclusion.png · *_MetallicSmoothness.png (R = 금속, A = 매끄러움)
import bpy, sys, os, math
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
map_dir = argv[0]
preview_dir = argv[1] if len(argv) > 1 else None
SEED = 1106


def fft_noise(shape, sigma_x, sigma_y, seed):
    # 가로 sigma_x · 세로 sigma_y px 로 흐린 흰 노이즈 (표준편차 1). FFT 라 가장자리가 이어진다
    h, w = shape
    g = np.random.default_rng(SEED * 31 + seed).standard_normal((h, w))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.rfftfreq(w)[None, :]
    filt = np.exp(-2.0 * np.pi ** 2 * ((sigma_x * fx) ** 2 + (sigma_y * fy) ** 2))
    n = np.fft.irfft2(np.fft.rfft2(g) * filt, s=(h, w))
    return n / (n.std() + 1e-12)


def normals(height_mm, px_x_mm, px_y_mm, limit):
    # 높이(mm) → OpenGL 노멀 (초록 = 위). 감싸서 미분하니 이음매가 없다
    dhdx = (np.roll(height_mm, -1, axis=1) - np.roll(height_mm, 1, axis=1)) / (2.0 * px_x_mm)
    dhdy = (np.roll(height_mm, -1, axis=0) - np.roll(height_mm, 1, axis=0)) / (2.0 * px_y_mm)
    n = np.dstack([-np.clip(dhdx, -limit, limit), -np.clip(dhdy, -limit, limit), np.ones_like(height_mm)])
    return n / np.linalg.norm(n, axis=2, keepdims=True)


def blur(a, sigma_px):
    h, w = a.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.rfftfreq(w)[None, :]
    filt = np.exp(-2.0 * np.pi ** 2 * (sigma_px ** 2) * (fx ** 2 + fy ** 2))
    return np.fft.irfft2(np.fft.rfft2(a) * filt, s=(h, w))


def save(folder, name, rgba, non_color):
    os.makedirs(folder, exist_ok=True)
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    path = os.path.join(folder, name + ".png")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    print("WROTE", path, os.path.getsize(path), flush=True)
    return path


def bond(x, y, w, h, joint):
    # 엇갈려 쌓기: 줄 번호 · 칸 번호 · 블록 안 자리(u, v) · 줄눈 여부
    pitch_x, pitch_y = w + joint, h + joint
    row = np.floor(y / pitch_y).astype(np.int64)
    off = (row % 2) * pitch_x * 0.5
    col = np.floor((x - off) / pitch_x).astype(np.int64)
    u = x - off - col * pitch_x
    v = y - row * pitch_y
    return row, col, u, v


# ---------------------------------------------------------------- 세라믹 블록 (아이보리)
def ceramic():
    W_M, H_M, SIZE = 6 * 0.608, 12 * 0.308, 2048
    BW, BH, JOINT = 0.6, 0.3, 0.008
    R_MM, JOINT_DEPTH_MM, PILLOW_MM = 10.0, 12.0, 0.9
    mx, my = W_M / SIZE, H_M / SIZE
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    x, y = (xx + 0.5) * mx, (yy + 0.5) * my
    row, col, u, v = bond(x, y, BW, BH, JOINT)
    in_block = (u < BW) & (v < BH)
    # 블록 번호 — 한 장 안에서 칸 수로 감싸서 이음매 건너 같은 블록이 같은 색
    cols_per_row = 6
    key = (row % 12) * 64 + (col % cols_per_row)
    rng = np.random.default_rng(SEED)
    tint = rng.random(12 * 64)[key]
    hue = rng.normal(0.0, 1.0, 12 * 64)[key]

    # 높이 (mm): 줄눈 -12, 모서리 반지름 10 mm 둥글게, 면은 가운데가 0.9 mm 부푼다
    e = np.minimum(np.minimum(u, BW - u), np.minimum(v, BH - v)) * 1000.0
    edge = np.clip(e / R_MM, 0.0, 1.0)
    rounding = np.sqrt(np.clip(1.0 - (1.0 - edge) ** 2, 0.0, 1.0))
    su, sv = np.clip(u / BW, 0, 1) * 2 - 1, np.clip(v / BH, 0, 1) * 2 - 1
    pillow = PILLOW_MM * (1 - su ** 2) * (1 - sv ** 2)
    glaze_wave = 0.12 * fft_noise((SIZE, SIZE), 18, 18, 1)
    height = np.where(in_block, -JOINT_DEPTH_MM * (1.0 - rounding) + pillow + glaze_wave, -JOINT_DEPTH_MM)
    nrm = normals(height, mx * 1000.0, my * 1000.0, 2.5)

    # 그늘: 줄눈은 어둡고 둥근 모서리로 갈수록 밝아진다 + 줄눈 가까운 면도 살짝
    depth01 = np.clip((height + JOINT_DEPTH_MM) / JOINT_DEPTH_MM, 0.0, 1.0)
    ao = 0.5 + 0.5 * depth01 ** 0.6
    ao = np.minimum(ao, 0.82 + 0.18 * np.clip(blur(depth01, 6.0), 0, 1))

    # 색: 아이보리 유약 — 블록마다 밝기 ±, 따뜻하게·차갑게 조금, 블록 안 유약 얼룩. 줄눈은 회백색 몰탈
    ivory = np.array([232, 216, 178]) / 255.0
    bright = 0.93 + 0.11 * tint
    warm = 0.010 * hue
    mottle = 0.012 * fft_noise((SIZE, SIZE), 40, 30, 2) + 0.008 * fft_noise((SIZE, SIZE), 4, 4, 3)
    block_rgb = ivory[None, None, :] * (bright + mottle)[..., None]
    block_rgb = block_rgb + np.dstack([warm, warm * 0.4, -warm])
    # 둥근 모서리는 유약이 얇아 살짝 진하다
    block_rgb = block_rgb * (0.94 + 0.06 * rounding)[..., None]
    mortar = np.array([206, 201, 190]) / 255.0 * (0.95 + 0.05 * fft_noise((SIZE, SIZE), 3, 3, 4))[..., None]
    color = np.where(in_block[..., None], block_rgb, mortar * 0.92)
    color = color * (0.86 + 0.14 * ao)[..., None]

    smooth = np.where(in_block, 0.80 + 0.03 * fft_noise((SIZE, SIZE), 25, 25, 5), 0.12)
    folder = os.path.join(map_dir, "Ceramic1")
    ones, zeros = np.ones_like(height), np.zeros_like(height)
    paths = {
        "color": save(folder, "Ceramic1_Color", np.dstack([color, ones]), False),
        "normal": save(folder, "Ceramic1_NormalGL", np.dstack([nrm * 0.5 + 0.5, ones]), True),
    }
    save(folder, "Ceramic1_AmbientOcclusion", np.dstack([ao, ao, ao, ones]), True)
    save(folder, "Ceramic1_MetallicSmoothness", np.dstack([zeros, zeros, zeros, smooth]), True)
    return paths, (W_M, H_M)


# ---------------------------------------------------------------- 진회색 화강석 판
def granite():
    W_M, H_M, SIZE = 2.4, 2.4, 2048
    BW, BH, JOINT = 1.2 - 0.006, 0.6 - 0.006, 0.006
    CHAMFER_MM, JOINT_DEPTH_MM = 3.0, 4.0
    mx, my = W_M / SIZE, H_M / SIZE
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    x, y = (xx + 0.5) * mx, (yy + 0.5) * my
    row, col, u, v = bond(x, y, BW, BH, JOINT)
    in_panel = (u < BW) & (v < BH)
    key = (row % 4) * 8 + (col % 2)
    tone = np.random.default_rng(SEED + 1).normal(0.0, 1.0, 64)[key]

    e = np.minimum(np.minimum(u, BW - u), np.minimum(v, BH - v)) * 1000.0
    chamfer = np.clip(e / CHAMFER_MM, 0.0, 1.0)
    height = np.where(in_panel, -CHAMFER_MM * (1.0 - chamfer), -CHAMFER_MM - JOINT_DEPTH_MM)
    nrm = normals(height, mx * 1000.0, my * 1000.0, 1.5)

    # 알갱이: 잔 노이즈를 문턱으로 잘라 검은 알 · 흰 알, 그 위에 큰 얼룩
    grain_dark = fft_noise((SIZE, SIZE), 1.2, 1.2, 11)
    grain_light = fft_noise((SIZE, SIZE), 1.0, 1.0, 12)
    cloud = fft_noise((SIZE, SIZE), 60, 60, 13)
    base = np.full((SIZE, SIZE), 0.205) * (1.0 + 0.04 * tone + 0.05 * cloud)
    base = np.where(grain_dark > 1.1, base * 0.45, base)
    base = np.where(grain_light > 1.9, 0.55 + 0.08 * grain_light, base)
    base = np.where(np.abs(fft_noise((SIZE, SIZE), 2.5, 2.5, 14)) < 0.08, base * 1.35, base)
    color = np.dstack([base * 0.99, base * 0.99, base * 1.02])
    color = np.where(in_panel[..., None], color, np.array([0.30, 0.30, 0.31]))
    ao = np.where(in_panel, 0.9 + 0.1 * chamfer, 0.6)
    smooth = np.where(in_panel, 0.82 * (0.9 + 0.1 * chamfer), 0.15)
    folder = os.path.join(map_dir, "GraniteDark")
    ones, zeros = np.ones_like(height), np.zeros_like(height)
    paths = {
        "color": save(folder, "GraniteDark_Color", np.dstack([color, ones]), False),
        "normal": save(folder, "GraniteDark_NormalGL", np.dstack([nrm * 0.5 + 0.5, ones]), True),
    }
    save(folder, "GraniteDark_AmbientOcclusion", np.dstack([ao, ao, ao, ones]), True)
    save(folder, "GraniteDark_MetallicSmoothness", np.dstack([zeros, zeros, zeros, smooth]), True)
    return paths, (W_M, H_M)


cer_paths, cer_size = ceramic()
gra_paths, gra_size = granite()

# ---------------------------------------------------------------- 미리보기 — 화강석 기단 위에 세라믹 벽 (Cycles)
if preview_dir:
    os.makedirs(preview_dir, exist_ok=True)
    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)

    def panel(name, paths, size, x0, x1, z0, z1, rough_face):
        bpy.ops.mesh.primitive_plane_add(size=1.0)
        p = bpy.context.active_object
        p.name = name
        p.scale = (x1 - x0, z1 - z0, 1.0)
        p.rotation_euler = (math.radians(90), 0.0, 0.0)
        p.location = ((x0 + x1) / 2, 0.0, (z0 + z1) / 2)
        for loop in p.data.uv_layers.active.data:
            loop.uv = (loop.uv[0] * (x1 - x0) / size[0], loop.uv[1] * (z1 - z0) / size[1])
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        nt = mat.node_tree
        bsdf = nt.nodes["Principled BSDF"]
        ct = nt.nodes.new("ShaderNodeTexImage")
        ct.image = bpy.data.images.load(paths["color"])
        nt_ = nt.nodes.new("ShaderNodeTexImage")
        nt_.image = bpy.data.images.load(paths["normal"])
        nt_.image.colorspace_settings.name = "Non-Color"
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(ct.outputs["Color"], bsdf.inputs["Base Color"])
        nt.links.new(nt_.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        bsdf.inputs["Roughness"].default_value = rough_face
        p.data.materials.append(mat)

    panel("Granite", gra_paths, gra_size, 0.0, 6.0, 0.0, 3.4, 0.2)
    panel("Ceramic", cer_paths, cer_size, 0.0, 6.0, 3.4, 9.0, 0.22)
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.2
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = (math.radians(55), 0.0, math.radians(-40))
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.65, 0.8, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    for name, loc, rot, lens in (("ceramic_preview", (1.2, -2.6, 4.4), (math.radians(84), 0, math.radians(-22)), 30),
                                 ("facade_preview", (3.0, -10.5, 3.6), (math.radians(88), 0, 0), 30)):
        cam_data = bpy.data.cameras.new(name)
        cam_data.lens = lens
        cam = bpy.data.objects.new(name, cam_data)
        cam.location = loc
        cam.rotation_euler = rot
        scene.collection.objects.link(cam)
        scene.camera = cam
        scene.render.resolution_x, scene.render.resolution_y = 1280, 900
        scene.render.filepath = os.path.join(preview_dir, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDERED", scene.render.filepath, flush=True)
