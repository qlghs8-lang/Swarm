#!/usr/bin/env python3
"""
오디오 파이프라인 검사기.

파이프라인은 잘못된 것을 사람이 발견하기 전에 잡아야 한다. 여기 있는 검사 항목은 전부
이 프로젝트에서 실제로 겪었거나, 겪으면 원인을 찾기 어려운 종류의 실패다 —
소리가 안 나는데 에러도 없는 것이 오디오 작업에서 가장 비싼 실패이기 때문이다.

    python3 Audio_src/lint_audio.py          # 프로젝트 루트에서 실행해도 되고
    python3 lint_audio.py                    # Audio_src 안에서 실행해도 된다

실패가 하나라도 있으면 종료 코드 1. 커밋 훅이나 CI에 그대로 걸 수 있다.
설계 문서: docs/wwise-pipeline.md §5
"""
import hashlib
import os
import sys
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

SRC = HERE
ORIGINALS = os.path.join(ROOT, "Swarm_WwiseProject", "Originals")
GENERATED = os.path.join(ROOT, "Swarm_WwiseProject", "GeneratedSoundBanks")
STREAMING = os.path.join(ROOT, "Assets", "StreamingAssets", "Audio", "GeneratedSoundBanks")
IDS_CS = os.path.join(ROOT, "Assets", "_Project", "Scripts", "Audio", "WwiseIds.generated.cs")
AUDIO_DIR = os.path.join(ROOT, "Assets", "_Project", "Scripts")

# 인게임에서 실제로 재생되는 BGM. 레이어를 합쳐 render_demo.py가 구워낸 결과물이다.
BGM = ["Music_BGM_Loop.wav"]
# 레이어는 더 이상 게임이 직접 재생하지 않지만, BGM을 다시 구울 때의 소스로 남는다.
MUSIC_LAYERS = ["Music_Layer_Base.wav", "Music_Layer_Mid.wav", "Music_Layer_High.wav"]
PICKUP_SFX = ["SFX_XP_Pickup.wav", "SFX_Object_Pickup.wav"]
ALL_SOURCES = BGM + MUSIC_LAYERS + PICKUP_SFX

SEAM_LIMIT = 0.005          # 루프 이음매에서 허용하는 진폭 불연속
PICKUP_MAX_SEC = 0.30       # 자석에 수십 개가 한 프레임에 끌려올 때 뭉개지지 않는 한계

_failures, _warnings = [], []


def fail(check, message):
    _failures.append((check, message))


def warn(check, message):
    _warnings.append((check, message))


def read_wav(path):
    with wave.open(path, "rb") as w:
        n = w.getnframes()
        return {
            "channels": w.getnchannels(),
            "rate": w.getframerate(),
            "width": w.getsampwidth(),
            "frames": n,
            "seconds": n / w.getframerate(),
            "raw": w.readframes(n),
        }


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# ── 1. 음악 레이어 길이 일치 ──────────────────────────────────────────────────
def check_layer_lengths():
    """Blend Container가 세 레이어를 겹쳐 재생한다. 길이가 다르면 루프마다 위상이 밀린다."""
    lengths = {}
    for name in MUSIC_LAYERS:
        path = os.path.join(SRC, name)
        if not os.path.exists(path):
            fail("layer-length", f"{name} 가 없다")
            continue
        lengths[name] = read_wav(path)["frames"]

    if len(lengths) < 2:
        return
    if len(set(lengths.values())) > 1:
        detail = ", ".join(f"{k} {v}프레임" for k, v in lengths.items())
        fail("layer-length", f"음악 레이어 길이가 다르다 ({detail}). 루프가 점점 밀린다")


# ── 2. 루프 이음매 ────────────────────────────────────────────────────────────
def check_loop_seam():
    """
    마지막 샘플과 첫 샘플의 차이. 크면 매 바퀴 '틱' 소리가 난다.
    타악기 레이어는 루프 지점이 어택과 겹쳐 마스킹되므로 경고로만 둔다.
    """
    for name in BGM + MUSIC_LAYERS:
        path = os.path.join(SRC, name)
        if not os.path.exists(path):
            continue
        info = read_wav(path)
        if info["width"] != 2 or info["frames"] < 2:
            continue
        import struct
        first = struct.unpack_from("<h", info["raw"], 0)[0] / 32768.0
        last = struct.unpack_from("<h", info["raw"], (info["frames"] - 1) * 2)[0] / 32768.0
        seam = abs(first - last)
        if seam <= SEAM_LIMIT:
            continue
        if "High" in name:
            warn("loop-seam", f"{name} 이음매 {seam:.4f} — 타악기 어택에 가려지지만 확인해볼 것")
        else:
            fail("loop-seam", f"{name} 이음매 {seam:.4f} > {SEAM_LIMIT} — 매 바퀴 '틱' 소리가 난다")


