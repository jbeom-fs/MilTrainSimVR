# 차량 마무리 (11/10) — make_prop_lowpoly.py 가 줄여 구운 차를 '게임용 차'로 만든다
#   1) 텍셀마다 3D 자리를 굽고(자리 지도) 색 + 자리로 부위를 나눈다: 도장 · 유리 · 창틀 · 타이어 · 휠 · 등 · 번호판 · 크롬 · 검정 플라스틱
#   2) 텍스처를 부위별로 다시 칠한다: 도장 얼룩 줄이기 · 타이어/휠 정리, 부위마다 금속 · 매끈함, 도장에만 클리어코트
#   3) 유리를 진짜 투명하게: 몸통은 유리 자리를 알파로 도려내고, 유리 자리를 덮는 면만 복사한 유리 메시(투명 재질)를 따로 만든다
#   4) 창 너머가 비어 보이지 않게 실내를 만든다: 객실 쪽 몸통 면을 뒤집은 안쪽 껍데기 + 바닥 · 대시보드 · 운전대 · 앞좌석 · 뒷좌석 · 선반
#   5) FBX 하나로 내보낸다 — 오브젝트 셋(<Name> · <Name>_Glass · <Name>_Interior), 재질 이름 <Name>_Body · _Glass · _Interior
#   텍스처: _basecolor.png(A = 유리 아닌 곳) · _normal.png · _metallicSmoothness.png · _clearcoat.png(R = 도장) · _glass.png(RGB 색, A = 유리 x 짙기)
#   4K 로 구운 것을 2K 로 줄여 저장한다(경계가 부드럽게). 좌표: Blender X = 길이, Y = 폭(+Y 가 왼쪽), Z = 위
# blender -b --factory-startup --python vehicle_finish.py -- cfg.json
import bpy, bmesh, sys, os, json, math
import numpy as np
from mathutils import Matrix
from mathutils.bvhtree import BVHTree

cfg = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
OUT = cfg["out_dir"]
os.makedirs(OUT, exist_ok=True)
NAME = cfg["name"]
SX, SY, SZ = cfg["scale"]                      # 모델 한 칸이 몇 m 인지 — Blender X(길이) · Y(폭) · Z(높이)
FS = -1.0 if cfg.get("front", "+X") == "-X" else 1.0
log = {}

bpy.ops.wm.open_mainfile(filepath=cfg["blend"])
ob = bpy.data.objects[cfg.get("object", NAME)]
me = ob.data
co = np.empty(len(me.vertices) * 3, np.float64)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
mn, mx = co.min(0), co.max(0)


def load(path, non_color):
    img = bpy.data.images.load(path)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)


col = load(cfg["basecolor"], False)[..., :3].copy()
RES = col.shape[0]

# ---------------------------------------------------------------- 자리 지도 굽기
sc = bpy.context.scene
sc.render.engine = "CYCLES"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for dv in prefs.devices:
        dv.use = dv.type == "OPTIX"
    sc.cycles.device = "GPU"
except Exception:
    sc.cycles.device = "CPU"
sc.cycles.samples = 1
mat = bpy.data.materials.new("POS")
mat.use_nodes = True
nt = mat.node_tree
nt.nodes.clear()
n_out = nt.nodes.new("ShaderNodeOutputMaterial")
n_em = nt.nodes.new("ShaderNodeEmission")
n_geo = nt.nodes.new("ShaderNodeNewGeometry")
n_sub = nt.nodes.new("ShaderNodeVectorMath")
n_sub.operation = "SUBTRACT"
n_sub.inputs[1].default_value = mn
n_div = nt.nodes.new("ShaderNodeVectorMath")
n_div.operation = "DIVIDE"
n_div.inputs[1].default_value = mx - mn
nt.links.new(n_geo.outputs["Position"], n_sub.inputs[0])
nt.links.new(n_sub.outputs[0], n_div.inputs[0])
nt.links.new(n_div.outputs[0], n_em.inputs["Color"])
nt.links.new(n_em.outputs[0], n_out.inputs["Surface"])
pimg = bpy.data.images.new("POS", RES, RES, alpha=True, float_buffer=True)
pimg.colorspace_settings.name = "Non-Color"
pimg.generated_color = (0.0, 0.0, 0.0, 0.0)
n_tex = nt.nodes.new("ShaderNodeTexImage")
n_tex.image = pimg
nt.nodes.active = n_tex
me.materials.clear()
me.materials.append(mat)
for o in bpy.context.view_layer.objects:
    o.select_set(False)
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.bake(type="EMIT", margin=cfg.get("margin", 48), margin_type="EXTEND", use_clear=True, target="IMAGE_TEXTURES")
pa = np.empty(RES * RES * 4, np.float32)
pimg.pixels.foreach_get(pa)
pa = pa.reshape(RES, RES, 4)
cover = pa[..., 3] > 0.5
X = (pa[..., 0] * (mx[0] - mn[0]) + mn[0]) * SX * FS     # 앞(+) · 뒤(-)  m
Y = (pa[..., 1] * (mx[1] - mn[1]) + mn[1]) * SY * FS     # 왼쪽(+)
Z = pa[..., 2] * (mx[2] - mn[2]) * SZ                     # 바닥에서 높이
del pa
bpy.data.images.remove(pimg)
log["cover"] = round(float(cover.mean()), 3)
half_w = float(np.abs(Y[cover]).max())
x_front, x_rear = float(X[cover].max()), float(X[cover].min())
length = x_front - x_rear
z_top = float(Z[cover].max())
log["size_m"] = [round(length, 2), round(half_w * 2, 2), round(z_top, 2)]


def box(a, k):
    """k x k 평균 (적분 영상)"""
    a = a.astype(np.float32)
    r = k // 2
    p = np.pad(a, ((r + 1, r), (r + 1, r)), mode="edge")
    c = p.cumsum(0, dtype=np.float64).cumsum(1)
    h, w = a.shape
    return ((c[k:k + h, k:k + w] - c[:h, k:k + w] - c[k:k + h, :w] + c[:h, :w]) / float(k * k)).astype(np.float32)


KS = RES // 2048                                         # 필터 크기를 해상도에 맞춘다 (2K 기준)


