# Usage: blender -b --factory-startup --python make_exterior_textures.py -- <map_dir>
#   <map_dir> = Assets/C/Textures/Map — 폴더마다 <이름>_Color.jpg · _NormalGL.jpg (때 자국 · 큰 얼룩은 png)
# 일반 건물 바깥벽 마감 (사용자 11/06 — 목업 2차 '반복이 보여 옛날 게임 같다' 1~3단계를 보고 적용). 실제 치수로 줄눈 · 높낮이를 그린다:
#   TileBrown · TileBeige : 2구 외장 타일 227 × 60 mm, 줄눈 8 mm, 반장 엇갈림 (한 장 1.88 × 1.904 m)
#   TileGrey              : 회색 외장 타일 295 × 145 mm, 줄눈 5 mm, 줄 맞춤 (2.4 m)
#   Brick1                : 붉은 치장 벽돌 190 × 57 mm, 줄눈 10 mm (2.0 × 2.01 m)
#     타일 · 벽돌은 '너무 완벽한 타일'이 안 되게: 줄눈 폭 ± 0.6 mm · 줄마다 가로 어긋남 ± 3 mm · 모서리 깨짐 · 생산 묶음마다 다른 가로 띠 색 ·
#     장마다 미세한 기울기(빛을 조금씩 다르게 받는다). 반복을 드러내는 진한 장은 넣지 않는다 (벽돌만 5 % 를 살짝 진하게).
#     유약 타일은 모서리 깨짐을 벽돌의 1/3 쯤 — 벽돌만큼 깨지면 타일이 벽돌처럼 보인다
#   GraniteLight          : 밝은 회색 화강석(포천석) 판 1200 × 600 mm, 줄눈 5 mm (2.4 m)
#   Panel1                : 알루미늄 복합 패널 1200 × 1700 mm(한 층 두 장), 줄눈 12 mm — 색은 재질에서 곱한다 (2.4 × 3.4 m)
#   Stucco1               : 고운 모래 결 미장 — 눈에 띄는 무늬가 없어 반복이 안 보인다. 색은 재질에서 곱한다 (2.4 m)
#   ExposedConcrete       : 노출 콘크리트 — Wall C(Concrete008)에서 30 cm 넘는 얼룩만 덜어낸 색 + Wall C 노멀 · 그늘 (3.6 m)
#   RoofConcrete          : 옥상 — Wall B(Concrete020)에서 큰 얼룩을 덜어낸 색 + Wall B 노멀 (3 m)
#   Macro1_Detail.png     : 큰 얼룩층 — 한 장 30 m 의 밝기 · 색 얼룩, 평균 0.5 (URP Lit Detail 맵 × 2 = 1). 선형(sRGB 끔)
#   Grime1_Color.png      : 때 자국 아틀라스 (알파 블렌드: rgb = 때 색, a = 세기). 세 띠: 아래 1층 흙탕 때 · 가운데 빗물 줄 · 위 옥상 밑 줄
import bpy, sys, os, shutil
import numpy as np

map_dir = sys.argv[sys.argv.index("--") + 1:][0]
SEED = 1106
PX = 2048


def fft_noise(h, w, sx, sy, seed):
    g = np.random.default_rng(SEED * 31 + seed).standard_normal((h, w))
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.rfftfreq(w)[None, :]
    filt = np.exp(-2.0 * np.pi ** 2 * ((sx * fx) ** 2 + (sy * fy) ** 2))
    n = np.fft.irfft2(np.fft.rfft2(g) * filt, s=(h, w))
    return n / (n.std() + 1e-12)


def blur(img, sigma):
    h, w = img.shape[:2]
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.rfftfreq(w)[None, :]
    filt = np.exp(-2.0 * np.pi ** 2 * (sigma ** 2) * (fx ** 2 + fy ** 2))
    out = np.empty_like(img)
    for c in range(img.shape[2]):
        out[..., c] = np.fft.irfft2(np.fft.rfft2(img[..., c]) * filt, s=(h, w))
    return out


def save(folder, name, rgba, non_color, fmt="JPEG"):
    out = os.path.join(map_dir, folder)
    os.makedirs(out, exist_ok=True)
    h, w = rgba.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=fmt == "PNG")
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    path = os.path.join(out, name + (".png" if fmt == "PNG" else ".jpg"))
    img.filepath_raw = path
    img.file_format = fmt
    if fmt == "JPEG":
        img.save(filepath=path, quality=95)
    else:
        img.save()
    print("WROTE", path, os.path.getsize(path), flush=True)


