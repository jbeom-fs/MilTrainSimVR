# -*- coding: utf-8 -*-
"""C 플레이어 방탄복(플레이트 캐리어) 모델 — 2026-10-29

블렌더 5.2 를 화면 없이 돌려 만든다 (모델 폴더 = 이 파일이 있는 Source~ 의 한 단계 위):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" --background --factory-startup --python build_plate_carrier.py

만드는 것 (모두 Assets/C/Models/PlateCarrier 안):
  C_PlateCarrier.fbx           방탄복 + 그 밑 몸통(셔츠). 재질마다 메시 하나:
                               Vest_Cloth(천) · Vest_Webbing(띠) · Vest_Elastic(고무줄) · Vest_Patch(벨크로) · Vest_Inside(주머니 안) · Shirt(몸통)
  C_PlateCarrier_Layout.json   탄창 주머니 3개의 자리 — 생성기(CombatAssetsBuilder)가 여기에 탄창 6칸을 꽂는다
  Textures/C_Cordura_N.png     천 결 노멀 맵 (UV 1 = 5cm 마다 되풀이)
  Source~/C_PlateCarrier.blend 손으로 고쳐 볼 때 (유니티는 ~ 로 끝나는 폴더를 읽지 않는다)

좌표는 유니티 기준(오른쪽 +x · 위 +y · 앞 +z, m)으로 적고 U() 로 블렌더 좌표로 바꾼다.
원점 = 플레이어 몸 판정(PlayerHitboxes)의 가슴 상자 가운데 — 상자 0.40 × 0.32 × 0.24m, 윗면(y 0.16) = 어깨선.
왼쪽·오른쪽이 대칭이라 좌우가 뒤집혀도 같은 모양이다.
"""
import json
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def model_dir():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args:
        return os.path.abspath(args[0])
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def U(x, y, z):
    """유니티 좌표 → 블렌더 좌표. FBX 를 기본 축(앞 −Z · 위 Y)으로 변환을 구워 내보내면 유니티에서 다시 (x, y, z) 가 된다"""
    return Vector((-x, -z, y))


def from_blender(v):
    return Vector((-v.x, v.z, -v.y))


# ---------------------------------------------------------------- 치수 (유니티 좌표, m)

# 몸통(셔츠) — 높이마다 단면 반폭 a · 앞 반두께 bf · 뒤 반두께 bb. 단면은 둥근 사각형(초타원, 지수 2.6). 가슴 상자보다 조금 작다.
# 윗가슴은 목 쪽으로 눕고, 어깨(y 0.16 안팎)는 낮고 판판하게, 목은 짧게 뒤쪽에 — 내려다볼 때 눈 바로 밑에 큰 둥근 덩어리가 보이지 않게
TORSO_RINGS = [
    (-0.200, 0.158, 0.108, 0.100),
    (-0.120, 0.164, 0.113, 0.104),
    (-0.030, 0.170, 0.116, 0.108),
    (0.040, 0.174, 0.113, 0.110),
    (0.090, 0.175, 0.104, 0.110),
    (0.125, 0.172, 0.090, 0.107),
    (0.148, 0.164, 0.076, 0.100),
    (0.163, 0.146, 0.064, 0.090),
    (0.174, 0.112, 0.052, 0.076),
    (0.182, 0.076, 0.044, 0.060),
    (0.190, 0.058, 0.040, 0.050),
    (0.225, 0.054, 0.036, 0.048),
]
TORSO_EXP = 2.6
TORSO_SEG = 48

# 앞판·뒤판 (판을 넣은 천 주머니) — 폭 26cm · 높이 32cm, 위 두 모서리는 사수 절단(어깨를 쓰기 좋게 비스듬히 깎음)
PLATE_W = 0.130                      # 반폭
PLATE_Y0, PLATE_Y1 = -0.185, 0.135   # 아래 · 위 (위 = 목 아래 오목한 곳 높이)
CUT_X, CUT_Y = 0.075, 0.065          # 절단선: (±0.075, 0.135) → (±0.130, 0.065)
PLATE_IN_Z, PLATE_OUT_Z = 0.100, 0.135   # 앞판 안쪽·바깥 면 (두께 3.5cm, 안쪽은 몸통에 조금 묻힌다)
PLATE_R = 0.45                       # 판이 몸을 따라 휘는 반지름 — 가장자리가 x²/2R 만큼 뒤로
PLATE_CORNER = 0.025                 # 아래 모서리 둥글기