def oct_dilate(m, r):
    """팔각형(거의 원) 반경 r 칸만큼 넓힌다"""
    o = m.copy()
    for i in range(r):
        p = np.pad(o, 1)
        if i % 2 == 0:
            o = p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
        else:
            o = (p[:-2, :-2] | p[:-2, 1:-1] | p[:-2, 2:] | p[1:-1, :-2] | p[1:-1, 1:-1] | p[1:-1, 2:] | p[2:, :-2] | p[2:, 1:-1] | p[2:, 2:])
    return o


def oct_erode(m, r):
    return ~oct_dilate(~m, r)


def components(mask):
    """이어진 덩어리마다 번호 (4방향)"""
    lab = np.zeros(mask.shape, np.int32)
    n_ = 0
    h_, w_ = mask.shape
    for a0_, b0_ in np.argwhere(mask):
        if lab[a0_, b0_]:
            continue
        n_ += 1
        lab[a0_, b0_] = n_
        stack = [(int(a0_), int(b0_))]
        while stack:
            a1, b1 = stack.pop()
            for a2, b2 in ((a1 + 1, b1), (a1 - 1, b1), (a1, b1 + 1), (a1, b1 - 1)):
                if 0 <= a2 < h_ and 0 <= b2 < w_ and mask[a2, b2] and not lab[a2, b2]:
                    lab[a2, b2] = n_
                    stack.append((a2, b2))
    return lab, n_


def hull_fill(cells, shape):
    """점들의 볼록 껍질 안을 채운 그림"""
    pts_ = sorted(set(map(tuple, cells.tolist())))
    if len(pts_) < 3:
        return np.zeros(shape, bool)

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for p in pts_:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(pts_):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    hull = lower[:-1] + upper[:-1]
    lo = np.full(shape[0], 1e9)
    hi = np.full(shape[0], -1e9)
    for i in range(len(hull)):
        (a1, b1), (a2, b2) = hull[i], hull[(i + 1) % len(hull)]
        if a1 == a2:
            lo[a1] = min(lo[a1], b1, b2)
            hi[a1] = max(hi[a1], b1, b2)
            continue
        if a1 > a2:
            a1, b1, a2, b2 = a2, b2, a1, b1
        for a in range(a1, a2 + 1):
            b = b1 + (b2 - b1) * (a - a1) / (a2 - a1)
            lo[a] = min(lo[a], b)
            hi[a] = max(hi[a], b)
    out_ = np.zeros(shape, bool)
    for a in range(shape[0]):
        if hi[a] >= lo[a]:
            out_[a, max(0, int(math.floor(lo[a]))):min(shape[1], int(math.ceil(hi[a])) + 1)] = True
    return out_


# ---------------------------------------------------------------- 색으로 먼저 나눈다
R, G, B = col[..., 0], col[..., 1], col[..., 2]
L = (R + G + B) / 3.0
cmx = np.maximum(np.maximum(R, G), B)
cmn = np.minimum(np.minimum(R, G), B)
sat = cmx - cmn
is_red = (R - np.maximum(G, B) > 0.22) & (R > 0.45)
is_dark = (cmx < cfg.get("dark_max", 0.20)) & ~is_red
is_white = (L > cfg.get("white_min", 0.70)) & (sat < 0.12) & ~is_red
is_slate = ~is_dark & ~is_red & (L < 0.52) & ((B - R) > 0.015)
is_paint = is_white
if cfg.get("paint_rgb"):                                   # 흰색이 아닌 도장(예: 파란 트럭) — 가까운 색을 도장으로
    pr = np.array(cfg["paint_rgb"], np.float32) / 255.0
    is_paint = (np.abs(col - pr).max(axis=2) < cfg.get("paint_tol", 0.16)) & ~is_dark
    is_slate &= ~is_paint
is_grey = ~(is_red | is_dark | is_white | is_slate | is_paint)

# ---------------------------------------------------------------- 바퀴 자리
Rw = cfg.get("wheel_radius", 0.315)
contact = cover & is_dark & (Z < 0.06) & (np.abs(Y) > half_w - 0.45)
axles = []
for sgn in (1, -1):
    xs = X[contact & (X * sgn > 0.3)]
    axles.append(float(np.median(xs)) if xs.size > 50 else sgn * length * 0.31)
if cfg.get("axles"):
    axles = cfg["axles"]
log["axles_m"] = [round(a, 3) for a in axles]
in_wheel = np.zeros(col.shape[:2], bool)
rim_zone = np.zeros(col.shape[:2], bool)
for xa in axles:
    d2 = (X - xa) ** 2 + (Z - Rw) ** 2
    in_wheel |= d2 < (Rw * 1.10) ** 2
    rim_zone |= d2 < (Rw * 0.70) ** 2
in_wheel &= np.abs(Y) > half_w - cfg.get("wheel_depth", 0.34)
rim_zone &= in_wheel

