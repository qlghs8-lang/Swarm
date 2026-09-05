"""
Wwise 없이 오프라인으로 음악 믹스를 미리 듣는 렌더러.

Wwise Soundcaster에서 슬라이더를 손으로 움직이며 듣는 것과 같은 일을, 대신 게임 한 판의
RTPC 궤적을 미리 적어두고 한 번에 렌더한다. 오디오 담당이 아니어도 "적이 이만큼 몰리면
이렇게 들린다"를 파일 하나로 공유할 수 있어서, 밸런싱 논의가 말이 아니라 소리로 이루어진다.

여기 적힌 곡선 값은 Wwise 프로젝트의 설정을 그대로 복제한 것이다. 한쪽만 고치면 미리듣기가
거짓말을 하게 되므로, Wwise에서 크로스페이드나 LPF 곡선을 바꿨다면 아래 상수도 같이 바꿀 것.

    python3 render_demo.py
"""
import numpy as np, wave, os, sys

SR = 48000
SRC = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(SRC), "Claude outputs", "Wwise_BGM_Loop.wav")

# --game 으로 실행하면 인게임 소스를 만든다. 게임에서는 체력에 따른 로우패스를 Wwise가 실시간으로
# 걸기 때문에, 파일에까지 구워 넣으면 체력이 만땅인데도 매 바퀴 먹먹해지고 실제로 체력이 낮을 때는
# 두 번 걸린다. 감상용 데모에는 남기고, 게임에 넣는 파일에서는 뺀다.
GAME_SOURCE = "--game" in sys.argv
if GAME_SOURCE:
    OUT = os.path.join(SRC, "Music_BGM_Loop.wav")

# ── Wwise 프로젝트에서 복제한 값 ────────────────────────────────────────────
# Blend Container 크로스페이드 (control input: Enemy_Count)
# (파일, 크로스페이드 구간, Wwise Volume(dB))
LAYERS = [
    ("Music_Layer_Base.wav", None, -6.0),  # 크로스페이드 없음 = 항상 100%
    ("Music_Layer_Mid.wav",  (8.0, 30.0), 0.0),
    ("Music_Layer_High.wav", (35.0, 65.0), 0.0),
]
# Music 버스의 Lowpass RTPC (control input: Player_Health)
HEALTH_LPF = [(0.0, 65.0), (25.0, 35.0), (50.0, 0.0), (100.0, 0.0)]

# ── RTPC 궤적 (초, 값) ────────────────────────────────────────────────────
# 56초 = 8초 루프 7바퀴. 원본 소스가 전부 8초 격자에 맞으므로 이 길이면 오디오 자체는
# 이음매가 없다. 남는 조건은 오토메이션뿐 — t=0과 t=DURATION의 값이 같아야 다시
# 재생될 때 볼륨과 필터가 튀지 않는다. 양 끝을 (적 5, 체력 100)으로 맞춰둔 이유다.
#
# 체력 곡선도 끝에서 100으로 되돌린다. 먹먹한 채로 끝내면 루프 지점에서 갑자기 열려
# 이음매가 드러난다. 위기가 왔다가 풀리는 모양이 되어 오히려 한 바퀴가 완결된다.
DURATION = 64.0                            # 8초 루프 8바퀴
# 올라가는 데 38초를 쓰면서 내려오는 데 13초만 쓰면 절정에서 뚝 떨어진다. 하강을 22초로
# 늘려 상승과 호흡을 맞췄다. 레이어 크로스페이드 구간(Mid 8~30, High 35~65) 덕분에
# 적 수를 계단식으로 빼면 드럼 -> 아르페지오 순으로 역순으로 떨어져 나가고 패드만 남는다.
ENEMY_COUNT = [(0, 5), (3, 12), (11, 28), (15, 30), (21, 12), (25, 20),
               (33, 62), (38, 84), (42, 84),
               (46, 70), (50, 55), (54, 38), (57, 26), (60, 15), (62, 9), (64, 5)]
PLAYER_HEALTH = [(0, 100), (31, 100), (35, 64), (39, 32), (43, 22),
                 (50, 38), (57, 70), (64, 100)]


def read(name):
    with wave.open(os.path.join(SRC, name), "rb") as w:
        assert w.getnchannels() == 1 and w.getsampwidth() == 2
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0


def automation(points, n):
    """(초, 값) 구간을 샘플 단위 곡선으로 편다."""
    t = np.arange(n) / SR
    return np.interp(t, [p[0] for p in points], [p[1] for p in points])


def crossfade_gain(rtpc, edge_in, edge_full):
    """Blend Track의 Manual/Linear 크로스페이드. edge_in에서 0, edge_full에서 1."""
    return np.clip((rtpc - edge_in) / (edge_full - edge_in), 0.0, 1.0)


def lowpass(sig, lpf_curve):
    """
    Wwise Lowpass(0-100)를 한 극 IIR로 근사한다. 정확한 재현이 아니라 감을 잡기 위한 것 —
    실제 소리는 Wwise 쪽이 기준이다. 0이면 필터를 통과시키지 않고 그대로 둔다.
    """
    cutoff = 20000.0 * np.power(np.clip(1.0 - lpf_curve / 100.0, 0.0, 1.0), 2.5)
    cutoff = np.maximum(cutoff, 180.0)
    alpha = 1.0 - np.exp(-2.0 * np.pi * cutoff / SR)
    out = np.empty_like(sig)
    y = 0.0
    for i in range(sig.size):                      # 시변 필터라 벡터화가 안 된다
        y += alpha[i] * (sig[i] - y)
        out[i] = y
    return np.where(lpf_curve > 0.5, out, sig)


def main():
    n = int(DURATION * SR)
    enemy = automation(ENEMY_COUNT, n)
    health = automation(PLAYER_HEALTH, n)

    mix = np.zeros(n)
    for name, edges, vol_db in LAYERS:
        clip = read(name)
        looped = np.resize(clip, n)                # 8초 루프를 길이에 맞춰 반복
        gain = np.ones(n) if edges is None else crossfade_gain(enemy, *edges)
        gain = gain * (10.0 ** (vol_db / 20.0))    # Wwise Volume 속성을 그대로 반영
        mix += looped * gain
        print("%-24s %-20s %+.1f dB" % (name, "always on" if edges is None
                                        else "fade %g -> %g" % edges, vol_db))

    if GAME_SOURCE:
        print("%-24s %s" % ("(lowpass)", "생략 — Wwise의 Player_Health RTPC가 실시간으로 건다"))
    else:
        lpf = np.interp(health, [p[0] for p in HEALTH_LPF], [p[1] for p in HEALTH_LPF])
        mix = lowpass(mix, lpf)

    mix *= 0.85 / max(1e-9, np.abs(mix).max())
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((mix * 32000).astype("<i2").tobytes())
    print("\n-> %s  (%.1fs)" % (OUT, DURATION))


if __name__ == "__main__":
    main()