# 탄창 주머니 — 두 개씩 앞뒤로 들어가는 주머니 3개, 위가 열려 있다. 앞판 겉면에 붙고 판을 따라 조금씩 바깥을 본다
POUCH_X = (-0.088, 0.0, 0.088)       # 가운데 x
POUCH_Y = -0.160                     # 바닥
POUCH_W = 0.086                      # 폭 — 휜 탄창을 7° 기울여 꽂으면 들어가는 부분 폭 7.6cm
POUCH_D = 0.0766                     # 두께 — 탄창(3.33cm) 둘 + 사이 2mm + 벽
POUCH_HB, POUCH_HF = 0.110, 0.095    # 뒷벽·앞벽 높이 (앞이 낮아 꺼내기 쉽다)
WALL = 0.003
ROWS = (WALL + 0.001 + 0.01665, WALL + 0.001 + 0.0333 + 0.002 + 0.01665)   # 주머니 안 탄창 두 줄의 가운데 (주머니 기준 깊이)

# 어깨끈 · 옆구리 띠(커머번드)
STRAP_X, STRAP_W, STRAP_T = 0.085, 0.048, 0.012
BAND_Y0, BAND_Y1, BAND_T, BAND_END_X = -0.180, -0.040, 0.007, 0.112

TILE = 0.05   # UV 1 = 5cm


# ---------------------------------------------------------------- 도구

def new_material(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1.0)
    return m


MAT = {
    "Cloth": new_material("C_VestCloth", (0.30, 0.32, 0.20)),
    "Webbing": new_material("C_VestWebbing", (0.24, 0.26, 0.16)),
    "Elastic": new_material("C_VestElastic", (0.07, 0.07, 0.07)),
    "Patch": new_material("C_VestPatch", (0.27, 0.29, 0.19)),
    "Inside": new_material("C_VestInside", (0.10, 0.11, 0.07)),
    "Shirt": new_material("C_Shirt", (0.22, 0.23, 0.18)),
}


