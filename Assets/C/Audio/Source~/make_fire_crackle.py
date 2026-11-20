# 불타는 차 소리 (사용자 11/17 — 서쪽 지하 입구에 처박혀 타는 차) — 표준 라이브러리만으로 합성, 이음매 없이 반복되는 10초.
#   · 웅웅대는 불길: 낮은 소리 잡음(한 극 저역 두 번) — 천천히 출렁임
#   · 바람 쉭쉭: 가운데 대역 잡음 — 조금 빠르게 출렁임
#   · 탁탁 튀는 소리: 작은 것 초당 약 18번 + 큰 것 초당 약 2번 (짧은 잡음 덩어리, 지수 감쇠, 날카롭게 미분)
#   · 끝 0.5초를 처음 0.5초에 겹쳐 이음매를 없앤다, 큰 소리를 tanh 로 눌러 전체를 키우고 최고점 -1 dB
# 실행: python make_fire_crackle.py <출력 wav 경로>
import math, random, struct, sys, wave

SR = 44100
LOOP = 10.0
FADE = 0.5
N = int((LOOP + FADE) * SR)
rng = random.Random(1117)

def lowpass(x, cutoff):
    a = math.exp(-2.0 * math.pi * cutoff / SR)
    y, s = [0.0] * len(x), 0.0
    for i, v in enumerate(x):
        s = (1 - a) * v + a * s
        y[i] = s
    return y

def slow_mod(n, rate, depth, phase):
    # 두 사인 섞은 느린 출렁임 (0~1)
    return [1.0 - depth + depth * 0.5 * (1 + 0.6 * math.sin(2 * math.pi * rate * i / SR + phase) + 0.4 * math.sin(2 * math.pi * rate * 2.7 * i / SR + phase * 1.7)) for i in range(n)]

white = [rng.uniform(-1, 1) for _ in range(N)]
# 1) 웅웅 (약 250 Hz 아래)
roar = lowpass(lowpass(white, 260), 260)
peak = max(abs(v) for v in roar) or 1.0
roar = [v / peak for v in roar]
mod_r = slow_mod(N, 0.35, 0.45, 0.3)
# 2) 쉭쉭 (약 600~2500 Hz)
white2 = [rng.uniform(-1, 1) for _ in range(N)]
hiss_lp = lowpass(white2, 2500)
hiss_lo = lowpass(hiss_lp, 600)
hiss = [a - b for a, b in zip(hiss_lp, hiss_lo)]
peak = max(abs(v) for v in hiss) or 1.0
hiss = [v / peak for v in hiss]
mod_h = slow_mod(N, 1.6, 0.6, 1.1)

out = [0.42 * roar[i] * mod_r[i] + 0.10 * hiss[i] * mod_h[i] for i in range(N)]

# 3) 탁탁
def crackle(t0, amp, dur, sub):
    start = int(t0 * SR)
    for s in range(sub):
        off = start + int(rng.uniform(0, dur * 0.8) * SR) if s else start
        length = int(rng.uniform(0.3, 1.0) * dur * SR) + 8
        prev = 0.0
        a = amp * (1.0 if s == 0 else rng.uniform(0.3, 0.7))
        for k in range(length):
            idx = off + k
            if idx >= N:
                break
            v = rng.uniform(-1, 1)
            hp = v - prev   # 미분 = 날카롭게
            prev = v
            out[idx] += a * hp * math.exp(-k / (length * 0.25))

t = 0.0
while True:
    t += rng.expovariate(18.0)
    if t >= LOOP + FADE:
        break
    crackle(t, rng.uniform(0.08, 0.28), rng.uniform(0.001, 0.006), 1)
t = 0.0
while True:
    t += rng.expovariate(2.2)
    if t >= LOOP + FADE:
        break
    crackle(t, rng.uniform(0.35, 0.75), rng.uniform(0.008, 0.025), rng.randint(2, 5))

# 4) 이음매: 끝 FADE 초를 처음 FADE 초에 겹친다
L, F = int(LOOP * SR), int(FADE * SR)
loop = out[:L]
for i in range(F):
    w = i / F
    loop[i] = loop[i] * w + out[L + i] * (1 - w)
# 큰 탁 소리만 튀지 않게 부드럽게 눌러(tanh) 전체 크기를 올린다
peak = max(abs(v) for v in loop) or 1.0
loop = [math.tanh(3.0 * v / peak) for v in loop]
peak = max(abs(v) for v in loop) or 1.0
gain = 0.89 / peak
path = sys.argv[1]
with wave.open(path, "wb") as wf:
    wf.setnchannels(1); wf.setsampwidth(2); wf.setframerate(SR)
    wf.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v * gain)) * 32767)) for v in loop))
print("wrote", path, len(loop) / SR, "s, gain", round(gain, 3))
