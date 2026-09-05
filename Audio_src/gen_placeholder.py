"""
Swarm 플레이스홀더 오디오 생성기.

실제 음원이 준비되기 전까지 Wwise 파이프라인(Import -> Event -> SoundBank -> Unity)을
끝까지 굴려보기 위한 임시 파일을 만든다. 파일 이름과 길이·템포는 최종본과 동일하게
잡아뒀으므로, 나중에 같은 이름의 진짜 음원으로 덮어쓰고 Wwise에서 Reimport만 하면
구조를 손대지 않고 교체된다.

음악 3레이어는 반드시 같은 길이(8초 = 120BPM 4마디)여야 한다. Blend Container가
Enemy_Count RTPC로 이 셋을 겹쳐 재생하기 때문에 길이가 어긋나면 위상이 밀린다.

한 판이 10분이면 이 8초 루프가 75번 돈다. 그래서 '한 바퀴가 그럴듯한가'보다
'이음매가 안 들리는가'가 훨씬 중요하고, 아래 두 규칙이 그것을 담당한다:

  1. 모든 부분음 주파수를 1/DUR(=0.125Hz)의 배수로 양자화한다. 8초에 정수 사이클로
     떨어지지 않는 주파수를 쓰면 루프 지점에서 파형이 불연속이 되어 매 바퀴 '틱' 소리가
     난다. C4의 실제 값 261.63Hz는 8초에 2093.04 사이클 — 이게 바로 그 경우다.
  2. 감쇠 꼬리가 파일 끝을 넘으면 잘라내지 않고 앞으로 되감아 더한다(wrap_add).
     그래야 마지막 음의 여운이 다음 바퀴 첫머리에 이어져 끊긴 자국이 남지 않는다.

    python3 gen_placeholder.py
"""
import math, struct, wave, random, os

SR = 48000
OUT = os.path.dirname(os.path.abspath(__file__))
random.seed(1)

BPM = 120.0
BEAT = 60.0 / BPM
BARS, BEATS_PER_BAR = 4, 4
DUR = BARS * BEATS_PER_BAR * BEAT          # 8.0s
N = int(DUR * SR)


def q(f):
    """주파수를 루프 길이에 정수 사이클로 떨어지도록 양자화한다."""
    return round(f * DUR) / DUR


def write(name, samples):
    peak = max(1e-9, max(abs(s) for s in samples))
    if peak > 1.0:
        samples = [s / peak for s in samples]
    path = os.path.join(OUT, name)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(
            struct.pack("<h", int(max(-1.0, min(1.0, s)) * 32000)) for s in samples))
    print("%-28s %5.2fs  peak %.2f" % (name, len(samples) / SR, peak))


def sine(f, t, phase=0.0):
    return math.sin(2 * math.pi * f * t + phase)


def wrap_add(buf, start, values):
    """파일 끝을 넘는 부분은 잘라내는 대신 앞으로 되감아 더한다."""
    n = len(buf)
    for j, v in enumerate(values):
        buf[(start + j) % n] += v


# A 마이너. 전부 루프 정수 사이클로 양자화한다.
A2, A3, C4, E4, A4, C5, E5, A5 = (q(f) for f in
                                  (110.0, 220.0, 261.63, 329.63, 440.0, 523.25, 659.25, 880.0))


