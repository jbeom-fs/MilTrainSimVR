# -*- coding: utf-8 -*-
"""임무 안내판 그림 만들기 — 집결지의 큰 안내판(C_StartBackdrop/집결지 · 총 탁자 둘레/임무 안내판)에 붙는다.

사용자 11/19: "UI 로 뜨던 목표도 안내판 + 안내판 기둥 2개를 써서 큰 안내판으로 바꿔서 안내판 옆에 세워 줘".
시작 전에 눈앞에 떠 있던 판(MatchDisplay 의 앞판)의 내용을 그대로 옮긴다 — 지도 안내판(TrainingMap~/make_map.py)과 같은
2000~2010년대 길거리 안내판 느낌(남색 광택 제목 띠 · 노란 줄 · 바랜 색).

  brief.json   적 수 · 자유 전투 시간 · 점령지 이름 · 적 점령 대기 — 씬 값이 바뀌면 여기를 고치고 다시 만든다
               (판은 그림이라 저절로 바뀌지 않는다)

  python make_brief.py            → C_TrainingBrief.svg / .html / .png (이 폴더, 시안)
  python make_brief.py --install  → 위 + Assets/C/Images/Sign/C_TrainingBrief.png 로 복사 (안내판이 쓰는 텍스처)

판 크기 2.0 × 1.25 m (800 px/m). PNG 는 Edge(헤드리스)로 SVG 를 찍는다.
"""
import json
import math
import random
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ASSET_PNG = HERE.parent / "C_TrainingBrief.png"
EDGE = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]
B = json.loads((HERE / "brief.json").read_text(encoding="utf-8"))

W, H = 1600, 1000
HEADER = 196
FONT_TITLE = "'Noto Sans KR', 'Malgun Gothic', sans-serif"
FONT_LABEL = "'Malgun Gothic', 'Gulim', sans-serif"
INK, SUB, NAVY, RED = "#1c2a44", "#4d5b72", "#1f4793", "#d71f1a"


def esc(s):
    return str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


out = []
add = out.append
rng = random.Random(1119)

