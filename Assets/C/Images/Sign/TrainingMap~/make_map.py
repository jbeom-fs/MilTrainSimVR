# -*- coding: utf-8 -*-
"""안내판 지도 만들기 — 집결지 안내판(C_StartBackdrop/집결지 · 총 탁자 둘레/안내판)에 붙는 C_UrbanBlock 지도.

  map_scene.json   씬에서 내보낸 맵 배치 (export_scene.cs 로 다시 뽑는다 — 건물 · 땅 · 지하도 · 점령지 · 시작 자리)
  map_layers.json  손으로 고치는 것 — 제목, 보이는 범위, 경로, 표시(입구 · 정문 · 목표 · 현위치 · 차량 · 보급 …)

  python make_map.py            → C_TrainingMap.svg / .html / .png (이 폴더, 시안)
  python make_map.py --install  → 위 + Assets/C/Images/Sign/C_TrainingMap.png 로 복사 (안내판이 쓰는 텍스처)

2000~2010년대 길거리 안내도 느낌 (사용자 11/17): 남색 광택 제목 띠 · 노란 줄, 파스텔 블록 건물에 입체 그림자,
빨간 '현위치' 말풍선, 굵은 경로 화살표, 굴림 · 맑은 고딕, 볕에 바랜 색 · 잔 흠집. PNG 는 Edge(헤드리스)로 SVG 를 찍는다.
적 위치 · 순찰로 · 적 수는 그리지 않는다.
"""
import json
import math
import random
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ASSET_PNG = HERE.parent / "C_TrainingMap.png"
EDGE = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]

scene = json.loads((HERE / "map_scene.json").read_text(encoding="utf-8"))
layers = json.loads((HERE / "map_layers.json").read_text(encoding="utf-8"))

# ── 판 배치 (px) ─────────────────────────────────────────────
V = layers["view"]
PPM = V["ppm"]
X0, X1, Z0, Z1 = V["x0"], V["x1"], V["z0"], V["z1"]
MAP_W, MAP_H = round((X1 - X0) * PPM), round((Z1 - Z0) * PPM)
SIDE, HEADER, GAP, BOTTOM = 36, 222, 42, 36
MX, MY = SIDE, HEADER + GAP
W, H = MAP_W + SIDE * 2, MY + MAP_H + BOTTOM

ROUTE_W, CASING_W, ROUTE_OFFSET = 13, 21, 0.75 * PPM

FONT_TITLE = "'Noto Sans KR', 'Malgun Gothic', sans-serif"
FONT_LABEL = "'Malgun Gothic', 'Gulim', sans-serif"
FONT_OLD = "'Gulim', 'Malgun Gothic', sans-serif"


def P(x, z):
    return MX + (x - X0) * PPM, MY + (Z1 - z) * PPM


def f(v):
    return f"{v:.1f}"


def esc(s):
    return str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def box(item, **attrs):
    """씬 상자(가운데 x·z, yaw, 가로 sx · 세로 sz) → 회전한 <rect>"""
    cx, cy = P(item["x"], item["z"])
    w, h = item["sx"] * PPM, item["sz"] * PPM
    a = " ".join(f'{k.replace("_", "-")}="{v}"' for k, v in attrs.items())
    yaw = item.get("yaw", 0) % 360
    rot = f' transform="rotate({f(yaw)} {f(cx)} {f(cy)})"' if abs(yaw) > 0.01 else ""
    return f'<rect x="{f(cx - w / 2)}" y="{f(cy - h / 2)}" width="{f(w)}" height="{f(h)}"{rot} {a}/>'


def shade(hex_color, k):
    c = hex_color.lstrip("#")
    r, g, b = (int(c[i:i + 2], 16) for i in (0, 2, 4))
    return "#%02x%02x%02x" % tuple(max(0, min(255, round(v * k))) for v in (r, g, b))