def music_base():
    """
    지속되는 패드. 항상 들리는 바닥 레이어.

    저음 사인파만 쌓으면 10분 내내 먹먹하다. 5옥타브대 부분음을 얇게 얹어 공기감을 주되,
    위 레이어(아르페지오·드럼)가 들어올 자리를 뺏지 않도록 작게 둔다.

    이전 판에는 8초에 한 번 부풀었다 꺼지는 스웰이 있었는데, 그 주기가 곧 루프 주기라
    75번 반복되면 '숨쉬는 소리'로 들리며 루프 길이를 드러낸다. 그래서 스웰을 걷어내고,
    루프 길이를 정수로 나누는 4초 주기의 얕은(±12%) 흔들림만 상단에 남겼다. t=0에서
    최댓값이므로 곡이 조용하게 시작하지 않는다.
    """
    partials = [                       # (주파수, 세기, 위상)
        (A2, 0.30, 0.0),
        (A3, 0.42, 1.1),
        (C4, 0.26, 2.3),
        (E4, 0.22, 0.7),
        (A4, 0.12, 3.0),
        (C5, 0.07, 1.7),
        (E5, 0.055, 2.9),
        (A5, 0.030, 0.4),
    ]
    out = []
    for i in range(N):
        t = i / SR
        shimmer = 0.88 + 0.12 * math.cos(2 * math.pi * 2.0 * t / DUR)
        v = 0.0
        for f, amp, ph in partials:
            v += sine(f, t, ph) * amp * (shimmer if f >= A4 else 1.0)
        out.append(v * 0.20)
    return out


def music_mid():
    """8분음표 아르페지오. 적이 늘기 시작하면 올라온다."""
    out = [0.0] * N
    notes = [A3, C4, E4, A4, E4, C4, A3, C4]
    step = BEAT / 2
    k = 0
    while k * step < DUR:
        f = notes[k % len(notes)]
        start = int(k * step * SR)
        length = int(step * SR * 0.9)
        wrap_add(out, start, [
            (sine(f, j / SR) * 0.7 + sine(f * 2, j / SR) * 0.2) * math.exp(-(j / SR) * 9.0) * 0.22
            for j in range(length)])
        k += 1
    return out


def music_high():
    """킥 + 하이햇. 화면이 적으로 찰 때만 들어오는 최상단 레이어."""
    out = [0.0] * N
    beat = 0
    while beat * BEAT < DUR:
        wrap_add(out, int(beat * BEAT * SR), [                          # kick
            sine(110 * math.exp(-(j / SR) * 26), j / SR) * math.exp(-(j / SR) * 16) * 0.55
            for j in range(int(0.18 * SR))])
        for half in (0.5, 1.0):                                         # hats
            wrap_add(out, int((beat + half) * BEAT * SR), [
                random.uniform(-1, 1) * math.exp(-(j / SR) * 70) * 0.16
                for j in range(int(0.05 * SR))])
        beat += 1
    return out


def xp_pickup():
    """경험치 구슬. 짧고 밝은 2음 상승 — 초당 수십 번 울려도 지치지 않게 아주 짧게."""
    out = []
    dur = 0.14
    for i in range(int(dur * SR)):
        t = i / SR
        f = 880 if t < 0.055 else 1318.5
        env = math.exp(-((t % 0.055) if t < 0.055 else (t - 0.055)) * 26)
        out.append((sine(f, t) * 0.8 + sine(f * 2, t) * 0.2) * env * 0.6)
    return out


def object_pickup():
    """골드·자석·힐팩. XP보다 낮고 두껍게 시작해 위로 열리는 소리 — 귀로 구분되게."""
    out = []
    dur = 0.30
    for i in range(int(dur * SR)):
        t = i / SR
        body = sine(196 * (1 + t * 1.1), t) * math.exp(-t * 11) * 0.7
        chime = (sine(1174.7, t) + sine(1567.98, t) * 0.6) * math.exp(-max(0.0, t - 0.05) * 8) * 0.25
        out.append((body + chime) * 0.7)
    return out


def loop_seam(samples):
    """루프 이음매의 불연속 크기. 0에 가까울수록 '틱' 소리가 안 난다."""
    return abs(samples[0] - samples[-1])


if __name__ == "__main__":
    for name, gen in (("Music_Layer_Base.wav", music_base),
                      ("Music_Layer_Mid.wav", music_mid),
                      ("Music_Layer_High.wav", music_high)):
        s = gen()
        write(name, s)
        print("%-28s loop seam %.5f" % ("", loop_seam(s)))
    write("SFX_XP_Pickup.wav", xp_pickup())
    write("SFX_Object_Pickup.wav", object_pickup())