add(f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">')
add("""<defs>
  <linearGradient id="gHeader" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3566bd"/><stop offset="0.55" stop-color="#1f4793"/><stop offset="1" stop-color="#14326e"/></linearGradient>
  <linearGradient id="gGloss" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff" stop-opacity="0.42"/><stop offset="1" stop-color="#ffffff" stop-opacity="0.04"/></linearGradient>
  <linearGradient id="gFrame" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#e3e6e9"/><stop offset="0.5" stop-color="#a7adb4"/><stop offset="1" stop-color="#d4d8dc"/></linearGradient>
  <linearGradient id="gBadge" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3a74d0"/><stop offset="1" stop-color="#173a7c"/></linearGradient>
  <linearGradient id="gRed" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f0412f"/><stop offset="1" stop-color="#b8140f"/></linearGradient>
  <radialGradient id="gScrew" cx="0.35" cy="0.35" r="0.7"><stop offset="0" stop-color="#fbfbfb"/><stop offset="1" stop-color="#7c838a"/></radialGradient>
  <radialGradient id="gVignette" cx="0.5" cy="0.5" r="0.75"><stop offset="0.6" stop-color="#b9a46a" stop-opacity="0"/><stop offset="1" stop-color="#9c8650" stop-opacity="0.28"/></radialGradient>
  <filter id="fDrop" x="-20%" y="-20%" width="140%" height="160%"><feDropShadow dx="2" dy="3" stdDeviation="2" flood-color="#2b2418" flood-opacity="0.35"/></filter>
  <filter id="fTitle" x="-5%" y="-20%" width="110%" height="160%"><feDropShadow dx="3" dy="4" stdDeviation="1.5" flood-color="#081733" flood-opacity="0.7"/></filter>
  <filter id="fFade"><feColorMatrix type="saturate" values="0.84"/></filter>
  <filter id="fGrain" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.85" numOctaves="2" seed="11"/><feColorMatrix type="matrix" values="0 0 0 0 0.25  0 0 0 0 0.2  0 0 0 0 0.12  0 0 0 -2.2 1.25"/></filter>
  <filter id="fBlotch" x="0" y="0" width="100%" height="100%"><feTurbulence type="fractalNoise" baseFrequency="0.0035" numOctaves="3" seed="5"/><feColorMatrix type="matrix" values="0 0 0 0 0.86  0 0 0 0 0.78  0 0 0 0 0.52  0 0 0 2.4 -1.25"/></filter>
</defs>""")

add('<g filter="url(#fFade)">')
add(f'<rect x="0" y="0" width="{W}" height="{H}" fill="url(#gFrame)"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" rx="6" fill="#f3efe1"/>')

# 제목 띠 (지도 안내판과 같은 꼴)
add(f'<rect x="12" y="12" width="{W - 24}" height="{HEADER - 12}" rx="6" fill="url(#gHeader)"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{(HEADER - 12) * 0.48}" rx="6" fill="url(#gGloss)"/>')
add(f'<rect x="12" y="{HEADER - 4}" width="{W - 24}" height="11" fill="#f2bf1c"/>')
add(f'<rect x="12" y="{HEADER + 7}" width="{W - 24}" height="3" fill="#ffffff"/>')
add(f'<text x="{W / 2}" y="116" font-family="{FONT_TITLE}" font-weight="900" font-size="100" letter-spacing="10" fill="#ffffff" '
    f'text-anchor="middle" filter="url(#fTitle)">{esc(B["title"])}</text>')
add(f'<text x="{W / 2}" y="164" font-family="Arial, sans-serif" font-weight="700" font-size="27" letter-spacing="11" fill="#dfe8f7" '
    f'text-anchor="middle">{esc(B["subtitle"])}</text>')

# 순서 세 줄 — 번호 동그라미 · 큰 글 · 작은 글 (+ 숫자 알약)
minutes = round(B["freeSeconds"] / 60)
rows = [
    ("소총을 잡으면 시작", "차양 아래 탁자 위에 소총이 있다", None),
    (f"자유 전투 {minutes}분", "지상이나 지하도로 나아가 적을 찾는다 — 길은 옆 지도 참고", f"적 {B['enemies']}명"),
    (f"{minutes}분 뒤 오버타임", f"점령지 ‘{B['zone']}’이 열리고, 적도 모두 그리로 모인다", "점령지 개방"),
]
y = HEADER + 108
ROW = 172
for i, (main, sub, pill) in enumerate(rows):
    cx = 118
    if i < len(rows) - 1:
        add(f'<line x1="{cx}" y1="{y + 50}" x2="{cx}" y2="{y + ROW - 50}" stroke="#9fb2d6" stroke-width="7" stroke-dasharray="3 14" stroke-linecap="round"/>')
    add(f'<g filter="url(#fDrop)"><circle cx="{cx}" cy="{y}" r="48" fill="url(#gBadge)" stroke="#ffffff" stroke-width="6"/></g>')
    add(f'<text x="{cx}" y="{y + 22}" font-family="Arial, sans-serif" font-weight="900" font-size="62" fill="#ffffff" text-anchor="middle">{i + 1}</text>')
    add(f'<text x="204" y="{y + 6}" font-family="{FONT_TITLE}" font-weight="900" font-size="68" fill="{INK}">{esc(main)}</text>')
    add(f'<text x="206" y="{y + 60}" font-family="{FONT_LABEL}" font-weight="700" font-size="35" fill="{SUB}">{esc(sub)}</text>')
    if pill:
        pw = 46 + 40 * len(pill)
        px = W - 86 - pw
        add(f'<g filter="url(#fDrop)"><rect x="{px}" y="{y - 52}" width="{pw}" height="70" rx="35" fill="url(#gRed)" stroke="#ffffff" stroke-width="5"/></g>')
        add(f'<text x="{px + pw / 2}" y="{y - 3}" font-family="{FONT_LABEL}" font-weight="800" font-size="40" fill="#ffffff" text-anchor="middle">{esc(pill)}</text>')
    y += ROW

# 승리 · 패배 상자
by = H - 222
add(f'<g filter="url(#fDrop)"><rect x="62" y="{by}" width="{W - 124}" height="166" rx="14" fill="#fffdf6" stroke="{RED}" stroke-width="6"/></g>')
add(f'<rect x="62" y="{by}" width="182" height="166" rx="14" fill="url(#gRed)"/>')
add(f'<rect x="212" y="{by}" width="32" height="166" fill="url(#gRed)"/>')
add(f'<text x="153" y="{by + 78}" font-family="{FONT_TITLE}" font-weight="900" font-size="58" fill="#ffffff" text-anchor="middle">승리</text>')
add(f'<text x="153" y="{by + 132}" font-family="{FONT_LABEL}" font-weight="700" font-size="30" fill="#ffe9c4" text-anchor="middle">조건</text>')
add(f'<text x="282" y="{by + 72}" font-family="{FONT_TITLE}" font-weight="900" font-size="60" fill="{INK}">적을 모두 섬멸하거나, 점령지를 먼저 점령</text>')
add(f'<text x="284" y="{by + 130}" font-family="{FONT_LABEL}" font-weight="700" font-size="33" fill="{SUB}">'
    f'패배 — 전사하거나 적이 점령지를 점령 (적의 점령은 오버타임 {B["enemyCaptureDelay"]}초 뒤부터)</text>')
add("</g>")  # fFade

# 세월 — 바랜 얼룩 · 잔 알갱이 · 흠집 · 가장자리 누렇게 (지도 안내판과 같은 손질)
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" filter="url(#fBlotch)" opacity="0.35"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" filter="url(#fGrain)" opacity="0.10"/>')
add(f'<rect x="12" y="12" width="{W - 24}" height="{H - 24}" fill="url(#gVignette)"/>')
for _ in range(26):
    x, yy = rng.uniform(30, W - 30), rng.uniform(30, H - 30)
    ln, a = rng.uniform(25, 140), rng.uniform(-0.6, 0.6) + (math.pi / 2 if rng.random() < 0.3 else 0)
    add(f'<line x1="{x:.1f}" y1="{yy:.1f}" x2="{x + ln * math.cos(a):.1f}" y2="{yy + ln * math.sin(a):.1f}" stroke="#ffffff" '
        f'stroke-opacity="{rng.uniform(0.18, 0.4):.2f}" stroke-width="{rng.uniform(0.8, 2.0):.1f}" stroke-linecap="round"/>')
for _ in range(48):
    add(f'<circle cx="{rng.uniform(20, W - 20):.1f}" cy="{rng.uniform(20, H - 20):.1f}" r="{rng.uniform(0.8, 2.4):.1f}" fill="#4a3d26" fill-opacity="{rng.uniform(0.12, 0.3):.2f}"/>')
for sx, sy in ((26, 26), (W - 26, 26), (26, H - 26), (W - 26, H - 26)):
    add(f'<circle cx="{sx}" cy="{sy}" r="10" fill="url(#gScrew)" stroke="#6b7178" stroke-width="1.5"/>'
        f'<line x1="{sx - 6}" y1="{sy + 3}" x2="{sx + 6}" y2="{sy - 3}" stroke="#5c6168" stroke-width="2"/>')
add("</svg>")

svg = "\n".join(out)
(HERE / "C_TrainingBrief.svg").write_text(svg, encoding="utf-8")
html = ('<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;padding:0;background:#000;overflow:hidden}'
        'svg{display:block}</style></head><body>' + svg + "</body></html>")
(HERE / "C_TrainingBrief.html").write_text(html, encoding="utf-8")

browser = next((p for p in EDGE if Path(p).exists()), None)
if browser is None:
    sys.exit("Edge/Chrome 을 못 찾았다 — C_TrainingBrief.html 을 브라우저로 열어 확인할 것")
png = HERE / "C_TrainingBrief.png"
subprocess.run([browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1",
                f"--window-size={W},{H}", f"--screenshot={png}", (HERE / "C_TrainingBrief.html").as_uri()],
               check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
print(f"{png}  {W}x{H}  (판 비율 {W / H:.3f})")
if "--install" in sys.argv:
    shutil.copyfile(png, ASSET_PNG)
    print(f"-> {ASSET_PNG}")