def finish(bm, name, materials, recalc=True):
    """bmesh → 씬의 오브젝트 (재질 칸은 materials 순서, 면마다 material_index).
    recalc = 닫힌 덩어리의 면을 모두 바깥으로 돌린다 — 재질별로 떼어 낸 열린 조각(주머니 안쪽 벽)엔 쓰지 않는다(안쪽을 봐야 한다)"""
    if recalc:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in materials:
        me.materials.append(m)
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def apply_modifiers(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old = obj.data
    obj.modifiers.clear()
    obj.data = me
    bpy.data.meshes.remove(old)


def bevel(obj, width, segments=2, angle=40.0):
    """모서리를 둥글게 — 천에 솜을 넣은 듯 부드럽게. 거의 평평한 이음(휜 면의 격자)은 그대로 둔다"""
    m = obj.modifiers.new("Bevel", 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(angle)
    m.use_clamp_overlap = True
    apply_modifiers(obj)


def slab(bm, outer, inner, mat=0):
    """두께 있는 판 — outer[i][k] · inner[i][k] 는 같은 격자(열 i · 줄 k)의 바깥·안쪽 점(유니티 좌표). 테두리도 막는다"""
    vo = [[bm.verts.new(U(*p)) for p in col] for col in outer]
    vi = [[bm.verts.new(U(*p)) for p in col] for col in inner]
    nc, nr = len(outer), len(outer[0])
    faces = []
    for i in range(nc - 1):
        for k in range(nr - 1):
            faces.append(bm.faces.new((vo[i][k], vo[i + 1][k], vo[i + 1][k + 1], vo[i][k + 1])))
            faces.append(bm.faces.new((vi[i][k + 1], vi[i + 1][k + 1], vi[i + 1][k], vi[i][k])))
    for i in range(nc - 1):
        faces.append(bm.faces.new((vo[i][0], vi[i][0], vi[i + 1][0], vo[i + 1][0])))
        faces.append(bm.faces.new((vo[i + 1][-1], vi[i + 1][-1], vi[i][-1], vo[i][-1])))
    for k in range(nr - 1):
        faces.append(bm.faces.new((vo[0][k + 1], vi[0][k + 1], vi[0][k], vo[0][k])))
        faces.append(bm.faces.new((vo[-1][k], vi[-1][k], vi[-1][k + 1], vo[-1][k + 1])))
    for f in faces:
        f.material_index = mat
    return faces


def box(bm, to_world, x0, x1, y0, y1, z0, z1, mat=0):
    """자기 좌표의 상자 — to_world(x, y, z) 가 유니티 좌표로 옮긴다"""
    c = [bm.verts.new(U(*to_world(x, y, z))) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    quads = ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3))
    for q in quads:
        bm.faces.new([c[i] for i in q]).material_index = mat


def arc_xs(x_edge, r, steps=6):
    """둥근 모서리 쪽 x 들 — 모서리 원(반지름 r)을 고르게 따라가게"""
    return [x_edge - r + r * math.sin(k * math.pi / (2 * steps)) for k in range(1, steps + 1)]


def column_grid(xs, bottom, top, rows):
    return [[(x, bottom(x) + (top(x) - bottom(x)) * k / rows) for k in range(rows + 1)] for x in xs]


def rounded_bottom(x, half_w, y0, r):
    d = abs(x) - (half_w - r)
    return y0 if d <= 0 else y0 + r - math.sqrt(max(r * r - d * d, 0.0))


def rounded_top(x, half_w, y1, r):
    d = abs(x) - (half_w - r)
    return y1 if d <= 0 else y1 - r + math.sqrt(max(r * r - d * d, 0.0))


def symmetric_xs(half_w, n_mid, arc_r, extra=()):
    side = list(np.linspace(0.0, half_w - arc_r, n_mid)) + arc_xs(half_w, arc_r) + [abs(e) for e in extra]
    xs = sorted(set(round(s * v, 6) for v in side for s in (-1.0, 1.0)))
    return xs


# ---------------------------------------------------------------- 몸통 (셔츠)

def superellipse(a, bf, bb, t, n=TORSO_EXP):
    """둥근 사각형 단면 위의 점 — t = 0 이 오른쪽(+x), π/2 가 앞(+z). 앞뒤 반두께가 다르다"""
    c, s = math.cos(t), math.sin(t)
    b = bf if s >= 0 else bb
    return a * math.copysign(abs(c) ** (2.0 / n), c), b * math.copysign(abs(s) ** (2.0 / n), s)


def superellipse_normal(a, bf, bb, x, z, n=TORSO_EXP):
    b = bf if z >= 0 else bb
    gx = (n / a) * (abs(x / a) ** (n - 1)) * math.copysign(1.0, x)
    gz = (n / b) * (abs(z / b) ** (n - 1)) * math.copysign(1.0, z)
    l = math.hypot(gx, gz) or 1.0
    return gx / l, gz / l


def ring_ab(y):
    """높이 y 의 단면 (반폭, 앞 반두께, 뒤 반두께) — 고리 사이는 직선으로 잇는다"""
    rs = TORSO_RINGS
    if y <= rs[0][0]:
        return rs[0][1:]
    for r0, r1 in zip(rs, rs[1:]):
        if y <= r1[0]:
            t = (y - r0[0]) / (r1[0] - r0[0])
            return tuple(v0 + (v1 - v0) * t for v0, v1 in zip(r0[1:], r1[1:]))
    return rs[-1][1:]


def build_torso():
    bm = bmesh.new()
    rings = []
    for (y, a, bf, bb) in TORSO_RINGS:
        ring = []
        for i in range(TORSO_SEG):
            x, z = superellipse(a, bf, bb, 2 * math.pi * i / TORSO_SEG)
            ring.append(bm.verts.new(U(x, y, z)))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(TORSO_SEG):
            j = (i + 1) % TORSO_SEG
            bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
    bottom = bm.verts.new(U(0.0, TORSO_RINGS[0][0], (TORSO_RINGS[0][2] - TORSO_RINGS[0][3]) * 0.5))
    top = bm.verts.new(U(0.0, TORSO_RINGS[-1][0] + 0.004, (TORSO_RINGS[-1][2] - TORSO_RINGS[-1][3]) * 0.5))
    for i in range(TORSO_SEG):
        j = (i + 1) % TORSO_SEG
        bm.faces.new((rings[0][j], rings[0][i], bottom))
        bm.faces.new((rings[-1][i], rings[-1][j], top))
    for f in bm.faces:
        f.smooth = True
    return finish(bm, "Shirt_Torso", [MAT["Shirt"]])


# ---------------------------------------------------------------- 앞판 · 뒤판

def plate_top(x):
    ax = abs(x)
    if ax <= CUT_X:
        return PLATE_Y1
    return PLATE_Y1 - (ax - CUT_X) * (PLATE_Y1 - CUT_Y) / (PLATE_W - CUT_X)


def plate_bottom(x):
    return rounded_bottom(x, PLATE_W, PLATE_Y0, PLATE_CORNER)


def plate_out_z(x):
    return PLATE_OUT_Z - x * x / (2 * PLATE_R)


def plate_in_z(x):
    return PLATE_IN_Z - x * x / (2 * PLATE_R)


def on_plate(x, y, out):
    """앞판 겉면의 (x, y) 에서 겉면 법선 쪽으로 out 만큼 — 판에 붙이는 띠·주머니 자리"""
    nx = x / PLATE_R
    l = math.sqrt(nx * nx + 1.0)
    return (x + nx / l * out, y, plate_out_z(x) + out / l)


def build_plate(name, sign):
    """sign +1 = 앞판, −1 = 뒤판 (앞뒤로 뒤집은 같은 모양)"""
    xs = symmetric_xs(PLATE_W, 14, PLATE_CORNER, extra=(CUT_X,))
    cols = column_grid(xs, plate_bottom, plate_top, 18)
    outer = [[(x, y, sign * plate_out_z(x)) for (x, y) in col] for col in cols]
    inner = [[(x, y, sign * plate_in_z(x)) for (x, y) in col] for col in cols]
    bm = bmesh.new()
    slab(bm, outer, inner)
    obj = finish(bm, name, [MAT["Cloth"]])
    bevel(obj, 0.007, segments=3)
    return obj


def surface_patch(name, mat, x0, x1, y0, y1, thick, corner, n_mid=4):
    """앞판 겉면에 붙는 얇은 판 (벨크로 · 띠 한 칸) — 판의 휜 면을 따라간다"""
    cx, hw = (x0 + x1) * 0.5, (x1 - x0) * 0.5
    xs = [cx + v for v in symmetric_xs(hw, n_mid, corner)] if corner > 0 else list(np.linspace(x0, x1, n_mid + 1))
    bottom = (lambda x: rounded_bottom(x - cx, hw, y0, corner)) if corner > 0 else (lambda x: y0)
    top = (lambda x: rounded_top(x - cx, hw, y1, corner)) if corner > 0 else (lambda x: y1)
    cols = column_grid(xs, bottom, top, 1)
    outer = [[on_plate(x, y, thick) for (x, y) in col] for col in cols]
    inner = [[on_plate(x, y, -0.002) for (x, y) in col] for col in cols]
    bm = bmesh.new()
    slab(bm, outer, inner)
    obj = finish(bm, name, [mat])
    bevel(obj, min(0.0008, thick * 0.4), segments=1)
    return obj


def build_front_details():
    objs = []
    # 몰리 띠 두 줄 (2.5cm 띠, 3.8cm 마다 박음질 — 칸 사이 3mm 틈으로 보인다)
    for (y0, y1) in ((-0.040, -0.015), (0.010, 0.035)):
        for k in range(6):
            c = (k - 2.5) * 0.038
            objs.append(surface_patch(f"Molle_{y0:+.3f}_{k}", MAT["Webbing"], c - 0.0175, c + 0.0175, y0, y1, 0.0025, 0.0, n_mid=3))
    # 이름표 자리 벨크로
    objs.append(surface_patch("Patch", MAT["Patch"], -0.065, 0.065, 0.060, 0.117, 0.002, 0.008))
    return objs


# ---------------------------------------------------------------- 탄창 주머니

def pouch_frames():
    """주머니마다 (뒤 아래 가운데 점, y 축 둘레 각도[라디안, 앞 +z 가 +x 쪽으로]) — 앞판 겉면에 2mm 묻혀 붙인다"""
    frames = []
    for x in POUCH_X:
        yaw = math.atan2(x / PLATE_R, 1.0)
        base = on_plate(x, POUCH_Y, -0.002)
        frames.append((base, yaw))
    return frames


def pouch_to_world(base, yaw):
    c, s = math.cos(yaw), math.sin(yaw)

    def f(lx, ly, lz):
        return (base[0] + lx * c + lz * s, base[1] + ly, base[2] - lx * s + lz * c)
    return f


def build_pouch(name, base, yaw):
    """위가 열린 주머니 — 바깥 벽·테두리는 천, 안쪽 벽은 어두운 천(Inside). 뒷벽이 높고 앞벽이 낮다"""
    w = POUCH_W * 0.5
    d, t = POUCH_D, WALL

    def h(z):
        return POUCH_HB + (POUCH_HF - POUCH_HB) * (z / d)
    to_world = pouch_to_world(base, yaw)
    bm = bmesh.new()

    def v(x, y, z):
        return bm.verts.new(U(*to_world(x, y, z)))
    # 바깥
    ob = [v(-w, 0, 0), v(w, 0, 0), v(w, 0, d), v(-w, 0, d)]
    ot = [v(-w, h(0), 0), v(w, h(0), 0), v(w, h(d), d), v(-w, h(d), d)]
    # 안쪽
    wi = w - t
    ib = [v(-wi, t, t), v(wi, t, t), v(wi, t, d - t), v(-wi, t, d - t)]
    it = [v(-wi, h(t), t), v(wi, h(t), t), v(wi, h(d - t), d - t), v(-wi, h(d - t), d - t)]
    faces = []
    faces.append((bm.faces.new((ob[0], ob[1], ob[2], ob[3])), 0))
    for i in range(4):
        j = (i + 1) % 4
        faces.append((bm.faces.new((ob[i], ob[j], ot[j], ot[i])), 0))      # 바깥 벽
        faces.append((bm.faces.new((ib[j], ib[i], it[i], it[j])), 1))      # 안쪽 벽
        faces.append((bm.faces.new((ot[i], ot[j], it[j], it[i])), 0))      # 윗 테두리
    faces.append((bm.faces.new((ib[3], ib[2], ib[1], ib[0])), 1))
    for f, m in faces:
        f.material_index = m
    # 앞벽 위 고무줄 · 앞면 몰리 띠 한 줄
    box(bm, to_world, -w + 0.004, w - 0.004, h(d) - 0.016, h(d) - 0.002, d - 0.0005, d + 0.0022, mat=2)
    box(bm, to_world, -w + 0.002, w - 0.002, 0.034, 0.059, d - 0.0005, d + 0.0022, mat=3)
    obj = finish(bm, name, [MAT["Cloth"], MAT["Inside"], MAT["Elastic"], MAT["Webbing"]])
    bevel(obj, 0.0018, segments=2)
    return obj


# ---------------------------------------------------------------- 어깨끈 · 옆구리 띠

def sweep(name, mat, centers, normals, width, thick):
    """가운데 선을 따라 납작한 띠 — normals = 두께 방향(겉면 법선)"""
    n = len(centers)
    outer, inner = [], []
    for i in range(n):
        p = centers[i]
        tan = (centers[min(i + 1, n - 1)] - centers[max(i - 1, 0)]).normalized()
        nrm = normals[i] - tan * normals[i].dot(tan)
        nrm.normalize()
        side = tan.cross(nrm).normalized()
        col_o, col_i = [], []
        for s in (-0.5, 0.5):
            col_o.append(tuple(p + side * (s * width) + nrm * (thick * 0.5)))
            col_i.append(tuple(p + side * (s * width) - nrm * (thick * 0.5)))
        outer.append(col_o)
        inner.append(col_i)
    bm = bmesh.new()
    slab(bm, outer, inner)
    obj = finish(bm, name, [mat])
    bevel(obj, min(0.003, thick * 0.3), segments=2)
    return obj


def torso_bvh(torso):
    bm = bmesh.new()
    bm.from_mesh(torso.data)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


def smooth(points, passes=3):
    pts = [p.copy() for p in points]
    for _ in range(passes):
        pts = [pts[0]] + [(pts[i - 1] + pts[i] * 2.0 + pts[i + 1]) * 0.25 for i in range(1, len(pts) - 1)] + [pts[-1]]
    return pts


def build_straps(tree):
    objs = []
    for sx in (-STRAP_X, STRAP_X):
        origin = Vector((sx, 0.10, 0.0))
        centers, normals = [], []
        for k in range(49):
            phi = math.radians(3.0 + 174.0 * k / 48)
            d = Vector((0.0, math.sin(phi), math.cos(phi)))
            hit, nrm, _, _ = tree.ray_cast(U(*origin), U(*d))
            if hit is None:
                continue
            p, n = from_blender(hit), from_blender(nrm).normalized()
            centers.append(p + n * (0.004 + STRAP_T * 0.5))
            normals.append(n)
        objs.append(sweep(f"Strap_{'L' if sx < 0 else 'R'}", MAT["Cloth"], smooth(centers), smooth(normals), STRAP_W, STRAP_T))
    return objs


def build_bands():
    """옆구리 띠 — 앞판 옆 가장자리 밑에서 옆구리를 돌아 뒤판 밑까지. 몸통 단면을 따라 위아래로 조금 좁아진다"""
    objs = []
    rows = 4
    for side in (-1.0, 1.0):
        # 띠 끝 = 몸통 단면에서 |x| = BAND_END_X 인 곳 (가운데 높이 기준)
        a_mid = ring_ab((BAND_Y0 + BAND_Y1) * 0.5)[0]
        t_end = math.acos(min(1.0, (BAND_END_X / a_mid) ** (TORSO_EXP / 2.0)))
        ts = list(np.linspace(-t_end, t_end, 25))
        outer, inner = [], []
        for t in ts:
            col_o, col_i = [], []
            for k in range(rows + 1):
                y = BAND_Y0 + (BAND_Y1 - BAND_Y0) * k / rows
                a, bf, bb = ring_ab(y)
                x, z = superellipse(a, bf, bb, t)
                nx, nz = superellipse_normal(a, bf, bb, x, z)
                x *= side
                nx *= side
                col_o.append((x + nx * (0.001 + BAND_T), y, z + nz * (0.001 + BAND_T)))
                col_i.append((x + nx * 0.001, y, z + nz * 0.001))
            outer.append(col_o)
            inner.append(col_i)
        bm = bmesh.new()
        slab(bm, outer, inner)
        obj = finish(bm, f"Band_{'L' if side < 0 else 'R'}", [MAT["Webbing"]])
        bevel(obj, 0.002, segments=1)
        objs.append(obj)
    return objs


# ---------------------------------------------------------------- 재질별로 합치기 · UV · 내보내기

OUTPUT_NAMES = {
    "C_VestCloth": "Vest_Cloth",
    "C_VestWebbing": "Vest_Webbing",
    "C_VestElastic": "Vest_Elastic",
    "C_VestPatch": "Vest_Patch",
    "C_VestInside": "Vest_Inside",
    "C_Shirt": "Shirt",
}


def merge_by_material(objs):
    groups = {}
    for o in objs:
        src = bmesh.new()
        src.from_mesh(o.data)
        for mi, mat in enumerate(o.data.materials):
            dst = groups.setdefault(mat.name, bmesh.new())
            vmap = {}
            for f in src.faces:
                if f.material_index != mi:
                    continue
                vs = []
                for vert in f.verts:
                    if vert not in vmap:
                        vmap[vert] = dst.verts.new(vert.co)
                    vs.append(vmap[vert])
                nf = dst.faces.new(vs)
                nf.smooth = True
        src.free()
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    merged = []
    for mat_name, bm in groups.items():
        uv = bm.loops.layers.uv.new("UVMap")
        bm.normal_update()
        for f in bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for loop in f.loops:
                c = loop.vert.co
                u, w = ((c.y, c.z), (c.x, c.z), (c.x, c.y))[ax]
                loop[uv].uv = (u / TILE, w / TILE)
        name = OUTPUT_NAMES[mat_name]
        merged.append(finish(bm, name, [bpy.data.materials[mat_name]], recalc=False))
    return merged


def make_cordura(path, n=512):
    """천 결 노멀 맵 — 1mm 평직 + 넓은 주름. 가로세로 정수 번 되풀이라 이음매가 없다
    (5mm 립스탑 격자도 넣어 봤는데 내려다보면 모눈종이처럼 보여 뺐다)"""
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float64) / n
    h = 0.30 * np.sin(2 * np.pi * 50 * xx) * np.sin(2 * np.pi * 50 * yy)
    rng = np.random.default_rng(1029)
    for _ in range(14):
        kx, ky = (int(v) for v in rng.integers(-5, 6, size=2))
        if kx == 0 and ky == 0:
            continue
        h += 0.10 * np.sin(2 * np.pi * (kx * xx + ky * yy) + rng.uniform(0.0, 2 * np.pi))
    gx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    gy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    s = 0.45 / (np.percentile(np.hypot(gx, gy), 95) + 1e-9)
    nx, ny, nz = -gx * s, -gy * s, np.ones_like(h)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    rgb = np.stack([nx / length, ny / length, nz / length], axis=-1) * 0.5 + 0.5
    rgba = np.concatenate([rgb, np.ones((n, n, 1))], axis=-1).astype(np.float32)
    img = bpy.data.images.new("C_Cordura_N", n, n, alpha=False)
    img.colorspace_settings.name = 'Non-Color'
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()