def offset_line(pts, d):
    """진행 방향 오른쪽(+) · 왼쪽(-)으로 d px 비킨 꺾은선 (모서리는 마이터)"""
    if abs(d) < 1e-6 or len(pts) < 2:
        return pts
    normals = []
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        dx, dy = bx - ax, by - ay
        n = math.hypot(dx, dy) or 1
        normals.append((-dy / n, dx / n))
    out = []
    for i, (px, py) in enumerate(pts):
        if i == 0:
            nx, ny = normals[0]
            out.append((px + nx * d, py + ny * d))
        elif i == len(pts) - 1:
            nx, ny = normals[-1]
            out.append((px + nx * d, py + ny * d))
        else:
            (n1x, n1y), (n2x, n2y) = normals[i - 1], normals[i]
            mx, my = n1x + n2x, n1y + n2y
            m = math.hypot(mx, my) or 1
            mx, my = mx / m, my / m
            k = d / max(0.3, mx * n1x + my * n1y)
            out.append((px + mx * k, py + my * k))
    return out


def poly(pts):
    return " ".join(f"{f(x)},{f(y)}" for x, y in pts)


def walk(pts, step, first):
    """꺾은선을 따라 step 마다 (x, y, 각도)"""
    out, carry = [], first
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        seg = math.hypot(bx - ax, by - ay)
        ang = math.degrees(math.atan2(by - ay, bx - ax))
        t = carry
        while t < seg:
            out.append((ax + (bx - ax) * t / seg, ay + (by - ay) * t / seg, ang))
            t += step
        carry = t - seg
    return out


# ── 그림 조각 ────────────────────────────────────────────────
PASTEL = ["#ecd3c3", "#d8e4c6", "#d2dde9", "#efe3b8", "#e3d5e6"]


def building_color(name):
    return PASTEL[sum(ord(c) for c in name) % len(PASTEL)]


def stair_glyph(cx, cy, s, color="#ffffff"):
    # 계단 세 칸 (지하도 입구 표지)
    u = s / 6
    pts = [(-3, 3), (-3, 1), (-1, 1), (-1, -1), (1, -1), (1, -3), (3, -3), (3, 3)]
    d = " ".join(f"{f(cx + x * u)},{f(cy + y * u)}" for x, y in pts)
    return f'<polygon points="{d}" fill="{color}"/>'


def entrance_icon(cx, cy, size=46):
    r = size / 2
    return (f'<rect x="{f(cx - r - 3)}" y="{f(cy - r - 3)}" width="{f(size + 6)}" height="{f(size + 6)}" rx="9" fill="#ffffff"/>'
            f'<rect x="{f(cx - r)}" y="{f(cy - r)}" width="{f(size)}" height="{f(size)}" rx="7" fill="url(#gEntrance)"/>'
            + stair_glyph(cx, cy + 1, size * 0.62))


def star(cx, cy, r, fill, stroke="none", sw=0):
    pts = []
    for i in range(10):
        rr = r if i % 2 == 0 else r * 0.42
        a = math.radians(-90 + i * 36)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    return f'<polygon points="{poly(pts)}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" stroke-linejoin="round"/>'


def pill(cx, cy, text, fill, color="#ffffff", size=34, font=FONT_LABEL, stroke="#ffffff", pad=18):
    w = len(text) * size * 0.98 + pad * 2
    h = size * 1.55
    return (f'<g filter="url(#fDrop)"><rect x="{f(cx - w / 2)}" y="{f(cy - h / 2)}" width="{f(w)}" height="{f(h)}" rx="{f(h / 2)}" '
            f'fill="{fill}" stroke="{stroke}" stroke-width="4"/></g>'
            f'<text x="{f(cx)}" y="{f(cy + size * 0.36)}" font-family="{font}" font-weight="700" font-size="{size}" '
            f'fill="{color}" text-anchor="middle">{esc(text)}</text>')


