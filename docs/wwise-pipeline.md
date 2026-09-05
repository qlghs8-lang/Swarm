# Swarm 오디오 파이프라인 설계

**Wwise 2025.1.10 / Unity 6000.4.3f1 / Windows**

이 문서는 "무엇을 만들었는가"보다 **"무엇을 어디서 결정하는가"**를 적는다. 도구 사용법은
Audiokinetic 문서에 있고, 여기 적을 가치가 있는 것은 이 프로젝트에서 내린 경계와 그 이유다.

> 📋 프로젝트 전반: [../CLAUDE.md](../CLAUDE.md) · 아트 파이프라인: [art-plan.md](./art-plan.md)

---

## 0. 한 줄 원칙

> **게임 코드는 사실만 보고한다. 소리에 대한 판단은 전부 Wwise가 한다.**

이 문장이 이 문서의 전부다. 나머지는 이 원칙을 어떻게 물리적으로 강제하느냐에 대한 이야기다.

---

## 1. 경계 — 누가 무엇을 결정하는가

| | 책임 | 금지 |
|---|---|---|
| **게임 코드** | 사실 보고 — "적 32마리", "체력 47%", "상태가 LevelUp으로 바뀜" | 소리에 대한 어떤 판단도 하지 않음 |
| **Wwise** | 그 사실을 소리로 번역 — 무엇이 언제 얼마나 들릴지 | — |

### 왜 이렇게 나누는가

`if (enemyCount > 35) PlayDrums()` 가 한 줄이라도 들어가는 순간 파이프라인은 죽는다.
"드럼이 너무 일찍 들어온다"를 고치려고 C# 파일을 열고, 컴파일하고, 커밋해야 하기 때문이다.

지금 구조에서 같은 수정은 **Wwise에서 크로스페이드 곡선을 끌어 35 → 50으로 옮기는 것**으로
끝난다. 코드는 건드리지 않는다. 사운드 담당이 프로그래머 없이 밸런싱할 수 있다 — 이것이
파이프라인을 두는 유일한 이유다.

**게임 코드에는 "많다/적다"라는 판단조차 없어야 한다.** `SetEnemyCount(32)`는 사실이고,
`SetIntensity(High)`는 판단이다. 후자는 이미 게임 코드가 소리를 결정한 것이다.

### AudioDirector — 경계를 물리적으로 만드는 것

게임 코드는 `AkUnitySoundEngine`을 **직접 부르지 않는다.** 전부 `AudioDirector`를 거친다.

```
Player / Enemy / UI / GameManager
            │
            ▼
      AudioDirector          ← Wwise를 아는 유일한 클래스
            │
            ▼
      AkUnitySoundEngine
```

이렇게 두는 이유가 셋이다.

1. **교체 가능성** — Wwise를 걷어내도 `AudioDirector` 하나만 비우면 게임이 컴파일된다
2. **이름의 단일 지점** — 이벤트·RTPC 이름이 흩어지지 않는다 (§2와 직결)
3. **가시성** — 어떤 게임 데이터가 오디오로 나가는지 파일 하나만 열면 전부 보인다

기존 `BackgroundMusic` / `SoundEffects`의 public API는 **그대로 둔다.** 본문만 Wwise 호출로
바꾼다. `GameSettings.cs`에 이미 "볼륨 값은 스피커에 적용하는 쪽이 소유한다"고 적혀 있고,
그 규칙이 백엔드를 바꿔도 그대로 성립한다. 결과적으로 `GameManager`·`ResultSequence`·
`GameSettings`·`SettingsMenu`는 한 줄도 수정하지 않는다.

---

## 2. 계약 — 게임과 Wwise가 주고받는 것

경계를 넘는 것은 아래 목록이 전부다. **이 표가 곧 계약이다.**

### Events

| 이름 | 대상 | 호출 지점 |
|---|---|---|
| `Play_Music` | Music_Loop | 게임 시작 시 1회 |
| `Stop_Music` | Music_Loop | 씬 이탈 |
| `Play_XP_Pickup` | SFX\XP_Pickup | `ExperiencePickup.TryCollect` |
| `Play_Object_Pickup` | SFX\Object_Pickup | `GoldPickup` / `MagnetPickup` / `HealthPackPickup` |

### Game Parameters (RTPC)