# ── 3. 포맷 ───────────────────────────────────────────────────────────────────
def check_format():
    """스테레오를 넣으면 Wwise가 공간 처리를 못 한다. 2D 게임이라도 패닝은 Wwise의 몫이다."""
    for name in ALL_SOURCES:
        path = os.path.join(SRC, name)
        if not os.path.exists(path):
            continue
        info = read_wav(path)
        if info["channels"] != 1:
            fail("format", f"{name} 이 {info['channels']}채널이다. 모노여야 한다")
        if info["rate"] != 48000:
            fail("format", f"{name} 이 {info['rate']}Hz다. 48000이어야 한다")
        if info["width"] != 2:
            fail("format", f"{name} 이 {info['width'] * 8}bit다. 16이어야 한다")


# ── 4. 픽업 효과음 길이 ───────────────────────────────────────────────────────
def check_pickup_length():
    """자석 픽업은 한 프레임에 수십 개의 XP를 끌어온다. 길면 겹쳐서 뭉갠다."""
    for name in PICKUP_SFX:
        path = os.path.join(SRC, name)
        if not os.path.exists(path):
            continue
        sec = read_wav(path)["seconds"]
        if sec > PICKUP_MAX_SEC + 1e-6:
            fail("pickup-length", f"{name} 이 {sec:.2f}초다. {PICKUP_MAX_SEC}초 이하여야 한다")


# ── 5. 소스와 Wwise Originals 동기화 ──────────────────────────────────────────
def check_originals_in_sync():
    """
    Audio_src를 고치고 Wwise에서 Reimport를 안 하면, 게임에서는 옛 소리가 계속 난다.
    파일은 분명히 바꿨는데 소리가 그대로라 원인을 찾기 어려운 종류의 실패다.
    """
    if not os.path.isdir(ORIGINALS):
        warn("originals", "Wwise Originals 폴더가 없다. Wwise 프로젝트를 확인할 것")
        return

    on_disk = {}
    for dirpath, _, files in os.walk(ORIGINALS):
        for f in files:
            if f.lower().endswith(".wav"):
                on_disk[f] = os.path.join(dirpath, f)

    for name in BGM + PICKUP_SFX:
        src = os.path.join(SRC, name)
        if not os.path.exists(src):
            continue
        if name not in on_disk:
            fail("originals", f"{name} 이 Wwise에 임포트되지 않았다")
            continue
        if sha(src) != sha(on_disk[name]):
            fail("originals", f"{name} 이 Audio_src와 Wwise Originals에서 다르다 — Wwise에서 Reimport 할 것")


# ── 6. 뱅크 복사 동기화 ───────────────────────────────────────────────────────
def check_banks_copied():
    """
    Wwise는 GeneratedSoundBanks에 만들고 런타임은 StreamingAssets에서 읽는다. 그 사이 복사는
    빌드 시점에만 돌기 때문에, 뱅크를 다시 만들고 에디터에서 그냥 Play를 누르면 옛 뱅크가 로드된다.
    복사 자체가 없으면 'Bank Load Failed'가 프레임마다 쏟아진다 — 실제로 겪었다.
    """
    if not os.path.isdir(GENERATED):
        warn("bank-copy", "GeneratedSoundBanks가 없다. Wwise에서 SoundBank를 생성할 것")
        return
    if not os.path.isdir(STREAMING):
        fail("bank-copy", "StreamingAssets에 뱅크가 없다. Swarm → Audio → Rebuild 를 실행할 것")
        return

    stale = []
    for dirpath, _, files in os.walk(GENERATED):
        for f in files:
            if not f.lower().endswith((".bnk", ".json")):
                continue
            rel = os.path.relpath(os.path.join(dirpath, f), GENERATED)
            dst = os.path.join(STREAMING, rel)
            if not os.path.exists(dst) or sha(os.path.join(dirpath, f)) != sha(dst):
                stale.append(rel)

    if stale:
        head = ", ".join(stale[:4]) + (" 외" if len(stale) > 4 else "")
        fail("bank-copy", f"StreamingAssets의 뱅크가 최신이 아니다 ({head}). "
                          "Swarm → Audio → Rebuild 를 실행할 것")


