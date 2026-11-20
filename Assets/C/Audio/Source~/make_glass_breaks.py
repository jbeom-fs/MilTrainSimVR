# 유리 깨짐 소리 4개 만들기 (11/18) — 원본 mp3 3개는 Assets/C/Audio/Source~/glass/ (사용자가 준 Pixabay 소리, 저장소엔 안 올림)
# 실행: blender -b --factory-startup --python make_glass_breaks.py -- <원본 폴더> Assets/C/Audio/Glass
#   511316 은 1.0 ~ 1.6 초 사이 가장 조용한 곳에서 둘로 자른다 → 4개. 모노 48 kHz · 앞뒤 무음 자르기 · 가장 큰 0.3 초 RMS -15 dBFS
#   피치는 밝기(스펙트럼 중심) 차이를 절반쯤 좁히게 (±12% 까지). Blender 의 aud + numpy 만 쓴다 (이 PC 엔 ffmpeg · PIL 이 없다)
import sys, os, json, wave
import numpy as np
import aud

src, dst = sys.argv[sys.argv.index("--") + 1:][:2]
os.makedirs(dst, exist_ok=True)
RATE = 48000


def load(name):
    s = aud.Sound(os.path.join(src, name))
    r = int(s.specs[0])
    d = np.asarray(s.data(), dtype=np.float64)
    mono = d.mean(axis=1) if d.ndim > 1 else d
    if r != RATE:   # 선형 보간으로 48 kHz
        t = np.arange(int(len(mono) * RATE / r)) * (r / RATE)
        mono = np.interp(t, np.arange(len(mono)), mono)
    return mono


def env_db(x, win):
    frames = len(x) // win
    e = np.array([np.sqrt(np.mean(x[i * win:(i + 1) * win] ** 2)) + 1e-9 for i in range(frames)])
    return 20 * np.log10(e)


def trim(x):
    win = int(RATE * 0.005)
    db = env_db(x, win)
    top = db.max()
    on = np.where(db > top - 35)[0]
    start = max(0, on[0] * win - int(RATE * 0.004))
    alive = np.where(db > top - 50)[0]
    end = min(len(x), (alive[-1] + 1) * win + int(RATE * 0.06))
    y = x[start:end].copy()
    fi, fo = int(RATE * 0.002), min(int(RATE * 0.05), len(y) // 3)
    y[:fi] *= np.linspace(0, 1, fi)
    y[-fo:] *= np.linspace(1, 0, fo)
    return y


def centroid(x):
    win = int(RATE * 0.02)
    db = env_db(x, win)
    loud = np.where(db > db.max() - 20)[0]
    seg = x[loud[0] * win:(loud[-1] + 1) * win]
    spec = np.abs(np.fft.rfft(seg * np.hanning(len(seg))))
    f = np.fft.rfftfreq(len(seg), 1.0 / RATE)
    return float((spec * f).sum() / spec.sum())


def loud_rms(x):
    win = int(RATE * 0.3)
    best = 0.0
    for i in range(0, max(1, len(x) - win), int(RATE * 0.02)):
        best = max(best, float(np.sqrt(np.mean(x[i:i + win] ** 2))))
    return best


def pitch(x, f):
    t = np.arange(int(len(x) / f)) * f
    return np.interp(t, np.arange(len(x)), x)


def write(path, x):
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


# 1) 자르기
a = load("dragon-studio-glass-shattering-511316.mp3")
win = int(RATE * 0.01)
db = env_db(a, win)
lo, hi = int(1.0 / 0.01), int(1.6 / 0.01)          # 1.0 ~ 1.6 초 사이에서 가장 조용한 곳
cut = (lo + int(np.argmin(db[lo:hi]))) * win
clips = [("511316 앞", a[:cut]), ("511316 뒤", a[cut:]),
         ("83809", load("freesound_community-breaking-glass-83809.mp3")),
         ("631488", load("dragon-studio-glass-shatter-631488.mp3"))]
clips = [(n, trim(x)) for n, x in clips]

# 2) 피치: 밝기 차이를 절반쯤 좁힌다
cents = [centroid(x) for _, x in clips]
target = float(np.exp(np.mean(np.log(cents))))
report = {"cut_s": round(cut / RATE, 3), "target_centroid": round(target)}
out = []
for (n, x), c in zip(clips, cents):
    f = float(np.clip((target / c) ** 0.5, 0.88, 1.12))
    y = pitch(x, f)
    out.append((n, y, c, f))

# 3) 음량: 가장 큰 0.3 초 RMS 를 -15 dBFS 로, 봉우리가 -0.5 dBFS 를 넘으면 그만큼 낮춘다
TARGET = 10 ** (-15 / 20)
for i, (n, y, c, f) in enumerate(out, 1):
    y = y * (TARGET / loud_rms(y))
    p = float(np.max(np.abs(y)))
    if p > 0.944:
        y *= 0.944 / p
    path = os.path.join(dst, f"C_GlassBreak_{i}.wav")
    write(path, y)
    report[f"C_GlassBreak_{i}"] = {"from": n, "seconds": round(len(y) / RATE, 2), "centroid_before": round(c),
                                    "pitch": round(f, 3), "centroid_after": round(centroid(y)), "peak_db": round(20 * np.log10(np.max(np.abs(y))), 1)}
print("GLASS " + json.dumps(report, ensure_ascii=False))