| 이름 | 범위 | 게임 쪽 출처 | Wwise 쪽 대상 |
|---|---|---|---|
| `Enemy_Count` | 0–100 | `EnemySpawner.ActiveEnemyCount` | Music_Bed 블렌드 크로스페이드 (§7 참고 — 현재 재생 경로가 아니다) |
| `Player_Health` | 0–100 | `PlayerHealth.OnHealthChanged` (백분율) | Music·SFX 버스 Lowpass |
| `Volume_Music` | 0–100 | `GameSettings.BgmVolume` × 100 | Music 버스 Volume |
| `Volume_SFX` | 0–100 | `GameSettings.SfxVolume` × 100 | SFX·UI 버스 Volume |
| `Music_Pitch` | 0.1–3.0 | `BackgroundMusic.SetPitch` | Music 버스 Pitch |

### States

`Game_State` : `Title` / `Playing` / `LevelUp` / `Dead`

| 상태 | Wwise에 걸린 것 |
|---|---|
| `LevelUp` | Music 버스 Volume −8dB, SFX 버스 −4dB. 카드 고르는 동안 소리가 물러난다 |
| `Title` / `Playing` / `Dead` | 아직 오프셋 없음. 그룹에는 물려 있으므로 값만 넣으면 바로 산다 |

버스의 State 속성 오버라이드는 Wwise UI에서만 걸 수 있다 (§8). 게임 코드는 상태 이름만 보내고
덕킹 값은 모른다 — 과하다 싶으면 Wwise에서 숫자만 바꾸면 되고 C#은 컴파일조차 하지 않는다.

### 계약을 타입으로 강제하기

문자열로 이벤트를 부르면 **오타가 컴파일을 통과하고 런타임에 조용히 아무 일도 안 한다.**
이건 파이프라인이 아니라 지뢰밭이다.

Wwise가 뱅크와 함께 뱉는 `Main.json`을 읽어 C# 상수 파일을 생성한다.

생성기는 `Assets/_Project/Scripts/Editor/WwiseIdGenerator.cs`, 결과물은
`Assets/_Project/Scripts/Audio/WwiseIds.generated.cs`다. `.wwu`(저작 파일)가 아니라
**생성된 뱅크**를 읽는다 — 런타임이 실제로 로드하는 것이 뱅크이므로, 저작 쪽에서 이름을 바꾸고
뱅크를 다시 만들지 않았다면 게임이 보는 진실은 여전히 옛 이름이기 때문이다.

```csharp
// Assets/_Project/Scripts/Audio/WwiseIds.generated.cs
// 자동 생성 — 손으로 고치지 말 것. Swarm → Audio → Rebuild 로 갱신된다.
public static class WwiseIds
{
    public static class Events { public const uint Play_XP_Pickup = 2836847905; /* ... */ }
    public static class RTPC   { public const uint Enemy_Count    = 1539036812; /* ... */ }
}
```

이러면 오타가 컴파일 에러가 되고, Wwise에서 이벤트 이름을 바꾸면 재생성 시 빌드가 깨져
즉시 알게 된다. **계약이 문서가 아니라 타입 시스템에 들어간다.**

### 이름 규칙

- 이벤트 — `<동작>_<대상>` : `Play_XP_Pickup`, `Stop_Music`
- RTPC — `<명사>_<속성>` : `Enemy_Count`, `Player_Health`. **판단이 들어간 이름 금지**
  (`Music_Intensity`는 이미 "강도"라는 해석이 이름에 박힌 것이다 — 게임은 적 수만 안다)
- State — `<영역>_<상태>` : `Game_State` 그룹 아래 상태명은 단수형

---

## 3. 자산 흐름

```
Audio_src/*.wav                  소스. 사람이 만든 것. 커밋함
      │  Wwise Import
      ▼
Swarm_WwiseProject/Originals/    Wwise가 관리하는 원본 사본. 커밋함
      │  변환 (Vorbis / PCM)
      ▼
Swarm_WwiseProject/.cache/*.wem  중간 산출물. 커밋 안 함
      │  SoundBank Generate
      ▼
GeneratedSoundBanks/Windows/     Main.bnk + Init.bnk. 커밋 안 함
      │  복사  ← 이 화살표가 자동화 대상 (§4)
      ▼
Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/   런타임이 읽는 곳
```

