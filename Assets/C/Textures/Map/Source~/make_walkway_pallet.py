# Usage: blender -b --factory-startup --python make_walkway_pallet.py -- <map_dir> [preview_dir]
#   <map_dir> = Assets/C/Textures/Map — 그 아래 Walkway1 폴더에 쓴다
# 청운백화점(N05) 경사형 무빙워크 판 (사용자 11/06 — 계단형 에스컬레이터 대신 '레일형'). 무료 텍스처 사이트(ambientCG)에 에스컬레이터 ·
#   무빙워크 바닥이 없어 실제 판 치수로 직접 만든다:
#   · 알루미늄 판(팔레트) 길이 0.4 m — 판 사이 이음매 4 mm
#   · 진행 방향으로 홈 — 8.5 mm 간격, 홈 폭 6 mm · 깊이 6 mm (홈 사이 돌기 2.5 mm 가 '레일'처럼 줄지어 보인다), 판 끝 돌기는 둥글게
#   · 돌기 윗면은 닳아 반짝 · 홈 안은 어둡고 먼지
#   한 장 = 진행 방향 0.8 m (판 둘) × 가로 0.408 m (돌기 48줄). 그림 가로(U) = 진행 방향
#   Walkway1_Color.png (sRGB) · _NormalGL.png · _AmbientOcclusion.png · _MetallicSmoothness.png (R = 금속, A = 매끄러움)
import bpy, sys, os, math
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
map_dir = argv[0]
preview_dir = argv[1] if len(argv) > 1 else None
SEED = 1106
U_M, V_M = 0.8, 0.408
W_PX, H_PX = 2048, 1024
PALLET = 0.4
JOINT = 0.004
PITCH, CLEAT = 0.0085, 0.0025
DEPTH_MM = 6.0


def fft_noise(sigma_x, sigma_y, seed):
    g = np.random.default_rng(SEED * 31 + seed).standard_normal((H_PX, W_PX))
    fy = np.fft.fftfreq(H_PX)[:, None]
    fx = np.fft.rfftfreq(W_PX)[None, :]
    filt = np.exp(-2.0 * np.pi ** 2 * ((sigma_x * fx) ** 2 + (sigma_y * fy) ** 2))
    n = np.fft.irfft2(np.fft.rfft2(g) * filt, s=(H_PX, W_PX))
    return n / (n.std() + 1e-12)


mx, my = U_M / W_PX, V_M / H_PX
yy, xx = np.mgrid[0:H_PX, 0:W_PX].astype(np.float64)
u, v = (xx + 0.5) * mx, (yy + 0.5) * my
pu = np.mod(u, PALLET)                      # 판 안에서 진행 방향 자리
pv = np.mod(v, PITCH)                       # 돌기 한 칸 안에서 가로 자리
in_joint = pu < JOINT
# 돌기: 가운데 CLEAT 폭이 위, 양옆으로 0.6 mm 둥글게 내려간다
cleat_c = np.abs(pv - PITCH / 2)
edge = np.clip((CLEAT / 2 + 0.0006 - cleat_c) / 0.0006, 0.0, 1.0)
cleat = np.where(cleat_c < CLEAT / 2, 1.0, edge)
# 판 끝(이음매 쪽 8 mm)에서 돌기 끝이 둥글게 내려간다
end = np.minimum(pu - JOINT, PALLET - pu)
tip = np.clip(end / 0.008, 0.0, 1.0)
tip = np.sqrt(np.clip(1 - (1 - tip) ** 2, 0, 1))
height = np.where(in_joint, -DEPTH_MM - 2.0, -DEPTH_MM * (1.0 - cleat * tip))

dhdx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) / (2.0 * mx * 1000.0)
dhdy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) / (2.0 * my * 1000.0)
nrm = np.dstack([-np.clip(dhdx, -3, 3), -np.clip(dhdy, -3, 3), np.ones_like(height)])
nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)

top = np.clip((height + DEPTH_MM) / DEPTH_MM, 0.0, 1.0)            # 1 = 돌기 윗면, 0 = 홈 바닥
ao = 0.35 + 0.65 * top ** 0.7
ao = np.where(in_joint, 0.25, ao)

wear = 0.04 * fft_noise(40, 10, 1)                                  # 진행 방향으로 길게 닳은 자국
dirt = np.clip(fft_noise(6, 6, 2), 0, None) * 0.03
alu = np.array([0.60, 0.61, 0.62])
color = alu[None, None, :] * (0.55 + 0.45 * top + wear)[..., None] - dirt[..., None] * (1 - top)[..., None]
color = np.where(in_joint[..., None], np.array([0.08, 0.08, 0.085]), color)
color = np.clip(color, 0, 1)
metal = np.where(in_joint, 0.3, 0.6 + 0.4 * top)
smooth = np.where(in_joint, 0.1, 0.25 + 0.4 * top + 0.05 * fft_noise(30, 8, 3))


def save(name, rgba, non_color):
    folder = os.path.join(map_dir, "Walkway1")
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


ones, zeros = np.ones_like(height), np.zeros_like(height)
color_path = save("Walkway1_Color", np.dstack([color, ones]), False)
normal_path = save("Walkway1_NormalGL", np.dstack([nrm * 0.5 + 0.5, ones]), True)
save("Walkway1_AmbientOcclusion", np.dstack([ao, ao, ao, ones]), True)
save("Walkway1_MetallicSmoothness", np.dstack([metal, zeros, zeros, np.clip(smooth, 0, 1)]), True)

if preview_dir:
    os.makedirs(preview_dir, exist_ok=True)
    scene = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    bpy.ops.mesh.primitive_plane_add(size=1.0)
    p = bpy.context.active_object
    p.scale = (2.4, 1.2, 1.0)
    for loop in p.data.uv_layers.active.data:
        loop.uv = (loop.uv[0] * 2.4 / U_M, loop.uv[1] * 1.2 / V_M)
    mat = bpy.data.materials.new("Pallet")
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
    bsdf.inputs["Metallic"].default_value = 0.8
    bsdf.inputs["Roughness"].default_value = 0.45
    p.data.materials.append(mat)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(45), 0, math.radians(30))
    scene.collection.objects.link(sun)
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.62, 0.66, 1.0)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 40
    cam.location = (-1.4, -0.1, 0.55)
    cam.rotation_euler = (math.radians(62), 0, math.radians(-90))
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 800
    scene.render.filepath = os.path.join(preview_dir, "walkway_preview.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath, flush=True)