def tag_box(cx, cy, text, color, size=30):
    w = len(text) * size * 0.98 + 22
    h = size * 1.5
    return (f'<rect x="{f(cx - w / 2)}" y="{f(cy - h / 2)}" width="{f(w)}" height="{f(h)}" rx="5" fill="#ffffff" stroke="{color}" stroke-width="3"/>'
            f'<text x="{f(cx)}" y="{f(cy + size * 0.36)}" font-family="{FONT_OLD}" font-weight="700" font-size="{size}" fill="{color}" text-anchor="middle">{esc(text)}</text>')


def vehicle_icon(cx, cy, yaw=0):
    # 차량 (위에서 본 상자 + 앞유리) — 지도 축척 그대로 4.6 × 2 m
    w, h = 2.0 * PPM, 4.6 * PPM
    return (f'<g transform="rotate({f(yaw)} {f(cx)} {f(cy)})">'
            f'<rect x="{f(cx - w / 2)}" y="{f(cy - h / 2)}" width="{f(w)}" height="{f(h)}" rx="5" fill="#5d6b7a" stroke="#ffffff" stroke-width="2.5"/>'
            f'<rect x="{f(cx - w / 2 + 4)}" y="{f(cy - h / 2 + 7)}" width="{f(w - 8)}" height="{f(h * 0.2)}" rx="2" fill="#bcd3e6"/></g>')


def supply_icon(cx, cy, size=46):
    # 탄약 보급 — 올리브색 판에 흰 총알 셋 (초록 십자는 약국처럼 보여서 안 쓴다)
    r = size / 2
    out = (f'<rect x="{f(cx - r - 3)}" y="{f(cy - r - 3)}" width="{f(size + 6)}" height="{f(size + 6)}" rx="9" fill="#ffffff"/>'
           f'<rect x="{f(cx - r)}" y="{f(cy - r)}" width="{f(size)}" height="{f(size)}" rx="7" fill="#4c6b25"/>')
    w, h = size * 0.17, size * 0.6
    for dx in (-0.29, 0.0, 0.29):
        x, top, bottom = cx + dx * size, cy - h / 2, cy + h / 2
        out += (f'<path d="M {f(x - w / 2)} {f(bottom)} L {f(x - w / 2)} {f(top + w * 0.9)} Q {f(x)} {f(top - w * 0.5)} {f(x + w / 2)} {f(top + w * 0.9)} '
                f'L {f(x + w / 2)} {f(bottom)} Z" fill="#ffffff"/>'
                f'<rect x="{f(x - w / 2 - 1)}" y="{f(bottom - h * 0.36)}" width="{f(w + 2)}" height="2.2" fill="#4c6b25"/>')
    return out


def label_offset(side, gap):
    return {"n": (0, -gap), "s": (0, gap), "e": (gap * 2.6, 0), "w": (-gap * 2.6, 0)}.get(side, (0, -gap))


def marker_label(m, cx, cy, gap, color):
    """표시 이름표 — labelAt [x, z](월드)가 있으면 그 자리, 없으면 labelSide(n · s · e · w) 쪽으로 gap px"""
    if "labelAt" in m:
        lx, ly = P(*m["labelAt"])
    else:
        dx, dy = label_offset(m.get("labelSide", "n"), gap)
        lx, ly = cx + dx, cy + dy
    return tag_box(lx, ly, m["label"], color)


# ── 조립 ─────────────────────────────────────────────────────
out = []
add = out.append
rng = random.Random(1117)