> 🚨 **함정: 생성 위치 ≠ 런타임 참조 위치**
>
> Wwise는 `Swarm_WwiseProject/GeneratedSoundBanks/`에 만들고, Unity 런타임은
> `Assets/StreamingAssets/Audio/GeneratedSoundBanks/<플랫폼>/`에서 읽는다.
> 그 사이를 잇는 복사가 `WwiseSettings.xml`의 `CopySoundBanksAsPreBuildStep: true` —
> **빌드 시점에만 돈다.** 에디터 Play 모드는 커버되지 않는다.
>
> 실제로 이 구멍 때문에 `Bank Load Failed Name: 1355168291`(= `Init`의 FNV 해시) 에러가
> 프레임마다 쏟아진 적이 있다. 뱅크에는 아무 문제가 없었고 복사가 안 됐을 뿐이었다.
> §4의 에디터 메뉴는 이 사고를 다시 겪지 않기 위한 것이다.

> 🚨 **함정 2: 사용자 뱅크는 자동으로 로드되지 않는다**
>
> `AkInitializer`가 알아서 올려주는 것은 **Init 뱅크뿐**이다. 이벤트·구조·미디어가 든 뱅크는
> 누군가 명시적으로 `LoadBank`를 불러야 한다. 빠뜨리면 엔진은 멀쩡히 뜨고 RTPC도 정상으로
> 흘러가는데 이벤트만 전부 `Event ID not found`로 실패한다 — 뱅크 파일도 제자리에 있고
> 복사도 됐고 ID도 맞아서, 로그만 봐서는 원인이 보이지 않는다. 실제로 겪었다.
>
> `AudioDirector.EnsureBanks()`가 담당한다. 씬에 `AkBank` 컴포넌트를 두는 방법도 있지만,
> 그러면 뱅크 로드가 씬 설정에 흩어져 코드만 봐서는 알 수 없게 된다. §5의 마지막 검사가
> "이벤트가 든 뱅크를 코드가 올리는가"를 대조하는 것이 이 때문이다.

### BGM은 한 단계가 더 있다

```
Music_Layer_{Base,Mid,High}.wav
      │  render_demo.py --game   ← 레이어를 합쳐 한 파일로 굽는다
      ▼
Audio_src/Music_BGM_Loop.wav     이것이 게임에 들어가는 BGM
```

`--game`으로 실행하면 체력 로우패스를 굽지 않는다. 게임에서는 그것을 Wwise의 `Player_Health`
RTPC가 실시간으로 걸기 때문에, 파일에까지 넣으면 체력이 만땅인데도 매 바퀴 먹먹해지고 실제로
체력이 낮을 때는 두 번 걸린다. `--game` 없이 실행하면 감상용 데모(`Claude outputs/`)가 나온다.

### 음원 교체 절차

`Audio_src/`에 **같은 파일명으로 덮어쓰고** → Wwise에서 해당 Sound Reimport → Rebuild.
Wwise 구조·이벤트·RTPC 배선은 전부 유지된다.

`gen_placeholder.py`는 지우지 않는다. 각 파일이 무엇을 대체해야 하는 자리였는지가
스크립트에 남아 있는 것이, 나중에 진짜 음원을 만들 때의 사양서가 된다.

---

## 4. 자동화

### Unity 에디터: `Swarm → Audio → Rebuild` (`Ctrl+Shift+A`)

`Assets/_Project/Scripts/Editor/AudioPipeline.cs`. 버튼 하나가 아래를 순서대로 수행한다.

1. `GeneratedSoundBanks/` → `StreamingAssets/` 복사 (플랫폼 폴더별로, 옛 산출물은 먼저 지운다)
2. 뱅크 메타데이터 → `WwiseIds.generated.cs` 재생성
3. `AssetDatabase.Refresh()`

경로는 하드코딩하지 않고 `Assets/WwiseSettings.xml`의 `RootOutputPath` /
`WwiseStreamingAssetsPath`를 읽는다. 설정을 바꾸면 이 메뉴도 따라간다.

**SoundBank 생성 자체는 여기서 하지 않는다.** Wwise Authoring이나 WAAPI(MCP)의 몫이고, 이
메뉴는 "이미 만들어진 것을 Unity에 반영한다"까지만 책임진다. 통합이 제공하는 생성 메뉴가
2025.1에는 없어서, 있지도 않은 것을 부르는 대신 경계를 분명히 두었다.

이게 없으면 **"뱅크 복사 깜빡해서 소리 안 남"이 주 단위로 반복된다.**

### 빌드 시점

