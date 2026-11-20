# 구운 텍스처의 빈(검은) 바탕을 섬 색으로 채운다 — 원본 Tripo 텍스처처럼. 밉맵 · LOD 에서 검은색이 번지지 않게.
# 섬 자리(FBX UV 삼각형) + 굽기 여백(keep_px)은 그대로 두고, 나머지는 push-pull(피라미드)로 채운다.
# blender -b --factory-startup --python fill_gaps.py -- <fbx> <texture> [<copy-to> ...]
import bpy, sys, os
import numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
fbx, tex, copies = argv[0], argv[1], argv[2:]
KEEP_PX = 12   # 굽기 여백은 16 px — 가장자리 JPEG 번짐은 빼고 12 px 까지만 믿는다

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
ob = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
me = ob.data
me.calc_loop_triangles()
uvl = me.uv_layers.active.data

im = bpy.data.images.load(tex)
W, H = im.size
px = np.empty(W * H * 4, np.float32)
im.pixels.foreach_get(px)
px = px.reshape(H, W, 4)

# 1) 섬 자리 래스터
mask = np.zeros((H, W), bool)
for t in me.loop_triangles:
    P = np.array([(uvl[li].uv.x * W, uvl[li].uv.y * H) for li in t.loops])
    x0, y0 = np.floor(P.min(0)).astype(int)
    x1, y1 = np.ceil(P.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0)
    x1, y1 = min(x1, W - 1), min(y1, H - 1)
    if x1 < x0 or y1 < y0:
        continue
    xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = P
    d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
    if abs(d) < 1e-12:
        mask[y0:y1 + 1, x0:x1 + 1] |= False
        continue
    l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / d
    l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / d
    l3 = 1 - l1 - l2
    e = -0.02
    mask[y0:y1 + 1, x0:x1 + 1] |= (l1 >= e) & (l2 >= e) & (l3 >= e)
# 2) 굽기 여백만큼 넓힌다 (3x3 최대값을 KEEP_PX 번)
valid = mask.copy()
for _ in range(KEEP_PX):
    v = valid.copy()
    v[1:, :] |= valid[:-1, :]
    v[:-1, :] |= valid[1:, :]
    v[:, 1:] |= valid[:, :-1]
    v[:, :-1] |= valid[:, 1:]
    valid = v

# 3) push-pull: 빈칸은 더 거친 단계의 평균색으로
rgb = px[..., :3].copy()
w0 = valid.astype(np.float32)
levels = []
c, w = rgb * w0[..., None], w0
while min(c.shape[0], c.shape[1]) > 1:
    levels.append((c, w))
    h2, w2 = c.shape[0] // 2, c.shape[1] // 2
    c = c[:h2 * 2, :w2 * 2].reshape(h2, 2, w2, 2, 3).sum((1, 3))
    w = w[:h2 * 2, :w2 * 2].reshape(h2, 2, w2, 2).sum((1, 3))
fill = np.where(w[..., None] > 0, c / np.maximum(w[..., None], 1e-8), 0.5)
for c_l, w_l in reversed(levels):
    up = fill.repeat(2, 0).repeat(2, 1)[:c_l.shape[0], :c_l.shape[1]]
    avg = c_l / np.maximum(w_l[..., None], 1e-8)
    fill = np.where(w_l[..., None] > 0, avg, up)
out = np.where(valid[..., None], rgb, fill)
px[..., :3] = out
im.pixels.foreach_set(px.ravel())
before = float((rgb.max(-1) < 0.02).mean())
after = float((out.max(-1) < 0.02).mean())
for p in [tex] + copies:
    im.filepath_raw = p
    im.file_format = "JPEG"
    im.save(filepath=p, quality=95)
print("@@FILL", os.path.basename(tex), f"{W}x{H}", "islands", round(float(mask.mean()), 3), "kept", round(float(valid.mean()), 3),
      "near-black before", round(before, 3), "after", round(after, 3), "saved", len(copies) + 1)