add(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">')
add("""<defs>
  <linearGradient id="gHeader" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3566bd"/><stop offset="0.55" stop-color="#1f4793"/><stop offset="1" stop-color="#14326e"/></linearGradient>
  <linearGradient id="gGloss" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff" stop-opacity="0.42"/><stop offset="1" stop-color="#ffffff" stop-opacity="0.04"/></linearGradient>
  <linearGradient id="gFrame" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#e3e6e9"/><stop offset="0.5" stop-color="#a7adb4"/><stop offset="1" stop-color="#d4d8dc"/></linearGradient>
  <linearGradient id="gEntrance" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3a8be8"/><stop offset="1" stop-color="#1757b0"/></linearGradient>
  <linearGradient id="gStart" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f0412f"/><stop offset="1" stop-color="#b8140f"/></linearGradient>
  <linearGradient id="gLegendTab" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3566bd"/><stop offset="1" stop-color="#173a7c"/></linearGradient>
  <radialGradient id="gScrew" cx="0.35" cy="0.35" r="0.7"><stop offset="0" stop-color="#fbfbfb"/><stop offset="1" stop-color="#7c838a"/></radialGradient>
  <radialGradient id="gVignette" cx="0.5" cy="0.5" r="0.75"><stop offset="0.6" stop-color="#b9a46a" stop-opacity="0"/><stop offset="1" stop-color="#9c8650" stop-opacity="0.28"/></radialGradient>
  <filter id="fDrop" x="-20%" y="-20%" width="140%" height="160%"><feDropShadow dx="2" dy="3" stdDeviation="2" flood-color="#2b2418" flood-opacity="0.35"/></filter>
  <filter id="fTitle" x="-5%" y="-20%" width="110%" height="160%"><feDropShadow dx="3" dy="4" stdDeviation="1.5" flood-color="#081733" flood-opacity="0.7"/></filter>
  <filter id="fFade"><feColorMatrix type="saturate" values="0.84"/></filter>
  <filter id="fGrain" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="2" seed="7"/><feColorMatrix type="matrix" values="0 0 0 0 0.25  0 0 0 0 0.2  0 0 0 0 0.12  0 0 0 -2.2 1.25"/></filter>
  <filter id="fBlotch" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.0035" numOctaves="3" seed="3"/><feColorMatrix type="matrix" values="0 0 0 0 0.86  0 0 0 0 0.78  0 0 0 0 0.52  0 0 0 2.4 -1.25"/></filter>
""")
add(f'<clipPath id="cMap"><rect x="{MX}" y="{MY}" width="{MAP_W}" height="{MAP_H}"/></clipPath>')
add("</defs>")

add('<g filter="url(#fFade)">')
# 판 바탕 · 금속 테
add(f'<rect x="0" y="0" width="{W}" height="{H}" fill="url(#gFrame)"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" rx="6" fill="#f3efe1"/>')

# 제목 띠
add(f'<rect x="12" y="12" width="{W - 24}" height="{HEADER - 12}" rx="6" fill="url(#gHeader)"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{(HEADER - 12) * 0.48}" rx="6" fill="url(#gGloss)"/>')
add(f'<rect x="12" y="{HEADER - 4}" width="{W - 24}" height="11" fill="#f2bf1c"/>')
add(f'<rect x="12" y="{HEADER + 7}" width="{W - 24}" height="3" fill="#ffffff"/>')
add(f'<text x="{W / 2}" y="130" font-family="{FONT_TITLE}" font-weight="900" font-size="112" letter-spacing="10" fill="#ffffff" '
    f'text-anchor="middle" filter="url(#fTitle)">{esc(layers["title"])}</text>')
if layers.get("subtitle"):
    add(f'<text x="{W / 2}" y="186" font-family="Arial, sans-serif" font-weight="700" font-size="27" letter-spacing="9" fill="#cfe0ff" '
        f'text-anchor="middle">{esc(layers["subtitle"])}</text>')

# 지도 테두리
add(f'<rect x="{MX - 7}" y="{MY - 7}" width="{MAP_W + 14}" height="{MAP_H + 14}" fill="#ffffff" stroke="#4d5b72" stroke-width="4"/>')

add('<g clip-path="url(#cMap)">')
add(f'<rect x="{MX}" y="{MY}" width="{MAP_W}" height="{MAP_H}" fill="#cdbf98"/>')
ground = scene["ground"]
for g in ground:
    if g["kind"] == "Sidewalk":
        add(box(g, fill="#e2d4aa"))
for g in ground:
    if g["kind"] == "Lot":
        add(box(g, fill="#d6d2c6", stroke="#b5ae9b", stroke_width="2"))
# 차도 · 샛길 — 테두리를 먼저 다 그리고 속을 칠해 교차로가 이어지게
for g in ground:
    if g["kind"] == "Road":
        add(box(g, fill="#857a62", stroke="#857a62", stroke_width="11"))
    elif g["kind"] == "Alley":
        add(box(g, fill="#9b8f74", stroke="#9b8f74", stroke_width="7"))
for g in ground:
    if g["kind"] == "Road":
        add(box(g, fill="#fffdf6"))
    elif g["kind"] == "Alley":
        add(box(g, fill="#f8f3e2"))

# 건물 — 그림자(입체) 먼저, 위에 지붕
dept = [b for b in scene["buildings"] if b["departmentStore"]]
others = [b for b in scene["buildings"] if not b["departmentStore"]]
add('<g transform="translate(6 7)">')
for b in others:
    add(box(b, fill=shade(building_color(b["name"]), 0.72)))
for b in dept:
    add(box(b, fill="#b0532c"))
add("</g>")
for b in others:
    add(box(b, fill=building_color(b["name"]), stroke="#8f8673", stroke_width="1.6"))
for b in dept:
    add(box(b, fill="#f08b5b", stroke="#9a4520", stroke_width="2.5"))

# 지하도 (홀 · 통로 · 열린 입구) — 반투명 파랑, 점선 테두리
hidden = set(layers.get("hideUnderground", []))
for u in scene["underground"]:
    if u["name"] in hidden:
        continue
    add(box(u, fill="#7fa6d8", fill_opacity="0.36", stroke="#3f70b5", stroke_width="2.5", stroke_dasharray="9 6"))

# 장식 (분수)
for m in layers["markers"]:
    if m["type"] == "fountain":
        cx, cy = P(m["x"], m["z"])
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(m.get("r", 3) * PPM)}" fill="#bfe1f0" stroke="#7aa6bf" stroke-width="3"/>')
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(m.get("r", 3) * PPM * 0.35)}" fill="#e9f6fb" stroke="#7aa6bf" stroke-width="2"/>')