`IPreprocessBuildWithReport`(callbackOrder −100)로 같은 절차를 한 번 더 강제하고, 실패하면
`BuildFailedException`으로 빌드를 세운다. 사람이 잊어도 빌드는 잊지 않는다.

---

## 5. 검증 — `Audio_src/lint_audio.py`

파이프라인은 **잘못된 것을 사람이 발견하기 전에** 잡아야 한다. 아래 항목은 전부 이 프로젝트에서
실제로 겪었거나, 겪으면 원인을 찾기 어려운 종류의 실패다. 실패가 있으면 종료 코드 1을 내므로
커밋 훅이나 CI에 그대로 걸 수 있다.

| 검사 | 기준 | 왜 |
|---|---|---|
| 음악 레이어 길이 일치 | 3개 전부 동일 | 다르면 루프가 점점 밀린다. 재현이 어려운 종류의 버그 |
| 루프 이음매 불연속 | < 0.005 | 매 바퀴 "틱" 소리. 타악기 레이어는 어택에 가려지므로 경고로만 |
| 채널 / 샘플레이트 / 비트뎁스 | 모노 / 48kHz / 16bit | 스테레오면 Wwise 공간 처리가 죽는다 |
| 픽업 효과음 길이 | ≤ 0.3초 | 자석으로 수십 개가 한 프레임에 들어올 때 뭉갠다 |
| `Audio_src` ↔ Wwise `Originals` | 해시 일치 | 소스를 고치고 Reimport를 안 하면 옛 소리가 계속 난다 |
| `GeneratedSoundBanks` ↔ `StreamingAssets` | 해시 일치 | §3의 함정. 복사를 잊으면 옛 뱅크가 로드된다 |
| `WwiseIds` ↔ 뱅크 | 이름·ID 일치 | 뱅크를 다시 만들고 재생성을 안 한 어긋남 |
| 이벤트가 든 뱅크를 코드가 로드하는가 | `Banks.<이름>` 참조 존재 | §3 함정 2. 빠뜨리면 이벤트가 전부 조용히 실패한다 |

뒤의 세 항목이 핵심이다. **서로 다른 두 곳에 있는 같은 것을 대조하는 검사**는 둘이 따로 놀 수
있다는 사실을 인정해야 나오는 발상이고, 이 파이프라인에서 가장 값비싼 실패 — 에러 없이 소리만
안 나거나 옛 소리가 나는 것 — 를 정확히 겨냥한다.

## 6. 버전관리 경계

원칙: **재생성 가능한 것은 커밋하지 않는다. 대신 재생성 절차를 문서화한다.**

| 커밋함 | 제외함 | 제외 이유 |
|---|---|---|
| `Audio_src/*.wav`, `*.py` | `.cache/` | 변환 중간물 |
| `Swarm_WwiseProject/*.wwu` | `GeneratedSoundBanks/` | 뱅크는 산출물 |
| `Assets/Wwise/` C# + `.meta` | `StreamingAssets/` 뱅크 | 위와 동일 |
| | `Assets/Wwise/API/Runtime/Plugins/` | 743MB 네이티브 바이너리. Launcher가 재생성 |
| | `Assets/Wwise/Documentation/` | 59MB 오프라인 문서 |
| | `Tools/Wwise-MCP/` | 30MB 실행 파일 |

`Assets/Wwise/`를 **통째로** 제외하지 않은 이유: 재통합 시 `.meta` GUID가 새로 생성되어
씬·프리팹에 붙은 `AkGameObj` 등의 참조가 전부 끊어진다. C#과 `.meta`는 남기고 네이티브
바이너리만 제외해서, 831MB → 약 30MB로 줄이면서 참조는 보존했다.

**재생성 절차**: Wwise Launcher → UNITY 탭 → *Integrate Wwise into Project*

---

## 7. 하지 않은 것과 그 이유

1인 1개월 프로젝트다. 아래는 의도적으로 만들지 않았다.

| 안 한 것 | 이유 |
|---|---|
| CI에서 뱅크 빌드 | Wwise 라이선스와 전용 머신이 필요하다. 1인 프로젝트에서는 비용만 발생 |
| 다중 플랫폼 뱅크 | Windows 단일 타깃. 플랫폼이 늘면 Launcher에서 추가하고 §4에 platform 인자만 늘리면 된다 |
| SoundBank 분할 | 뱅크 하나로 충분한 규모(2.3MB). 분할 기준은 "씬 단위 + 상시 상주 분리"로 정해두되 지금은 적용하지 않는다 |
| Addressables 연동 | 에셋 규모가 이 복잡도를 정당화하지 못한다 |
| 오디오 담당자용 별도 브랜치 전략 | 혼자 작업한다 |
| **음악 레이어의 런타임 크로스페이드** | 아래 참고 |