def save_pair(folder, rgb, height_mm, mx, my):
    h, w = height_mm.shape
    dhdx = (np.roll(height_mm, -1, axis=1) - np.roll(height_mm, 1, axis=1)) / (2 * mx * 1000)
    dhdy = (np.roll(height_mm, -1, axis=0) - np.roll(height_mm, 1, axis=0)) / (2 * my * 1000)
    nrm = np.dstack([-np.clip(dhdx, -3, 3), -np.clip(dhdy, -3, 3), np.ones_like(height_mm)])
    nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    ones = np.ones((h, w))
    save(folder, folder + "_Color", np.dstack([np.clip(rgb, 0, 1), ones]), False)
    save(folder, folder + "_NormalGL", np.dstack([nrm * 0.5 + 0.5, ones]), True)


def grid(w_m, h_m):
    h_px = int(round(PX * h_m / w_m))
    mx, my = w_m / PX, h_m / h_px
    yy, xx = np.mgrid[0:h_px, 0:PX].astype(np.float64)
    return (xx + 0.5) * mx, (yy + 0.5) * my, mx, my, h_px


def masonry(folder, tile_w, tile_h, joint, cols, rows, running, base, var, hue, mortar, recess, bevel, rough,
            dark_frac, dark_mul, seed, joint_jit=0.0012, row_jit=0.003, chip=0.00035, batch=0.035, tilt=0.004):
    mod_w, mod_h = tile_w + joint, tile_h + joint
    w_m, h_m = cols * mod_w, rows * mod_h
    x, y, mx, my, h_px = grid(w_m, h_m)
    rng = np.random.default_rng(SEED + seed)
    row = np.floor(y / mod_h).astype(int) % rows
    row_off = (rng.random(rows) - 0.5) * 2 * row_jit
    xs = x + ((row % 2) * mod_w * 0.5 if running else 0.0) + row_off[row]
    col = np.floor(xs / mod_w).astype(int) % cols
    u, v = np.mod(xs, mod_w), np.mod(y, mod_h)
    r = rng.random((rows, cols, 8))[row, col]
    # 장마다 줄눈 폭을 조금씩 (양옆 · 위아래 끝을 ± joint_jit / 2)
    left = joint / 2 + (r[..., 3] - 0.5) * joint_jit
    right = joint / 2 + tile_w + (r[..., 4] - 0.5) * joint_jit
    bottom = joint / 2 + (r[..., 5] - 0.5) * joint_jit * 0.6
    top = joint / 2 + tile_h + (r[..., 6] - 0.5) * joint_jit * 0.6
    d_edge = np.minimum(np.minimum(u - left, right - u), np.minimum(v - bottom, top - v))
    d_edge = d_edge + chip * fft_noise(h_px, PX, 1.6, 1.6, seed + 5)          # 모서리가 고르지 않게 (깨짐)
    in_tile = d_edge > 0
    edge = np.clip(d_edge / bevel, 0, 1)
    surf = fft_noise(h_px, PX, 1.5, 1.5, seed + 1)
    blot = fft_noise(h_px, PX, 12, 12, seed + 2)
    # 생산 묶음: 줄 몇 개씩 묶여 색이 아주 조금 다르다 (가로 띠)
    band_rows = 9
    band = (row // band_rows) % max(1, rows // band_rows)
    band_tone = 1 + batch * (rng.random(max(1, rows // band_rows))[band] - 0.5) * 2
    tone = (1 + var * (r[..., 0] - 0.5) * 2 + 0.02 * blot) * band_tone
    tint = np.dstack([1 + hue * (r[..., 1] - 0.5), np.ones_like(x), 1 - hue * (r[..., 1] - 0.5)])
    tile_rgb = np.array(base)[None, None, :] * tone[..., None] * tint
    tile_rgb = np.where((r[..., 2] < dark_frac)[..., None], tile_rgb * dark_mul, tile_rgb)
    tile_rgb *= (1 + 0.035 * surf)[..., None]
    tile_rgb *= (0.86 + 0.14 * edge)[..., None]
    mortar_rgb = np.array(mortar)[None, None, :] * (1 + 0.06 * fft_noise(h_px, PX, 2, 2, seed + 3))[..., None]
    rgb = np.where(in_tile[..., None], tile_rgb, mortar_rgb * 0.92)
    # 장마다 미세한 기울기 (튀어나옴 · 기울어짐 0.5 mm 안팎)
    cu, cv = (left + right) / 2, (bottom + top) / 2
    tilt_mm = ((u - cu) * (r[..., 7] - 0.5) * 2 * tilt + (v - cv) * (r[..., 0] - 0.5) * 2 * tilt) * 1000
    height = np.where(in_tile, -recess * (1 - np.sqrt(edge)) * 0.4 + rough * surf * 0.15 + tilt_mm, -recess + 0.3 * surf)
    save_pair(folder, rgb, height, mx, my)
    return w_m, h_m


def granite(folder, pw, ph, joint, cols, rows, base, seed):
    w_m, h_m = cols * pw, rows * ph
    x, y, mx, my, h_px = grid(w_m, h_m)
    pr = np.floor(y / ph).astype(int) % rows
    pc = np.floor(x / pw).astype(int) % cols
    u, v = np.mod(x, pw), np.mod(y, ph)
    in_joint = (u < joint) | (v < joint)
    panel_tone = 1 + 0.035 * (np.random.default_rng(SEED + seed).random((rows, cols))[pr, pc] - 0.5) * 2
    low = 0.03 * fft_noise(h_px, PX, 60, 60, seed + 1)
    rgb = np.array(base)[None, None, :] * (panel_tone * (1 + low))[..., None]
    mid = fft_noise(h_px, PX, 2.2, 2.2, seed + 2)
    rgb = np.where((mid > 1.1)[..., None], rgb * 0.78, rgb)
    dark = fft_noise(h_px, PX, 1.1, 1.1, seed + 3)
    rgb = np.where((dark > 1.75)[..., None], np.array([0.16, 0.16, 0.17]), rgb)
    white = fft_noise(h_px, PX, 1.0, 1.0, seed + 4)
    rgb = np.where((white > 1.9)[..., None], np.array([0.92, 0.92, 0.9]), rgb)
    rgb = np.where(in_joint[..., None], np.array([0.25, 0.25, 0.26]), rgb)
    height = np.where(in_joint, -4.0, 0.05 * mid)
    save_pair(folder, rgb, height, mx, my)
    return w_m, h_m


def panels(folder, pw, ph, joint, cols, rows, base, seed):
    w_m, h_m = cols * pw, rows * ph
    x, y, mx, my, h_px = grid(w_m, h_m)
    pr = np.floor(y / ph).astype(int) % rows
    pc = np.floor(x / pw).astype(int) % cols
    u, v = np.mod(x, pw), np.mod(y, ph)
    d_edge = np.minimum(np.minimum(u, pw - u), np.minimum(v, ph - v)) - joint / 2
    in_joint = d_edge < 0
    panel_tone = 1 + 0.015 * (np.random.default_rng(SEED + seed).random((rows, cols))[pr, pc] - 0.5) * 2
    brushed = 0.012 * fft_noise(h_px, PX, 0.8, 30, seed + 1)
    rgb = np.array(base)[None, None, :] * (panel_tone + brushed)[..., None]
    rgb *= (0.9 + 0.1 * np.clip(d_edge / 0.004, 0, 1))[..., None]
    rgb = np.where(in_joint[..., None], np.array([0.06, 0.06, 0.065]), rgb)
    height = np.where(in_joint, -10.0, -1.5 * (1 - np.clip(d_edge / 0.003, 0, 1)))
    save_pair(folder, rgb, height, mx, my)
    return w_m, h_m


def stucco(folder, seed):
    x, y, mx, my, h_px = grid(2.4, 2.4)
    fine = fft_noise(h_px, PX, 0.9, 0.9, seed + 1)
    grain = fft_noise(h_px, PX, 2.5, 2.5, seed + 2)
    soft = fft_noise(h_px, PX, 22, 22, seed + 3)
    pits = fft_noise(h_px, PX, 1.1, 1.1, seed + 4) > 2.7
    tone = 0.80 * (1 + 0.035 * fine + 0.02 * grain + 0.012 * soft)
    tone = np.where(pits, tone * 0.82, tone)
    height = 0.25 * fine + 0.35 * grain - np.where(pits, 0.6, 0.0)
    save_pair(folder, np.dstack([tone, tone * 0.995, tone * 0.985]), height, mx, my)


def flatten(folder, src_dir, src_prefix, sigma_px, keep, copy_suffixes):
    img = bpy.data.images.load(os.path.join(map_dir, src_dir, src_prefix + "Color.jpg"))
    w, h = img.size
    rgb = np.array(img.pixels[:], dtype=np.float64).reshape(h, w, 4)[..., :3]
    low = blur(rgb, sigma_px)
    mean = rgb.reshape(-1, 3).mean(axis=0)
    flat = rgb * (mean[None, None, :] / np.maximum(low, 1e-3)) ** (1 - keep)
    save(folder, folder + "_Color", np.dstack([np.clip(flat, 0, 1), np.ones((h, w))]), False)
    for suffix in copy_suffixes:
        src = os.path.join(map_dir, src_dir, src_prefix + suffix + ".jpg")
        dst = os.path.join(map_dir, folder, folder + "_" + suffix + ".jpg")
        shutil.copyfile(src, dst)
        print("COPIED", dst, flush=True)


sizes = {
    "TileBrown": masonry("TileBrown", 0.227, 0.060, 0.008, 8, 28, True, (0.38, 0.25, 0.18), 0.06, 0.03, (0.60, 0.58, 0.54), 3.0, 0.0015, 0.4, 0.0, 1.0, 10),
    "TileBeige": masonry("TileBeige", 0.227, 0.060, 0.008, 8, 28, True, (0.80, 0.71, 0.56), 0.05, 0.02, (0.72, 0.70, 0.66), 3.0, 0.0015, 0.4, 0.0, 1.0, 20),
    "TileGrey": masonry("TileGrey", 0.295, 0.145, 0.005, 8, 16, False, (0.58, 0.58, 0.57), 0.05, 0.01, (0.45, 0.45, 0.45), 2.5, 0.0015, 0.3, 0.0, 1.0, 30),
    "Brick1": masonry("Brick1", 0.190, 0.057, 0.010, 10, 30, True, (0.55, 0.25, 0.17), 0.10, 0.05, (0.70, 0.68, 0.63), 6.0, 0.003, 1.0, 0.05, 0.86, 40,
                      joint_jit=0.002, row_jit=0.004, chip=0.0012),
    "GraniteLight": granite("GraniteLight", 1.2, 0.6, 0.005, 2, 4, (0.74, 0.73, 0.70), 50),
    "Panel1": panels("Panel1", 1.2, 1.7, 0.012, 2, 2, (0.80, 0.80, 0.80), 60),
}
stucco("Stucco1", 70)
flatten("ExposedConcrete", "Wall C", "Concrete008_2K-JPG_", 60, 0.3, ["NormalGL", "AmbientOcclusion"])
flatten("RoofConcrete", "Wall B", "Concrete020_2K-JPG_", 60, 0.3, ["NormalGL"])

# 큰 얼룩층 (1024², 한 장 = 30 m)
M = 1024
n = 0.6 * fft_noise(M, M, 70, 70, 101) + 0.3 * fft_noise(M, M, 28, 28, 102) + 0.1 * fft_noise(M, M, 9, 9, 103)
n /= n.std()
macro = 0.5 + 0.065 * np.clip(n, -2.2, 2.2)
hue = 0.008 * fft_noise(M, M, 90, 90, 104)
save("Macro1", "Macro1_Detail", np.dstack([macro + hue, macro, macro - hue, np.ones((M, M))]), True, "PNG")

# 때 자국 아틀라스 2048²: 띠마다 682 px — 0 1층 흙탕 때(가로 한 장 4 m) · 1 빗물 줄(3 m) · 2 옥상 밑 줄(4 m)
G = 2048
BAND = G // 3
atlas = np.zeros((G, G, 4))
for b in range(3):
    y0 = b * BAND
    hb = BAND if b < 2 else G - 2 * BAND
    vl = (np.arange(hb)[:, None] + 0.5) / hb
    if b == 0:
        edge = 0.6 + 0.2 * fft_noise(1, G, 40, 1, 201)[0][None, :]
        a = np.clip(1 - vl / edge, 0, 1) ** 1.4 * (0.5 + 0.12 * fft_noise(hb, G, 8, 8, 202))
        spots = (fft_noise(hb, G, 3, 3, 203) > 2.1) & (vl < 0.55)
        a = np.where(spots, np.maximum(a, 0.4), a)
        col = np.array([0.30, 0.27, 0.23])
    else:
        s = np.clip(fft_noise(1, G, 14 if b == 1 else 18, 1, 300 + b)[0] + 0.3, 0, None)
        s = s / (s.max() + 1e-9)
        length = 0.3 + 0.7 * np.clip(0.5 + 0.35 * fft_noise(1, G, 30, 1, 310 + b)[0], 0, 1)
        fade = np.clip((vl - (1 - length[None, :])) / np.maximum(length[None, :], 1e-3), 0, 1) ** 1.2
        wobble = 1 + 0.2 * fft_noise(hb, G, 3, 30, 320 + b)
        a = (s[None, :] ** 0.7) * fade * wobble * (0.75 if b == 1 else 0.85)
        a = np.maximum(a, (0.22 if b == 1 else 0.32) * np.clip((vl - 0.75) / 0.25, 0, 1) ** 1.5)
        col = np.array([0.24, 0.23, 0.22])
    a[:3, :] = 0
    a[-3:, :] = 0
    atlas[y0:y0 + hb, :, :3] = col[None, None, :]
    atlas[y0:y0 + hb, :, 3] = np.clip(a, 0, 1)
save("Grime1", "Grime1_Color", atlas, False, "PNG")
print("SIZES", sizes, flush=True)
