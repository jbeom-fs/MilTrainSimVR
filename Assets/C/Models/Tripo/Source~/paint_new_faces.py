# 메시를 고쳐 새로 덮인 UV 자리(원래 어떤 삼각형도 안 쓰던 곳)에 바로 옆 무늬를 이어 칠한다 (11/18 콘크리트 블록 —
# 구멍을 메운 삼각형이 그림 없는 자리를 읽어 뿌연 줄이 보였다). 가장 가까운 '원래 칠해진' 점을 거울 삼아 그 너머를 베낀다.
# blender -b --factory-startup --python paint_new_faces.py -- <원본.fbx> <고친.fbx> <텍스처> <저장할 곳>
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbxio  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
fbx_old, fbx_new, tex, out_path = argv[:4]
MARGIN = 4      # 새 자리 둘레로 이만큼(px) 더 칠한다 — 밉맵에서 바깥 색이 번지지 않게
INSIDE = 3      # 거울 삼는 점에서 이만큼(px) 더 안쪽부터 베낀다 — 섬 가장자리의 번진 색을 피해서


def uv_triangles(path):
    root, _ = fbxio.load(path)
    g = fbxio.find(root, b"Objects", b"Geometry")
    poly = list(fbxio.find(g, b"PolygonVertexIndex").props[0])
    luv = fbxio.find(g, b"LayerElementUV")
    uv = list(fbxio.find(luv, b"UV").props[0])
    idx = list(fbxio.find(luv, b"UVIndex").props[0])
    tris, start = [], 0
    for i, raw in enumerate(poly):
        if raw < 0:
            corners = [(uv[idx[k] * 2], uv[idx[k] * 2 + 1]) for k in range(start, i + 1)]
            for j in range(1, len(corners) - 1):
                tris.append((corners[0], corners[j], corners[j + 1]))
            start = i + 1
    return tris


im = bpy.data.images.load(tex)
W, H = im.size
px = np.empty(W * H * 4, np.float32)
im.pixels.foreach_get(px)
px = px.reshape(H, W, 4)


def raster(tris):
    mask = np.zeros((H, W), bool)
    for t in tris:
        P = np.array([(u * W, v * H) for u, v in t])
        x0, y0 = np.floor(P.min(0)).astype(int)
        x1, y1 = np.ceil(P.max(0)).astype(int)
        x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        (ax, ay), (bx, by), (cx, cy) = P
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-12:
            continue
        l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
        l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
        mask[y0:y1 + 1, x0:x1 + 1] |= (l1 >= -0.02) & (l2 >= -0.02) & (1 - l1 - l2 >= -0.02)
    return mask


valid = raster(uv_triangles(fbx_old))
target = raster(uv_triangles(fbx_new)) & ~valid
count = int(target.sum())
if count == 0:
    print("@@PAINT nothing to paint — the new faces only use texels that were already painted")
    raise SystemExit(0)
for _ in range(MARGIN):
    grown = target.copy()
    grown[1:, :] |= target[:-1, :]
    grown[:-1, :] |= target[1:, :]
    grown[:, 1:] |= target[:, :-1]
    grown[:, :-1] |= target[:, 1:]
    target = grown & ~valid

ys, xs = np.nonzero(target)
reach = 80
y0, y1 = max(ys.min() - reach, 0), min(ys.max() + reach, H - 1)
x0, x1 = max(xs.min() - reach, 0), min(xs.max() + reach, W - 1)
vy, vx = np.nonzero(valid[y0:y1 + 1, x0:x1 + 1])
vy, vx = vy + y0, vx + x0
rgb = px[..., :3].copy()
out = rgb.copy()
painted = 0
for y, x in zip(ys, xs):
    d2 = (vx - x) ** 2 + (vy - y) ** 2
    j = int(np.argmin(d2))
    qx, qy = int(vx[j]), int(vy[j])
    dx, dy = qx - x, qy - y
    length = max(1.0, float(np.hypot(dx, dy)))
    sx = int(round(qx + dx + dx / length * INSIDE))
    sy = int(round(qy + dy + dy / length * INSIDE))
    if not (0 <= sx < W and 0 <= sy < H and valid[sy, sx]):
        sx, sy = qx, qy
    out[y, x] = rgb[sy, sx]
    painted += 1
px[..., :3] = out
im.pixels.foreach_set(px.ravel())
im.filepath_raw = out_path
im.file_format = "JPEG"
im.save(filepath=out_path, quality=95)
print("@@PAINT", os.path.basename(tex), f"{W}x{H}", "new texels", count, "painted with margin", painted,
      "box", (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())), "saved", out_path)