# 경로 — 흰 테 → 색 선 (지하 구간은 점선) → 흰 꺾쇠 → 끝 화살촉
for r in layers["routes"]:
    color = r["color"]
    segs = r["segments"]
    for i, s in enumerate(segs):
        pts = [P(x, z) for x, z in s["points"]]
        if s.get("offset", True):
            pts = offset_line(pts, r.get("side", 0) * ROUTE_OFFSET)
        s["_px"] = pts
    for s in segs:
        if s["underground"]:
            add(f'<polyline points="{poly(s["_px"])}" fill="none" stroke="#ffffff" stroke-opacity="0.9" stroke-width="{CASING_W - 4}" stroke-linejoin="round" stroke-linecap="round"/>')
        else:
            add(f'<polyline points="{poly(s["_px"])}" fill="none" stroke="#ffffff" stroke-width="{CASING_W}" stroke-linejoin="round" stroke-linecap="round"/>')
    for s in segs:
        if s["underground"]:
            add(f'<polyline points="{poly(s["_px"])}" fill="none" stroke="{color}" stroke-width="{ROUTE_W - 2}" stroke-dasharray="22 13" stroke-linejoin="round"/>')
        else:
            add(f'<polyline points="{poly(s["_px"])}" fill="none" stroke="{color}" stroke-width="{ROUTE_W}" stroke-linejoin="round" stroke-linecap="round"/>')
    for s in segs:
        if s["underground"]:
            continue
        for x, y, a in walk(s["_px"], 128, 64)[:-1] if len(s["_px"]) > 1 else []:
            add(f'<path d="M -6 -6.5 L 2.5 0 L -6 6.5" transform="translate({f(x)} {f(y)}) rotate({f(a)})" fill="none" stroke="#ffffff" stroke-width="3.6" stroke-linecap="round" stroke-linejoin="round"/>')
    last = segs[-1]["_px"]
    (ax, ay), (bx, by) = last[-2], last[-1]
    a = math.degrees(math.atan2(by - ay, bx - ax))
    add(f'<path d="M -14 -15 L 12 0 L -14 15 Z" transform="translate({f(bx)} {f(by)}) rotate({f(a)})" fill="{color}" stroke="#ffffff" stroke-width="4" stroke-linejoin="round"/>')