# ---------------------------------------------------------------- 부위
names = ["other", "paint", "glass", "trim", "black", "tire", "rim", "chrome", "lamp", "lamp_red", "plate", "line", "caliper"]
OTHER, PAINT, GLASS, TRIM, BLACK, TIRE, RIM, CHROME, LAMP, LAMP_RED, PLATE, LINE, CALIPER = range(13)
belt = cfg.get("beltline", 0.88)
cls = np.full(col.shape[:2], OTHER, np.uint8)
solid_grey = box(is_grey, 7 * KS + 1) > 0.55
fl, rl = cfg.get("front_lamp", {}), cfg.get("rear_lamp", {})
front_lamp = (X > x_front - fl.get("depth", 0.95)) & (np.abs(Y) > fl.get("y_min", 0.35)) & (Z > fl.get("z0", 0.50)) & (Z < fl.get("z1", 0.98))
rear_lamp = (X < x_rear + rl.get("depth", 0.60)) & (Z > rl.get("z0", 0.68)) & (Z < rl.get("z1", 1.10))
fp, rp = cfg.get("front_plate", {}), cfg.get("rear_plate", {})
front_plate = (X > x_front - 0.30) & (np.abs(Y) < fp.get("y", 0.30)) & (Z > fp.get("z0", 0.25)) & (Z < fp.get("z1", 0.62))
rear_plate = (X < x_rear + 0.35) & (np.abs(Y) < rp.get("y", 0.32)) & (Z > rp.get("z0", 0.66)) & (Z < rp.get("z1", 1.00))
body = ~in_wheel
cls[body & is_white] = PAINT if not cfg.get("paint_rgb") else CHROME
cls[body & is_paint] = PAINT
cls[body & is_white & (front_plate | rear_plate) & (L > cfg.get("plate_min", 0.90))] = PLATE
cls[body & is_dark] = BLACK
cls[body & is_dark & (Z > belt)] = TRIM
cls[body & is_slate] = BLACK
cls[body & is_slate & (Z > belt)] = GLASS
cls[body & is_grey] = LINE
cls[body & is_grey & solid_grey] = CHROME
cls[body & is_grey & solid_grey & (front_lamp | rear_lamp)] = LAMP
cls[body & is_red] = LAMP_RED
cls[in_wheel] = TIRE
cls[rim_zone & ~is_dark] = RIM
cls[rim_zone & is_red] = CALIPER
# 유리 가장자리 다듬기 1: 유리 쪽이 많은 곳은 유리로(흰 번짐 지우기), 유리 색인데 도장 쪽이 많은 곳은 도장으로
k9 = 4 * KS + 1
gfrac = box(cls == GLASS, k9)
pfrac = box(cls == PAINT, k9)
glass2 = (gfrac > 0.5) & (Z > belt) & body & (cls != TRIM) & (cls != LAMP_RED)
stray = (cls == GLASS) & ~glass2 & (pfrac > 0.5)
cls[glass2] = GLASS
cls[stray] = PAINT
# 도장 안의 넓은 회색 얼룩(선이 아닌 것)도 도장으로
smear = ((cls == LINE) | (cls == CHROME)) & (box(cls == PAINT, 10 * KS + 1) > 0.80) & (L > 0.50)
cls[smear] = PAINT