# ── 7. 코드가 참조하는 이름이 뱅크에 있는가 ───────────────────────────────────
def check_code_matches_bank():
    """
    WwiseIds.generated.cs는 뱅크에서 뽑아낸 것이라 손으로 고치면 안 된다. 코드가 참조하는
    상수가 그 파일에 없으면 컴파일이 깨지므로 컴파일러가 잡아주지만, 뱅크를 다시 만들고
    재생성을 안 한 경우는 컴파일러가 모른다. 그 어긋남을 여기서 잡는다.
    """
    import glob
    import json
    import re

    if not os.path.exists(IDS_CS):
        fail("code-bank", "WwiseIds.generated.cs가 없다. Swarm → Audio → Rebuild 를 실행할 것")
        return

    ids_text = open(IDS_CS, encoding="utf-8").read()
    declared = dict(re.findall(r"public const uint (\w+) = (\d+)u;", ids_text))

    in_bank = {}
    banks_with_events = set()
    for path in glob.glob(os.path.join(STREAMING, "**", "*.json"), recursive=True):
        if os.path.basename(path).endswith("Info.json"):
            continue
        try:
            info = json.load(open(path, encoding="utf-8-sig"))["SoundBanksInfo"]
        except (ValueError, KeyError):
            continue
        for bank in info.get("SoundBanks", []):
            if bank.get("Events"):
                banks_with_events.add(bank["ShortName"])
            for section in ("Events", "GameParameters"):
                for item in bank.get(section) or []:
                    in_bank[item["Name"]] = item["Id"]
            for group in bank.get("StateGroups") or []:
                in_bank[group["Name"]] = group["Id"]
                for state in group.get("States") or []:
                    in_bank[state["Name"]] = state["Id"]

    if not in_bank:
        return  # check_banks_copied 가 이미 보고했다

    for name, value in declared.items():
        if name == "Group":
            continue                                  # State 그룹 ID는 그룹 이름으로 확인된다
        if name not in in_bank:
            fail("code-bank", f"WwiseIds의 {name} 이 뱅크에 없다 — 재생성이 필요하다")
        elif in_bank[name] != value:
            fail("code-bank", f"WwiseIds의 {name} ID가 뱅크와 다르다 "
                              f"({value} vs {in_bank[name]}) — 재생성이 필요하다")

    # 이벤트가 든 뱅크를 코드가 올리기는 하는가. AkInitializer가 자동으로 올리는 것은 Init 뱅크뿐이라,
    # 로드를 빠뜨리면 이벤트가 전부 "Event ID not found"로 조용히 실패한다.
    code_all = ""
    for dirpath, _, files in os.walk(AUDIO_DIR):
        for f in files:
            if f.endswith(".cs"):
                code_all += open(os.path.join(dirpath, f), encoding="utf-8", errors="ignore").read()
    for bank in banks_with_events:
        if f"Banks.{bank}" not in code_all:
            fail("code-bank", f"뱅크 '{bank}' 에 이벤트가 있는데 코드가 이 뱅크를 로드하지 않는다 "
                              f"— 이벤트가 전부 'Event ID not found'로 실패한다")

    # 뱅크에는 있는데 코드가 아무 데서도 안 쓰는 이벤트 — 오류는 아니지만 알아둘 가치가 있다
    code = ""
    for dirpath, _, files in os.walk(AUDIO_DIR):
        for f in files:
            if f.endswith(".cs") and f != "WwiseIds.generated.cs":
                code += open(os.path.join(dirpath, f), encoding="utf-8", errors="ignore").read()
    for name in in_bank:
        if name.startswith(("Play_", "Stop_")) and name not in code:
            warn("code-bank", f"이벤트 {name} 이 뱅크에는 있으나 코드에서 쓰이지 않는다")


CHECKS = [
    ("음악 레이어 길이 일치", check_layer_lengths),
    ("루프 이음매", check_loop_seam),
    ("오디오 포맷", check_format),
    ("픽업 효과음 길이", check_pickup_length),
    ("소스 ↔ Wwise Originals", check_originals_in_sync),
    ("뱅크 ↔ StreamingAssets", check_banks_copied),
    ("코드 ↔ 뱅크", check_code_matches_bank),
]


def main():
    for label, fn in CHECKS:
        before = len(_failures)
        try:
            fn()
        except Exception as e:                                    # noqa: BLE001
            fail("internal", f"{label} 검사 자체가 실패했다: {e}")
        print(("  FAIL  " if len(_failures) > before else "  ok    ") + label)

    if _warnings:
        print("\n경고")
        for check, msg in _warnings:
            print(f"  [{check}] {msg}")

    if _failures:
        print("\n실패")
        for check, msg in _failures:
            print(f"  [{check}] {msg}")
        print(f"\n{len(_failures)}건 실패, {len(_warnings)}건 경고")
        return 1

    print(f"\n전부 통과 ({len(_warnings)}건 경고)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