# 표시
for m in layers["markers"]:
    t = m["type"]
    if t == "entrance":
        cx, cy = P(m["x"], m["z"])
        add(entrance_icon(cx, cy))
        if m.get("label"):
            add(marker_label(m, cx, cy, 56, "#1757b0"))
    elif t == "vehicle":
        cx, cy = P(m["x"], m["z"])
        add(vehicle_icon(cx, cy, m.get("yaw", 0)))
        if m.get("label"):
            add(marker_label(m, cx, cy, 48, "#46525f"))
    elif t == "supply":
        cx, cy = P(m["x"], m["z"])
        add(supply_icon(cx, cy))
        if m.get("label"):
            add(marker_label(m, cx, cy, 58, "#3f6524"))
    elif t == "gate":
        lx, ly = P(*m["labelAt"]) if "labelAt" in m else P(m["x"], m["z"] - 4)
        add(tag_box(lx, ly, m["label"], "#9a3a14", size=31))

for m in layers["markers"]:
    if m["type"] == "objective":
        zone = next(z for z in scene["zones"] if z["name"] == m["zone"])
        cx, cy = P(zone["x"], zone["z"])
        add(f'<g filter="url(#fDrop)"><circle cx="{f(cx)}" cy="{f(cy - 62)}" r="64" fill="#d71f1a" stroke="#ffffff" stroke-width="7"/></g>')
        add(star(cx, cy - 64, 44, "#ffe34d", "#ffffff", 3))
        w = 300
        add(f'<g filter="url(#fDrop)"><rect x="{f(cx - w / 2)}" y="{f(cy + 18)}" width="{w}" height="118" rx="12" fill="#ffffff" stroke="#d71f1a" stroke-width="5"/></g>')
        add(f'<text x="{f(cx)}" y="{f(cy + 70)}" font-family="{FONT_LABEL}" font-weight="800" font-size="44" fill="#d71f1a" text-anchor="middle">{esc(m["label"])}</text>')
        add(f'<text x="{f(cx)}" y="{f(cy + 118)}" font-family="{FONT_LABEL}" font-weight="700" font-size="36" fill="#3a2a20" text-anchor="middle">{esc(m["sub"])}</text>')

for r in layers["routes"]:
    if r.get("tag"):
        cx, cy = P(r["tag"]["x"], r["tag"]["z"])
        add(pill(cx, cy, r["name"], r["color"]))

