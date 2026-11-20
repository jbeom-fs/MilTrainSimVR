# Usage: python make_utility_textures.py <map_dir>
#   <map_dir> = Assets/C/Textures/Map — 폴더마다 <이름>_Color.png. 표준 라이브러리만 쓴다 (Blender 필요 없음)
# 4단계 B 설비 (사용자 11/06 — 목업 확인 "이대로 적용"). Unity 목업(C#)에서 그린 식 그대로:
#   ACFan1      : 실외기 윗면 팬 보호망 — 한 장 = 팬 하나 (둥근 테 · 동심원 철사 · 방사 철사 8개 · 날개 4장 · 허브). 바깥은 케이스 색 (512 px)
#   ACGrille1   : 실외기 열교환기 그릴 — 한 장 = 0.2 m (보호망 가로 2.5 cm · 세로 10 cm, 뒤 코일 핀) (256 px, 반복)
#   PanelTank1  : 판 조립 물탱크 — 한 장 = 1 m 판 (이음 줄 · 가장자리 그늘 · 가운데 볼록 테) (256 px, 반복)
# 픽셀 (ix, iy) 은 Unity 텍스처처럼 아래 줄부터 — PNG 는 위 줄부터 쓰니 뒤집어 쓴다
import math
import os
import struct
import sys
import zlib

CASING = (206, 206, 198)
WIRE = (158, 160, 158)


def fan(tu, tv):
    fx, fy = tu * 2.0 - 1.0, tv * 2.0 - 1.0
    fr, fa = math.hypot(fx, fy), math.atan2(fy, fx)
    if fr > 0.97:
        return CASING
    if fr > 0.9:
        return (146, 147, 143)
    sector = math.pi / 4.0
    ma = fa % sector
    da = min(ma, sector - ma)
    ring = fr > 0.15 and (fr / 0.07) % 1.0 < 0.17
    spoke = fr > 0.09 and da * fr < 0.011
    if ring or spoke:
        return WIRE
    if fr < 0.09:
        return (128, 129, 127)
    if fr < 0.16:
        return (58, 60, 63)
    ba = (fa / (2.0 * math.pi) * 4.0 + fr * 0.4) % 1.0
    if ba < 0.36 and fr < 0.86:
        return (50, 52, 56)
    return (20, 21, 23)


def grille(tu, tv):
    if (tv * 8.0) % 1.0 < 0.1 or (tu * 2.0) % 1.0 < 0.03:
        return WIRE
    return (36, 38, 41) if (tu * 128.0) % 1.0 < 0.5 else (60, 63, 67)


def panel(tu, tv):
    pe = min(tu, 1.0 - tu, tv, 1.0 - tv)
    if pe < 0.012:
        return (138, 138, 128)
    shade = 1.0 - 0.07 * min(1.0, max(0.0, 1.0 - pe / 0.12))
    c2 = max(abs(tu - 0.5), abs(tv - 0.5))
    if abs(c2 - 0.3) < 0.012:
        shade *= 0.92
    return (int(222 * shade), int(219 * shade), int(205 * shade))


def write_png(path, n, f):
    rows = []
    for r in range(n):
        iy = n - 1 - r
        row = bytearray(b"\x00")
        for ix in range(n):
            row.extend(f((ix + 0.5) / n, (iy + 0.5) / n))
        rows.append(bytes(row))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", n, n, 8, 2, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as fh:
        fh.write(png)
    print("saved", path)


map_dir = sys.argv[1]
write_png(os.path.join(map_dir, "ACFan1", "ACFan1_Color.png"), 512, fan)
write_png(os.path.join(map_dir, "ACGrille1", "ACGrille1_Color.png"), 256, grille)
write_png(os.path.join(map_dir, "PanelTank1", "PanelTank1_Color.png"), 256, panel)