### BGM을 구워서 쓰기로 한 것

인게임 BGM은 세 레이어를 실시간으로 섞지 않고, `render_demo.py --game`이 미리 합쳐 구운
64초 루프 한 파일(`Music_BGM_Loop.wav`)이다. 곡의 전개 — 아르페지오가 붙고 드럼이 들어왔다가
역순으로 벗겨지는 흐름 — 이 곡 자체의 완성도로 판단되어 그대로 고정하기로 했다.

**대가**: `Enemy_Count` RTPC는 저작 구조(`Music_Bed`의 Blend Track 크로스페이드)에 그대로
남아 있지만, 현재 재생 경로가 `Music_Loop`이므로 소리에 영향을 주지 않는다. 게임 코드는
여전히 매 프레임 적 수를 보고하고 프로파일러에도 값이 보이지만, 받는 쪽이 없다.

`Player_Health` → Lowpass는 살아 있다. 그래서 구운 파일에서는 로우패스를 뺐다 — 두 번 걸리면
안 되기 때문이다. **한쪽을 구웠으면 다른 쪽도 구워야 한다고 생각하기 쉬운데, 그 반대다.**

되돌리려면 `Play_Music` / `Stop_Music` 이벤트의 Target을 `Music_Bed`로 되돌리기만 하면 된다.
게임 코드는 한 줄도 바뀌지 않는다 — 경계를 그어둔 값이 여기서 나온다.

이 표는 부록이 아니라 문서의 일부다. **무엇을 안 했는지와 그 이유를 적어두는 것이,
판단을 했다는 증거다.** 전부 다 만든 파이프라인은 대개 아무 판단도 하지 않은 파이프라인이다.

---

## 8. 알려진 부채

| 항목 | 내용 |
|---|---|
| `render_demo.py`의 이중 소스 | Wwise의 크로스페이드·LPF 곡선을 손으로 복제해 두었다. Wwise만 고치면 미리듣기가 조용히 거짓말한다. WAAPI로 읽어오게 바꿔야 한다 |
| `Game_State` 미배선 | 상태는 정확히 쏘지만 받는 쪽이 없다 |
| 8초 루프 | 10분 플레이 = 75바퀴. 이음매는 잡혔지만 반복 자체는 남는다. 32초(16마디) + 화성 진행이 정석 |
| `Enemy_Count` 최대 0.5초 지연 | `SweepActiveEnemies()` 주기. RTPC 용도로는 무해하나 정확한 값이 필요하면 부적합 |
| State 오버라이드는 자동화 불가 | Wwise-MCP의 `set_object`가 오디오 노드의 `@StateGroups` / `@States`를 노출하지 않아 `Invalid property`로 거부된다. 값 조정은 Wwise UI 작업으로 남는다 |
| BGM이 6MB PCM | Conversion Settings가 기본값이라 무압축이다. 음악은 Vorbis + 스트리밍이 정석 |

---

## 9. 현재 상태

**완료**

- Wwise 구조 — 버스, RTPC 5종, Blend Container 크로스페이드, 이벤트 4종, SoundBank
- 게임 코드 연결 — `AudioDirector` / `WwiseGameSync`, 픽업 4종, 상태 전환 4개 지점
- `WwiseIds.generated.cs` 자동 생성 — 이벤트 이름이 타입으로 굳어 오타가 컴파일 에러가 된다
- `Swarm → Audio → Rebuild` 메뉴 + 빌드 전 훅
- `Audio_src/lint_audio.py` 검사 7종
- `Game_State` 배선 — `LevelUp`에서 Music −8dB / SFX −4dB
- 버전관리 경계, 플레이스홀더 음원과 생성·렌더 스크립트

**미완**

- §8의 부채 항목들 (BGM 무압축 6MB, `render_demo.py` 이중 소스, 8초 루프 소스, `Enemy_Count` 지연)

파이프라인 자체는 한 바퀴가 닫혔다. 소스를 고치고 → Wwise에서 굽고 → `Rebuild` 한 번으로
게임에 반영되고 → `lint_audio.py`가 어긋남을 잡는다.