for m in layers["markers"]:
    if m["type"] == "start":
        sx = m.get("x", scene["start"]["x"])
        sz = m.get("z", scene["start"]["z"])
        cx, cy = P(sx, sz)
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="34" fill="#e3231b" fill-opacity="0.22" stroke="#e3231b" stroke-opacity="0.55" stroke-width="3"/>')
        add(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="17" fill="url(#gStart)" stroke="#ffffff" stroke-width="5"/>')
        text = m["label"] + (" · " + m["sub"] if m.get("sub") else "")
        bw, bh = len(text) * 33 + 34, 58
        bx, by = cx - 46 - bw, cy - bh / 2 - 8
        add(f'<g filter="url(#fDrop)"><rect x="{f(bx)}" y="{f(by)}" width="{f(bw)}" height="{bh}" rx="10" fill="url(#gStart)" stroke="#ffffff" stroke-width="4"/>'
            f'<path d="M {f(bx + bw - 2)} {f(by + bh / 2 - 9)} L {f(cx - 22)} {f(cy)} L {f(bx + bw - 2)} {f(by + bh / 2 + 11)} Z" fill="#c9180f" stroke="#ffffff" stroke-width="3"/></g>')
        add(f'<text x="{f(bx + bw / 2)}" y="{f(by + 41)}" font-family="{FONT_LABEL}" font-weight="800" font-size="34" fill="#ffffff" text-anchor="middle">{esc(text)}</text>')

# 방위표
if layers.get("compassAt"):
    cx, cy = P(layers["compassAt"]["x"], layers["compassAt"]["z"])
    add(f'<g filter="url(#fDrop)"><circle cx="{f(cx)}" cy="{f(cy)}" r="44" fill="#ffffff" stroke="#24447f" stroke-width="5"/></g>')
    add(f'<polygon points="{f(cx)},{f(cy - 34)} {f(cx + 13)},{f(cy + 6)} {f(cx)},{f(cy)} {f(cx - 13)},{f(cy + 6)}" fill="#d71f1a"/>')
    add(f'<polygon points="{f(cx)},{f(cy + 30)} {f(cx + 13)},{f(cy + 6)} {f(cx)},{f(cy)} {f(cx - 13)},{f(cy + 6)}" fill="#9aa3b0"/>')
    add(f'<text x="{f(cx)}" y="{f(cy - 52)}" font-family="Arial, sans-serif" font-weight="900" font-size="30" fill="#24447f" text-anchor="middle" '
        f'stroke="#ffffff" stroke-width="6" paint-order="stroke">N</text>')

# 범례 — 지도에 있는 것만
rows = []
for r in layers["routes"]:
    rows.append(("route", r))
types = [m["type"] for m in layers["markers"]]
for t, name in (("entrance", "지하도 입구"), ("vehicle", "차량"), ("supply", "탄약 보급"), ("start", "현위치"), ("objective", "목표")):
    if t in types:
        rows.append((t, name))
if layers.get("legendAt"):
    lx, ly = P(layers["legendAt"]["x"], layers["legendAt"]["z"])
    row_h, lw = 58, 500
    lh = 74 + row_h * len(rows) + 14
    add(f'<g filter="url(#fDrop)"><rect x="{f(lx)}" y="{f(ly)}" width="{lw}" height="{lh}" rx="10" fill="#fffdf6" stroke="#4d5b72" stroke-width="3"/></g>')
    add(f'<path d="M {f(lx)} {f(ly + 10)} a 10 10 0 0 1 10 -10 h 150 l 22 52 h -182 Z" fill="url(#gLegendTab)"/>')
    add(f'<text x="{f(lx + 22)}" y="{f(ly + 38)}" font-family="{FONT_OLD}" font-weight="700" font-size="30" fill="#ffffff">범  례</text>')
    y = ly + 74 + row_h / 2
    for kind, item in rows:
        ix = lx + 60
        if kind == "route":
            dash = any(s["underground"] for s in item["segments"])
            add(f'<line x1="{f(ix - 34)}" y1="{f(y)}" x2="{f(ix + 34)}" y2="{f(y)}" stroke="#ffffff" stroke-width="{CASING_W}" stroke-linecap="round"/>')
            if dash:
                add(f'<line x1="{f(ix - 34)}" y1="{f(y)}" x2="{f(ix)}" y2="{f(y)}" stroke="{item["color"]}" stroke-width="{ROUTE_W}" stroke-linecap="round"/>')
                add(f'<line x1="{f(ix + 6)}" y1="{f(y)}" x2="{f(ix + 34)}" y2="{f(y)}" stroke="{item["color"]}" stroke-width="{ROUTE_W - 2}" stroke-dasharray="11 7"/>')
                add(f'<text x="{f(lx + 118)}" y="{f(y + 11)}" font-family="{FONT_OLD}" font-weight="700" font-size="32" fill="#2b2b2b">{esc(item["name"])}'
                    f'<tspan font-size="25" font-weight="400" fill="#555"> (점선: 지하)</tspan></text>')
            else:
                add(f'<line x1="{f(ix - 34)}" y1="{f(y)}" x2="{f(ix + 34)}" y2="{f(y)}" stroke="{item["color"]}" stroke-width="{ROUTE_W}" stroke-linecap="round"/>')
                add(f'<text x="{f(lx + 118)}" y="{f(y + 11)}" font-family="{FONT_OLD}" font-weight="700" font-size="32" fill="#2b2b2b">{esc(item["name"])}</text>')
            y += row_h
            continue
        if kind == "entrance":
            add(entrance_icon(ix, y, 38))
        elif kind == "vehicle":
            add(vehicle_icon(ix, y, 90))
        elif kind == "supply":
            add(supply_icon(ix, y, 38))
        elif kind == "start":
            add(f'<circle cx="{f(ix)}" cy="{f(y)}" r="15" fill="url(#gStart)" stroke="#ffffff" stroke-width="4"/>')
        elif kind == "objective":
            add(f'<circle cx="{f(ix)}" cy="{f(y)}" r="21" fill="#d71f1a" stroke="#ffffff" stroke-width="3"/>')
            add(star(ix, y - 1, 14, "#ffe34d"))
        add(f'<text x="{f(lx + 118)}" y="{f(y + 11)}" font-family="{FONT_OLD}" font-weight="700" font-size="32" fill="#2b2b2b">{esc(item)}</text>')
        y += row_h

add("</g>")  # cMap
add("</g>")  # fFade

# 세월 — 바랜 얼룩 · 잔 알갱이 · 흠집 · 가장자리 누렇게
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" filter="url(#fBlotch)" opacity="0.35"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" filter="url(#fGrain)" opacity="0.10"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" fill="url(#gVignette)"/>')
for _ in range(34):
    x, y = rng.uniform(30, W - 30), rng.uniform(30, H - 30)
    ln, a = rng.uniform(25, 140), rng.uniform(-0.6, 0.6) + (math.pi / 2 if rng.random() < 0.3 else 0)
    add(f'<line x1="{f(x)}" y1="{f(y)}" x2="{f(x + ln * math.cos(a))}" y2="{f(y + ln * math.sin(a))}" stroke="#ffffff" '
        f'stroke-opacity="{rng.uniform(0.18, 0.4):.2f}" stroke-width="{rng.uniform(0.8, 2.0):.1f}" stroke-linecap="round"/>')
for _ in range(60):
    add(f'<circle cx="{f(rng.uniform(20, W - 20))}" cy="{f(rng.uniform(20, H - 20))}" r="{rng.uniform(0.8, 2.4):.1f}" fill="#4a3d26" fill-opacity="{rng.uniform(0.12, 0.3):.2f}"/>')

# 모서리 나사
for sx, sy in ((26, 26), (W - 26, 26), (26, H - 26), (W - 26, H - 26)):
    add(f'<circle cx="{sx}" cy="{sy}" r="10" fill="url(#gScrew)" stroke="#6b7178" stroke-width="1.5"/>'
        f'<line x1="{sx - 6}" y1="{sy + 3}" x2="{sx + 6}" y2="{sy - 3}" stroke="#5c6168" stroke-width="2"/>')
add("</svg>")

svg = "\n".join(out)
(HERE / "C_TrainingMap.svg").write_text(svg, encoding="utf-8")
html = ('<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;padding:0;background:#000;overflow:hidden}'
        'svg{display:block}</style></head><body>' + svg + "</body></html>")
(HERE / "C_TrainingMap.html").write_text(html, encoding="utf-8")

browser = next((p for p in EDGE if Path(p).exists()), None)
if browser is None:
    sys.exit("Edge/Chrome 을 못 찾았다 — C_TrainingMap.html 을 브라우저로 열어 확인할 것")
png = HERE / "C_TrainingMap.png"
subprocess.run([browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                f"--window-size={W},{H}", f"--screenshot={png}", (HERE / "C_TrainingMap.html").as_uri()],
               check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
print(f"{png}  {W}x{H}  (판 비율 {W / H:.3f})")
if "--install" in sys.argv:
    shutil.copyfile(png, ASSET_PNG)
    print(f"→ {ASSET_PNG}")