def main():
    out = model_dir()
    os.makedirs(os.path.join(out, "Textures"), exist_ok=True)
    os.makedirs(os.path.join(out, "Source~"), exist_ok=True)
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    torso = build_torso()
    tree = torso_bvh(torso)
    parts = [torso, build_plate("Plate_Front", 1.0), build_plate("Plate_Back", -1.0)]
    parts += build_front_details()
    for i, (base, yaw) in enumerate(pouch_frames()):
        parts.append(build_pouch(f"Pouch_{i}", base, yaw))
    parts += build_straps(tree)
    parts += build_bands()
    merged = merge_by_material(parts)

    bpy.ops.object.select_all(action='DESELECT')
    tris = 0
    for o in merged:
        o.select_set(True)
        tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
    bpy.context.view_layer.objects.active = merged[0]
    fbx = os.path.join(out, "C_PlateCarrier.fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')

    layout = {
        "note": "build_plate_carrier.py 가 만든다. 좌표 = 유니티, 방탄복(가슴 상자 가운데) 기준, m. pouches = 주머니 뒤 아래 가운데 · y 축 둘레 각도(도)",
        "pouches": [{"x": b[0], "y": b[1], "z": b[2], "yaw": math.degrees(yaw)} for (b, yaw) in pouch_frames()],
        "width": POUCH_W, "depth": POUCH_D, "wall": WALL, "heightBack": POUCH_HB, "heightFront": POUCH_HF,
        "rows": list(ROWS),
    }
    with open(os.path.join(out, "C_PlateCarrier_Layout.json"), "w", encoding="utf-8") as fp:
        json.dump(layout, fp, ensure_ascii=False, indent=2)

    make_cordura(os.path.join(out, "Textures", "C_Cordura_N.png"))
    bpy.context.preferences.filepaths.save_version = 0   # 덮어쓸 때 .blend1 백업을 만들지 않는다
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "Source~", "C_PlateCarrier.blend"), check_existing=False)
    print(f"PLATE_CARRIER ok — meshes {[o.name for o in merged]} tris {tris} → {fbx}")


main()
