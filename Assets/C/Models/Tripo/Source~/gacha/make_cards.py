# 가챠 카드 그림 두 장을 창 비율에 맞춘다 (Blender 가 webp 를 읽는다).
#   후드티(아래 단): 인형 줄만 가로로 잘라 2.15 : 1
#   저격수(위 단): 2줄 3개씩이라 자르면 인형이 잘린다 → 인형 부분을 가운데 두고 양옆을 배경색으로 늘려 2.13 : 1 (가장자리는 부드럽게 섞음)
# blender -b --factory-startup --python make_cards.py -- <hoodie.webp> <sniper.webp> <out_dir>
import bpy, sys, os
import numpy as np

hoodie_path, sniper_path, out_dir = sys.argv[sys.argv.index("--") + 1:][:3]

def load(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    return px.reshape(h, w, 4)[::-1].copy(), w, h          # 위에서부터 줄

def save(arr, name):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=False)
    img.pixels.foreach_set(arr[::-1].astype(np.float32).ravel())
    img.filepath_raw = os.path.join(out_dir, name + ".png")
    img.file_format = "PNG"
    img.save()
    return f"{name}: {w}x{h}"

log = []
a, w, h = load(hoodie_path)
log.append(f"hoodie src {w}x{h}")
y0, y1 = int(round(h * 0.2656)), int(round(h * 0.7305))
hoodie = a[y0:y1, :, :]
hoodie[..., 3] = 1.0
log.append(save(hoodie, "Gacha_Card_Hoodie"))

b, w, h = load(sniper_path)
log.append(f"sniper src {w}x{h}")
y0, y1 = int(round(h * 0.0977)), int(round(h * 0.9375))
crop = b[y0:y1, :, :3]
ch, cw = crop.shape[:2]
W = int(round(ch * 2.13))
edge = np.concatenate([crop[:, :24], crop[:, -24:]], axis=1).reshape(-1, 3)
bg = np.median(edge, axis=0)
canvas = np.empty((ch, W, 3), dtype=np.float32)
canvas[:] = bg
x0 = (W - cw) // 2
fade = 72
t = np.ones(cw, dtype=np.float32)
ramp = np.linspace(0.0, 1.0, fade, dtype=np.float32)
t[:fade] = ramp
t[-fade:] = ramp[::-1]
canvas[:, x0:x0 + cw] = crop * t[None, :, None] + bg[None, None, :] * (1.0 - t[None, :, None])
out = np.concatenate([canvas, np.ones((ch, W, 1), dtype=np.float32)], axis=2)
log.append(save(out, "Gacha_Card_Sniper"))
log.append(f"sniper bg={np.round(bg, 3).tolist()}")
print("CARDS_DONE", " | ".join(log))