# 창 다시 그리기 (11/10 사용자: 창 아래가 들쭉날쭉 · 창틀 속 흰 점 — '메쉬가 이상하다')
#   구운 텍스처의 유리 경계는 UV 조각마다 따로 번져 있다 → 텍스처 위가 아니라 '창이 놓인 평면'에서 다시 그린다.
#   창 넷(왼쪽 · 오른쪽 옆 유리, 앞 · 뒤 유리)마다 유리 텍셀로 평면을 맞추고, 그 평면 좌표(3 mm 칸)에 유리 자리를 찍어
#   메우고 둥글게 다듬은 뒤, 그 그림으로 유리 · 창틀(고른 검은 띠) · 창 아래 도장을 다시 정한다
glass_sel = (cls == GLASS) & cover
x_ws = float(np.percentile(X[glass_sel], 99.5))
x_rg = float(np.percentile(X[glass_sel], 0.5))
x_mid = (x_ws + x_rg) * 0.5
seeds = {
    "left": glass_sel & (Y > half_w - 0.36) & (X < x_ws - 0.80) & (X > x_rg + 0.45),
    "right": glass_sel & (Y < -(half_w - 0.36)) & (X < x_ws - 0.80) & (X > x_rg + 0.45),
    "front": glass_sel & (np.abs(Y) < 0.40) & (X > x_ws - 0.65),
    "rear": glass_sel & (np.abs(Y) < 0.40) & (X < x_rg + 0.55),
}
seeds.update({kk: glass_sel & eval(vv, {"X": X, "Y": Y, "Z": Z, "np": np}) for kk, vv in cfg.get("window_seeds", {}).items()})
planes = {}
for gname, sel in seeds.items():
    if int(sel.sum()) < 500:
        continue
    pts_ = np.stack([X[sel], Y[sel], Z[sel]], axis=1).astype(np.float64)
    if len(pts_) > 150000:
        pts_ = pts_[:: len(pts_) // 150000]
    c_ = pts_.mean(0)
    n_ = np.linalg.svd(pts_ - c_, full_matrices=False)[2][2]
    planes[gname] = (c_, n_)
order = list(planes)
stack = []
for gname in order:
    c_, n_ = planes[gname]
    d_ = np.abs((X - c_[0]) * n_[0] + (Y - c_[1]) * n_[1] + (Z - c_[2]) * n_[2]).astype(np.float32)
    if gname == "left":
        d_[Y < 0.15] = 9.0
    elif gname == "right":
        d_[Y > -0.15] = 9.0
    elif gname == "front":
        d_[X < x_mid] = 9.0
    elif gname == "rear":
        d_[X > x_mid] = 9.0
    stack.append(d_)
stack = np.stack(stack, axis=0)
grp = stack.argmin(axis=0).astype(np.int8)
dmin = stack.min(axis=0)
del stack
CELL = 0.003
win_log = {}
claimed = np.zeros(cls.shape, bool)
for gi, gname in enumerate(order):
    c_, n_ = planes[gname]
    is_side = gname in ("left", "right")
    tol = cfg.get("side_tol", 0.06) if is_side else cfg.get("screen_tol", 0.10)
    if is_side:
        a_, b_ = X, Z
    else:
        ev = np.cross(n_, [0.0, 1.0, 0.0])
        ev /= np.linalg.norm(ev)
        if ev[2] < 0:
            ev = -ev
        a_ = Y
        b_ = ((X - c_[0]) * ev[0] + (Y - c_[1]) * ev[1] + (Z - c_[2]) * ev[2]).astype(np.float32)
    in_g = (grp == gi) & (dmin < tol) & body & cover
    gsel = in_g & (cls == GLASS)
    if int(gsel.sum()) < 500:
        continue
    pad = 0.18
    a0, b0 = float(a_[gsel].min()) - pad, float(b_[gsel].min()) - pad
    na = int((float(a_[gsel].max()) + pad - a0) / CELL) + 1
    nb = int((float(b_[gsel].max()) + pad - b0) / CELL) + 1
    ia = ((a_ - a0) / CELL).astype(np.int32)
    ib = ((b_ - b0) / CELL).astype(np.int32)
    valid = in_g & (ia >= 0) & (ia < na) & (ib >= 0) & (ib < nb)
    occ = np.zeros((na, nb), np.float32)
    seen = np.zeros((na, nb), np.float32)
    seen[ia[valid], ib[valid]] = 1.0
    gv = gsel & valid
    occ[ia[gv], ib[gv]] = 1.0
    den = box(seen, 5)
    m_ = (box(occ, 5) > 0.5 * np.maximum(den, 1e-6)) & (den > 0.02)        # 본 칸 가운데 유리가 절반 넘는 곳 (빈 칸 메우기)
    sr = cfg.get("window_smooth_px", 3)
    m_ = box(m_, 2 * sr + 1) > 0.5                                         # 둥글게
    if is_side:
        # 옆 유리 아래 끝(벨트라인): 구울 때 창 아래 틈이 어둡게 번져 유리 아래 끝이 들쭉날쭉하다. 색으로는 진짜 끝을 알 수 없어서
        # 모양으로 정한다 — 유리는 유리 평면 위에 있고, 문 겉판은 벨트라인에서 바깥으로 턱이 진다. 칸마다 평면까지의 거리(바깥이 +)를
        # 재서, 유리 아래로 '아직 유리 면인 칸'까지만 채우고 턱 위로 올라탄 유리는 지운다 (곡선 맞춤은 뒷문에서 유리가 문으로 처졌다)
        out_sign = 1.0 if n_[1] * (1.0 if gname == "left" else -1.0) > 0 else -1.0
        sd = (((X - c_[0]) * n_[0] + (Y - c_[1]) * n_[1] + (Z - c_[2]) * n_[2]) * out_sign).astype(np.float32)
        flat = ia[valid].astype(np.int64) * nb + ib[valid]
        scnt = np.bincount(flat, minlength=na * nb).reshape(na, nb)
        ssum = np.bincount(flat, weights=sd[valid], minlength=na * nb).reshape(na, nb)
        sdm = np.where(scnt > 0, ssum / np.maximum(scnt, 1), np.nan)
        g0 = float(np.nanmedian(sdm[m_ & (scnt > 0)]))
        tol_g = cfg.get("glass_plane_tol", 0.009)
        max_fill = int(cfg.get("belt_fill", 0.05) / CELL)
        stops, steps_ = [], []
        for a__ in np.where(m_.any(axis=1))[0]:
            lo_ = int(np.argmax(m_[a__]))
            for b__ in range(lo_, min(lo_ + 8, nb)):                      # 턱 위로 올라탄 유리 지우기
                if scnt[a__, b__] > 0 and sdm[a__, b__] - g0 > tol_g + 0.004:
                    m_[a__, b__] = False
            if not m_[a__].any():
                continue
            lo_ = int(np.argmax(m_[a__]))
            gap, n_fill = 0, 0
            for b__ in range(lo_ - 1, max(lo_ - 1 - max_fill, -1), -1):   # 유리 면인 동안 아래로 채우기
                if scnt[a__, b__] == 0:
                    gap += 1
                    if gap > 2:
                        break
                    continue
                gap = 0
                if sdm[a__, b__] - g0 > tol_g:
                    stops.append(float(sdm[a__, b__] - g0))
                    break
                m_[a__, b__] = True
                n_fill += 1
            steps_.append(n_fill)
        m_ = box(m_, 5) > 0.5
        belt_log = {"glass_plane_offset": round(g0, 4), "columns": len(steps_), "fill_cells_median": float(np.median(steps_)) if steps_ else 0,
                    "fill_hit_limit": int(sum(1 for v in steps_ if v >= max_fill - 1)), "shoulder_step_median": round(float(np.median(stops)), 4) if stops else None}
    # 창 하나하나를 반듯한 모양으로: 구운 색으로 찍은 유리 자리는 테두리 몇 cm 가 믿을 수 없다(찢긴 종이처럼 나온다).
    # 창은 볼록한 판이니 — 가늘게 깎아 낸 속(잡티 · 가는 다리 제거)을 덩어리마다 볼록 껍질로 싸고, 깎은 만큼 다시 넓혀 모서리를 둥글린다
    pre_hull = m_.copy()                                                   # 볼록하게 싸기 전 유리 자리 (아래 끝 높이를 재는 데 쓴다)
    er = int(round(cfg.get("pane_erode", 0.021) / CELL))
    core = oct_erode(m_, er)
    lab_, n_lab = components(core)
    reg = np.zeros(m_.shape, bool)
    panes = 0
    for li in range(1, n_lab + 1):
        comp = lab_ == li
        if int(comp.sum()) * CELL * CELL < cfg.get("pane_min_area", 0.004):
            continue
        edge = comp & ~oct_erode(comp, 1)
        reg |= hull_fill(np.argwhere(edge), m_.shape)
        panes += 1
    if panes:
        m_ = oct_dilate(reg, er)
        if is_side:
            # 볼록 껍질은 아래 변을 곧은 선으로 메워서, 살짝 휜 벨트라인 아래 문 겉판까지 유리가 내려간다(문에 검은 조각 구멍).
            # 아래 끝만은 싸기 전 유리 자리의 아래 테두리를 따른다 — 칸마다 가장 낮은 유리 높이를 둘레 6 cm 에서 낮은 쪽(15 %)으로 잡아
            # (어두운 번짐 때문에 높게 뜬 칸을 거른다) 고르게 편 선. 그 선보다 아래는 유리가 아니다
            cols = np.where(pre_hull.any(axis=1))[0]
            if len(cols) > 20:
                lo_pre = np.array([int(np.argmax(pre_hull[a__])) for a__ in cols], np.float64)
                w1 = int(round(0.06 / CELL))
                env = np.array([np.percentile(lo_pre[max(0, i__ - w1):i__ + w1 + 1], 15) for i__ in range(len(cols))])
                w2 = int(round(0.045 / CELL))
                env = np.convolve(np.pad(env, w2, mode="edge"), np.ones(2 * w2 + 1) / (2 * w2 + 1), mode="valid")
                full = np.interp(np.arange(na), cols, env)             # 유리 없는 칸(기둥 자리)은 양옆을 이어서
                for a__ in range(na):
                    cut = int(round(full[a__]))
                    if cut > 0:
                        m_[a__, :min(cut, nb)] = False
            m_ = box(m_, 3) > 0.5
    rr = int(round((cfg.get("side_seal", 0.021) if is_side else cfg.get("screen_seal", 0.015)) / CELL))
    dil = oct_dilate(m_, rr)
    near_ = None
    if is_side:
        # 창틀(검정)은 모양으로만 정한다: 창 둘레 고른 띠 + 같은 높이에서 두 유리 사이에 낀 자리(B 필러 · 쪽창 살).
        # 구운 색의 검은 번짐은 테두리가 들쭉날쭉해서 창틀로 쓰지 않는다 — 창에서 5 cm 안의 나머지 어두운 얼룩은 도장으로 돌린다
        kp = int(round(cfg.get("pillar_max", 0.16) / CELL / 2))
        c0 = np.cumsum(np.pad(m_.astype(np.int32), ((kp + 1, kp), (0, 0))), axis=0)
        dil_a = (c0[2 * kp + 1:] - c0[:-(2 * kp + 1)]) > 0                   # X 방향으로만 넓힌다
        c1 = np.cumsum(np.pad(dil_a.astype(np.int32), ((kp + 1, kp), (0, 0)), constant_values=1), axis=0)
        closed = (c1[2 * kp + 1:] - c1[:-(2 * kp + 1)]) >= 2 * kp + 1          # 다시 좁힌다 → 두 유리 사이만 남는다
        pillar = closed & ~m_
        dil = oct_dilate(m_ | pillar, rr)
        near_ = oct_dilate(m_ | pillar, rr + int(round(cfg.get("smear_reach", 0.05) / CELL)))
    # 3 mm 칸 그림을 텍셀(약 1.8 mm)에 그대로 옮기면 테두리가 계단이 된다 → 살짝 흐린 그림을 칸 사이에서 보간해 0.5 선으로 자른다
    fa = ((a_[valid] - a0) / CELL - 0.5).astype(np.float32)
    fb = ((b_[valid] - b0) / CELL - 0.5).astype(np.float32)
    i0 = np.clip(np.floor(fa).astype(np.int32), 0, na - 2)
    j0 = np.clip(np.floor(fb).astype(np.int32), 0, nb - 2)
    wa = np.clip(fa - i0, 0.0, 1.0)
    wb = np.clip(fb - j0, 0.0, 1.0)

    def smooth_lookup(mask_):
        f_ = box(mask_.astype(np.float32), 3)
        return (f_[i0, j0] * (1 - wa) * (1 - wb) + f_[i0 + 1, j0] * wa * (1 - wb) + f_[i0, j0 + 1] * (1 - wa) * wb + f_[i0 + 1, j0 + 1] * wa * wb) > 0.5

    mg = np.zeros(cls.shape, bool)
    rg = np.zeros(cls.shape, bool)
    mg[valid] = smooth_lookup(m_)
    rg[valid] = smooth_lookup(dil)
    claimed |= valid
    keep_ = cls != LAMP_RED
    cls[valid & ~mg & (cls == GLASS)] = TRIM                               # 다듬은 창 밖으로 삐져나온 유리 → 창틀
    cls[valid & rg & ~mg & keep_] = TRIM                                   # 창 둘레 고른 검은 띠
    cls[valid & mg & keep_] = GLASS
    fixed = 0
    if is_side:                                                            # 창틀 밖 5 cm 안에 남은 어두운 번짐 → 도장
        nr = np.zeros(cls.shape, bool)
        nr[valid] = smooth_lookup(near_)
        fix = valid & nr & ~rg & ~mg & ((cls == TRIM) | (cls == BLACK) | (cls == LINE) | (cls == CHROME))
        cls[fix] = PAINT
        fixed = int(fix.sum())
    win_log[gname] = {"normal": [round(float(v), 3) for v in n_], "cells": [na, nb], "panes": panes, "glass_texels": int((valid & mg).sum()), "below_fixed": fixed}
    if is_side:
        win_log[gname]["belt"] = belt_log
# 유리는 위에서 반듯하게 그린 창에만 둔다 — 어느 창에도 속하지 않는데 색만 유리인 텍셀(기둥 속 구멍처럼 보인다)은 창틀로
orphan = (cls == GLASS) & ~claimed
cls[orphan] = TRIM
win_log["orphan_glass_texels"] = int((orphan & cover).sum())
del grp, dmin, claimed
log["windows"] = win_log
pane_planes = {g_: (planes[g_][0].copy(), planes[g_][1].copy()) for g_ in planes}
log["class_share"] = {names[i]: round(float(((cls == i) & cover).sum() / max(1, cover.sum())), 4) for i in range(13)}
gm = cls == GLASS
gx = X[gm & cover]
log["glass_x_m"] = [round(float(np.percentile(gx, 0.5)), 3), round(float(np.percentile(gx, 99.5)), 3)]
gx0, gx1 = log["glass_x_m"]

# ---------------------------------------------------------------- 색 다시 칠하기
out = col.copy()
pm = cls == PAINT
paint_src = is_paint if cfg.get("paint_rgb") else is_white
paint_med = np.median(col[pm & cover & paint_src], axis=0)
log["paint_median_srgb"] = [int(round(v * 255)) for v in paint_med]
k = cfg.get("paint_keep", 0.35)                           # 도장 얼룩을 얼마나 남길지
base = np.where(paint_src[..., None], col, paint_med[None, None, :])
out[pm] = (paint_med[None, :] + (base[pm] - paint_med[None, :]) * k).clip(0, 1)
if cfg.get("paint_out_rgb"):                              # 도장을 정해 준 색으로 옮긴다 (밝기 차이는 그대로)
    out[pm] = (out[pm] - paint_med[None, :] + np.array(cfg["paint_out_rgb"], np.float32)[None, :] / 255.0).clip(0, 1)
out[gm] = np.array(cfg.get("glass_rgb", [22, 26, 31]), np.float32) / 255.0
out[cls == TRIM] = np.array(cfg.get("trim_rgb", [19, 19, 20]), np.float32) / 255.0
out[cls == TIRE] = np.array(cfg.get("tire_rgb", [24, 24, 25]), np.float32) / 255.0
rimm = cls == RIM
out[rimm] = col[rimm] * 0.55 + np.array(cfg.get("rim_rgb", [200, 203, 208]), np.float32) / 255.0 * 0.45

# ---------------------------------------------------------------- 금속 · 매끈함 · 클리어코트
SM = {OTHER: 0.40, PAINT: 0.62, GLASS: 0.95, TRIM: 0.34, BLACK: 0.35, TIRE: 0.22, RIM: 0.74, CHROME: 0.80, LAMP: 0.92,
      LAMP_RED: 0.88, PLATE: 0.40, LINE: 0.55, CALIPER: 0.50}
MT = {RIM: 0.90, CHROME: 0.85, PAINT: 0.0}
SM.update({names.index(kk): vv for kk, vv in cfg.get("smooth", {}).items()})
MT.update({names.index(kk): vv for kk, vv in cfg.get("metal", {}).items()})
smooth = np.zeros(col.shape[:2], np.float32)
metal = np.zeros(col.shape[:2], np.float32)
for i in range(13):
    m_ = cls == i
    smooth[m_] = SM.get(i, 0.4)
    metal[m_] = MT.get(i, 0.0)
smooth = box(smooth, 3)
metal = box(metal, 3)


def down(a, f):
    if f == 1:
        return a
    h, w = a.shape[:2]
    return a.reshape(h // f, f, w // f, f, -1).mean(axis=(1, 3))


f = RES // cfg.get("out_res", 2048)
saved = {}


def save(arr, key, fmt, non_color, quality=95):
    path = os.path.join(OUT, NAME + key)
    h, w = arr.shape[:2]
    alpha = arr.shape[2] == 4
    img = bpy.data.images.new("o", w, h, alpha=alpha, float_buffer=False)
    img.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    img.alpha_mode = "CHANNEL_PACKED"
    rgba = np.ones((h, w, 4), np.float32)
    rgba[..., :arr.shape[2]] = arr
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = fmt
    if fmt == "JPEG":
        img.save(filepath=path, quality=quality)
    else:
        img.save(filepath=path)
    bpy.data.images.remove(img)
    saved[key] = path


glass_f = down(gm[..., None].astype(np.float32), f)[..., 0]
bc = np.concatenate([down(out, f), (1.0 - glass_f)[..., None]], axis=2)
save(bc, "_basecolor.png", "PNG", False)
ms = np.zeros(col.shape[:2] + (4,), np.float32)
ms[..., 0] = ms[..., 1] = ms[..., 2] = metal
ms[..., 3] = smooth
save(down(ms, f), "_metallicSmoothness.png", "PNG", True)
del ms
nrm = load(cfg["normal"], True)[..., :3]
# 창틀(검은 고무 · 기둥 덮개)은 법선을 편다 — 구운 법선은 창 둘레 틈에서 들쭉날쭉해서, 반들거리는 검은 띠에 하늘이 얼룩덜룩 비쳐
# 테두리가 찢긴 것처럼 보였다(11/10 Unity 근접 렌더)
nrm[cls == TRIM] = (0.5, 0.5, 1.0)
nd = down(nrm, f) * 2 - 1
del nrm
nd /= np.maximum(np.linalg.norm(nd, axis=2, keepdims=True), 1e-6)
save(nd * 0.5 + 0.5, "_normal.png", "PNG", True)
ccm = np.zeros(col.shape[:2] + (3,), np.float32)
ccm[..., 0] = box((cls == PAINT).astype(np.float32), 3)   # R = 클리어코트 세기(도장만), G = 클리어코트 매끈함
ccm[..., 1] = 1.0
save(down(ccm, f), "_clearcoat.png", "PNG", True)
del ccm
gt = np.zeros(glass_f.shape + (4,), np.float32)
gt[..., :3] = np.array(cfg.get("glass_tint_rgb", [18, 22, 26]), np.float32) / 255.0
gt[..., 3] = glass_f * cfg.get("glass_opacity", 0.55)
save(gt, "_glass.png", "PNG", False)
pal = np.array([[90, 90, 90], [235, 235, 235], [40, 90, 200], [60, 20, 90], [20, 20, 20], [70, 50, 30], [190, 190, 60],
                [0, 200, 200], [255, 240, 120], [230, 40, 40], [255, 140, 220], [120, 170, 120], [255, 120, 0]], np.float32) / 255.0
prev = pal[cls]
prev[~cover] = (0.0, 0.0, 0.0)
save(down(prev, RES // 1024), "_classes.png", "PNG", True)
del prev

# ---------------------------------------------------------------- 유리 메시 · 실내
def get_mat(n_):
    # 줄이기 스크립트가 남긴 같은 이름 재질이 있으면 그걸 쓴다 — 새로 만들면 '.001' 이 붙어 Unity 재질 연결이 어긋난다
    return bpy.data.materials.get(n_) or bpy.data.materials.new(n_)


body_mat = get_mat(NAME + "_Body")
glass_mat = get_mat(NAME + "_Glass")
int_mat = get_mat(NAME + "_Interior")
me.materials.clear()
me.materials.append(body_mat)
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv.active
uv = np.array([[l[uvl].uv[:] for l in fc.loops] for fc in bm.faces], np.float32)          # (F, 3, 2)
cen = np.array([fc.calc_center_median()[:] for fc in bm.faces], np.float64)
bm.free()
n = 8
W = np.array([(i / n, j / n, 1 - i / n - j / n) for i in range(n + 1) for j in range(n + 1 - i)], np.float32)
pts = np.einsum("kc,fcd->fkd", W, uv)
pxi = np.clip((pts[..., 0] % 1.0) * RES, 0, RES - 1).astype(np.int32)
pyi = np.clip((pts[..., 1] % 1.0) * RES, 0, RES - 1).astype(np.int32)
gd = box(gm, 2 * KS + 1) > 0.02                           # 조금 부풀린 유리 — 경계에 걸친 면을 놓치지 않게
touch = gd[pyi, pxi].any(axis=1)
log["glass_faces"] = int(touch.sum())
cx_m, cz_m = cen[:, 0] * SX * FS, (cen[:, 2] - mn[2]) * SZ
cab = (cx_m > gx0 - 0.20) & (cx_m < gx1 + 0.20) & (cz_m > cfg.get("shell_z0", 0.22))
log["shell_faces"] = int(cab.sum())


def sub_object(suffix, keep, material, flip):
    m2 = me.copy()
    o2 = bpy.data.objects.new(NAME + suffix, m2)
    bpy.context.scene.collection.objects.link(o2)
    b2 = bmesh.new()
    b2.from_mesh(m2)
    b2.faces.ensure_lookup_table()
    bmesh.ops.delete(b2, geom=[fc for i, fc in enumerate(b2.faces) if not keep[i]], context="FACES")
    if flip:
        bmesh.ops.reverse_faces(b2, faces=b2.faces[:])
    b2.to_mesh(m2)
    b2.free()
    m2.materials.clear()
    m2.materials.append(material)
    return o2


gl_ob = sub_object("_Glass", touch, glass_mat, False)
# 유리 법선은 창 평면의 법선 하나로 — 몸통에서 물려받은 법선은 창 아래 턱 쪽으로 기울어 있어, 유리 아래쪽에 하늘이 쐐기 모양으로 번쩍였다
gme = gl_ob.data
vco = np.empty(len(gme.vertices) * 3, np.float64)
gme.vertices.foreach_get("co", vco)
vco = vco.reshape(-1, 3)
real = np.stack([vco[:, 0] * SX * FS, vco[:, 1] * SY * FS, (vco[:, 2] - mn[2]) * SZ], axis=1)
best_d = np.full(len(real), 1e9)
vn = np.zeros((len(real), 3))
x_mid_ = (gx0 + gx1) * 0.5
for g_, (c_, n_) in pane_planes.items():
    d_ = np.abs((real - c_) @ n_)
    if g_ == "left":
        d_[real[:, 1] < 0.15] = 1e9
    elif g_ == "right":
        d_[real[:, 1] > -0.15] = 1e9
    elif g_ == "front":
        d_[real[:, 0] < x_mid_] = 1e9
    elif g_ == "rear":
        d_[real[:, 0] > x_mid_] = 1e9
    out_dir = {"left": (0, 1, 0), "right": (0, -1, 0), "front": (0.5, 0, 1), "rear": (-0.5, 0, 1)}.get(g_, (0, 0, 1))
    nn = n_ if float(np.dot(n_, out_dir)) > 0 else -n_
    nm = np.array([nn[0] * SX * FS, nn[1] * SY * FS, nn[2] * SZ])           # 진짜 m 의 평면 법선 → 모델 칸의 법선
    nm /= np.linalg.norm(nm)
    sel_ = d_ < best_d
    best_d[sel_] = d_[sel_]
    vn[sel_] = nm
if len(pane_planes):
    for an in ("custom_normal",):
        if an in gme.attributes:
            gme.attributes.remove(gme.attributes[an])
    gme.shade_smooth()
    gme.normals_split_custom_set_from_vertices([tuple(v_) for v_ in vn])
in_ob = sub_object("_Interior", cab, int_mat, True)
im = in_ob.data
for an in ("custom_normal", "sharp_edge", "sharp_face"):
    if an in im.attributes:
        im.attributes.remove(im.attributes[an])
im.shade_smooth()
# 안쪽 껍데기는 짙은 유리 너머로만 보인다 — 거칠게 줄여 삼각형을 아낀다
ratio = cfg.get("shell_ratio", 0.3)
if ratio < 1.0:
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = in_ob
    in_ob.select_set(True)
    dm = in_ob.modifiers.new("dec", "DECIMATE")
    dm.decimate_type = "COLLAPSE"
    dm.ratio = ratio
    dm.use_collapse_triangulate = True
    # 창 둘레(벨트라인 위)는 줄이지 않는다 — 껍데기도 몸통 텍스처의 알파로 창 구멍을 내는데, 줄이면 UV 가 틀어져
    # 반대쪽 창 구멍이 찢긴 종이처럼 보였다(11/10 미리보기). 아래쪽(문 안쪽 · 바닥 쪽)만 줄인다
    vg = in_ob.vertex_groups.new(name="low")
    z_keep = (belt - cfg.get("shell_keep_below_belt", 0.10)) / SZ + mn[2]
    vg.add([v_.index for v_ in im.vertices if v_.co.z < z_keep], 1.0, "REPLACE")
    dm.vertex_group = "low"
    bpy.ops.object.modifier_apply(modifier=dm.name)
    im = in_ob.data
# 접어 줄이면 면이 군데군데 뒤집힌다 — 바깥을 보는 면은 겉에서 검은 조각으로 보인다(Blender 미리보기에서 발견, 11/10).
# 껍데기는 몸통을 본뜬 것이니, 가장 가까운 몸통 면과 같은 쪽을 보는 면은 다시 뒤집어 모두 안쪽을 보게 한다
bb_ = bmesh.new()
bb_.from_mesh(me)
tree_ = BVHTree.FromBMesh(bb_)
bs_ = bmesh.new()
bs_.from_mesh(im)
bs_.faces.ensure_lookup_table()
bs_.normal_update()
wrong = []
for fc in bs_.faces:
    loc_, nor_, idx_, dist_ = tree_.find_nearest(fc.calc_center_median())
    if nor_ is not None and fc.normal.dot(nor_) > 0.0:
        wrong.append(fc)
if wrong:
    bmesh.ops.reverse_faces(bs_, faces=wrong)
bs_.normal_update()
# 껍데기를 몸통 겉면보다 1.2 cm 안쪽으로 들인다 — 줄인 뒤엔 군데군데 겉면 밖으로 1 cm 쯤 나와 있었다.
# Unity 는 뒷면을 안 그려 티가 안 나지만(그림자도 안 진다), 겉면 밖에 떠 있는 면을 남겨 둘 이유가 없다
inset = cfg.get("shell_inset", 0.012) / SY
before_n = [fc.normal.copy() for fc in bs_.faces]
shell_moved = 0


def shell_out(fc):
    for q_ in [fc.calc_center_median()] + [(e_.verts[0].co + e_.verts[1].co) * 0.5 for e_ in fc.edges]:
        r_ = tree_.find_nearest(q_)
        if r_[0] is not None and (q_ - r_[0]).dot(r_[1]) > 0.0005:
            return True
    return False


z_keep_ = (belt - cfg.get("shell_keep_below_belt", 0.10)) / SZ + mn[2]
thin = cfg.get("shell_inset_upper", 0.003) / SY                 # 창 둘레는 3 mm 만 — 창 구멍이 몸통과 어긋나지 않게
for round_ in range(6):
    targets = bs_.verts if round_ < 3 else {v_ for fc in bs_.faces if shell_out(fc) for v_ in fc.verts}
    for v_ in targets:
        upper_ = v_.co.z >= z_keep_
        if upper_ and round_ >= 1:
            continue
        depth = thin if upper_ else (inset if round_ < 3 else inset * (round_ - 1))   # 면 가운데가 아직 밖이면 더 깊이
        loc_, nor_, idx_, dist_ = tree_.find_nearest(v_.co)
        if loc_ is None:
            continue
        so_ = (v_.co - loc_).dot(nor_)
        if so_ > -depth:
            v_.co = v_.co - nor_ * (so_ + depth)
            shell_moved += 1
bs_.normal_update()
inverted = [fc for fc, n0_ in zip(bs_.faces, before_n) if fc.normal.dot(n0_) < 0.0]   # 옮기다 뒤집힌 가는 삼각형
if inverted:
    bmesh.ops.reverse_faces(bs_, faces=inverted)
dead_ = [fc for fc in bs_.faces if shell_out(fc)]              # 그래도 겉면 밖에 걸친 면은 뺀다
if dead_:
    bmesh.ops.delete(bs_, geom=dead_, context="FACES")
    loose_ = [v_ for v_ in bs_.verts if not v_.link_faces]
    if loose_:
        bmesh.ops.delete(bs_, geom=loose_, context="VERTS")
bs_.faces.ensure_lookup_table()
bs_.normal_update()
# 마지막 안전 검사 — 껍데기 면의 앞쪽(법선 쪽)에서 몸통에 가리지 않고 바깥이 보이면, 그 면은 겉에서 검은 조각으로 보인다(11/10
# 왼쪽 뒷문 창 아래 2 cm 띠: 꺾인 자리에서 껍데기가 몸통보다 1 mm 앞에 놓였다). 앞쪽 반구로 광선 여섯 개를 쏴 하나라도 빠져나가면 뺀다
exposed_ = []
for fc in bs_.faces:
    n__ = fc.normal
    if n__.length < 0.5:
        continue
    c__ = fc.calc_center_median() + n__ * 1e-4
    t1 = n__.orthogonal().normalized()
    t2 = n__.cross(t1)
    rays = [n__] + [(n__ * 0.5 + (t1 * math.cos(a__) + t2 * math.sin(a__)) * 0.866).normalized() for a__ in (0.0, 1.2566, 2.5133, 3.7699, 5.0265)]
    if any(tree_.ray_cast(c__, d__, 8.0)[0] is None for d__ in rays):
        exposed_.append(fc)
if exposed_:
    bmesh.ops.delete(bs_, geom=exposed_, context="FACES")
    loose_ = [v_ for v_ in bs_.verts if not v_.link_faces]
    if loose_:
        bmesh.ops.delete(bs_, geom=loose_, context="VERTS")
bs_.faces.ensure_lookup_table()
bs_.normal_update()
left_out = sum(1 for fc in bs_.faces if shell_out(fc))
bs_.to_mesh(im)
bs_.free()
bb_.free()
log["shell_fix"] = {"turned_inward": len(wrong), "inset_moves": shell_moved, "inverted_fixed": len(inverted), "removed_outside": len(dead_), "removed_exposed": len(exposed_), "faces_outside_body": left_out}

# 실내 — 좌석 · 대시보드 · 운전대 · 콘솔 · 격벽은 vehicle_interior.py 가 따로 만든다 (오브젝트 <Name>_Cabin, 재질 Vehicle_Cabin)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vehicle_interior
ic = cfg.get("interior", {})
iw = half_w - ic.get("door_thick", 0.24)                  # 실내 반폭 (거울 빼고 문 두께만큼 안쪽)
fz = ic.get("floor_z", 0.30)
z_ws = float(np.percentile(Z[gm & cover & (X > gx1 - 0.15)], 5))                      # 앞유리 아래 끝 높이
z_rg = float(np.percentile(Z[gm & cover & (X < gx0 + 0.15)], 5))                      # 뒷유리 아래 끝 높이
log["glass_base_z"] = {"front": round(z_ws, 3), "rear": round(z_rg, 3)}
cab_ob = vehicle_interior.build(NAME, me, dict(SX=SX, SY=SY, SZ=SZ, FS=FS, mn_z=float(mn[2]), gx0=gx0, gx1=gx1, belt=belt,
                                               z_ws=z_ws, z_rg=z_rg, iw=iw, fz=fz, half_w=half_w, z_top=z_top, cfg=ic), log)
vehicle_interior.write_palette(os.path.join(OUT, "Vehicle_CabinPalette.png"))


def tri_count(m):
    m.calc_loop_triangles()
    return len(m.loop_triangles)


log["tris"] = {"body": tri_count(me), "glass": tri_count(gl_ob.data), "shell": tri_count(in_ob.data), "cabin": tri_count(cab_ob.data)}
for o in bpy.context.view_layer.objects:
    o.select_set(False)
for o in (ob, gl_ob, in_ob, cab_ob):
    o.select_set(True)
bpy.context.view_layer.objects.active = ob
fbx_path = os.path.join(OUT, NAME + ".fbx")
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                         bake_space_transform=True, apply_unit_scale=True, mesh_smooth_type="FACE",
                         add_leaf_bones=False, path_mode="STRIP", use_mesh_modifiers=False, use_tspace=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + "_finished.blend"))
log["saved"] = saved
json.dump(log, open(os.path.join(OUT, "finish_log.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("FINISH_DONE", json.dumps({k: v for k, v in log.items() if k != "saved"}, ensure_ascii=False))
