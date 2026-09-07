# Swarm 오디오 파이프라인 설계

**Wwise 2025.1.10 / Unity 6000.4.3f1 / Windows**

이 문서는 "무엇을 만들었는가"보다 "무엇을 어디서 결정하는가"를 기술한다. 도구 사용법은
Audiokinetic 공식 문서를 따르며, 여기에는 이 프로젝트에서 정한 책임 경계와 그 근거를 정리한다.

> 📋 프로젝트 전반: [../CLAUDE.md](../CLAUDE.md) · 아트 파이프라인: [art-plan.md](./art-plan.md)

---

## 0. 설계 원칙

> 게임 코드는 사실만 보고하고, 소리에 대한 판단은 전부 Wwise가 담당한다.

이후 내용은 이 원칙을 구조적으로 강제하기 위한 방법을 정리한 것이다.

---

## 1. 경계 — 누가 무엇을 결정하는가

| | 책임 | 금지 |
|---|---|---|
| **게임 코드** | 사실 보고 — "적 32마리", "체력 47%", "상태가 LevelUp으로 바뀜" | 소리에 대한 어떤 판단도 하지 않음 |
| **Wwise** | 그 사실을 소리로 번역 — 무엇이 언제 얼마나 들릴지 | — |

### 왜 이렇게 나누는가

게임 코드에 `if (enemyCount > 35) PlayDrums()` 같은 판단이 들어가면, "드럼이 너무 일찍
들어온다"는 수정 하나에도 C# 파일 수정·컴파일·커밋이 필요해진다.

현재 구조에서는 Wwise의 크로스페이드 곡선을 35에서 50으로 조정하는 것으로 같은 수정이 끝나며,
게임 코드는 수정하지 않는다. 사운드 담당자가 프로그래머 없이 밸런싱할 수 있도록 하는 것이
이 파이프라인의 목적이다.

게임 코드에는 "많다/적다"와 같은 해석이 포함되지 않아야 한다. `SetEnemyCount(32)`는 사실
전달이고, `SetIntensity(High)`는 해석이 포함된 호출이다.

### AudioDirector — 경계를 물리적으로 만드는 것

게임 코드는 `AkUnitySoundEngine`을 직접 호출하지 않고, 전부 `AudioDirector`를 경유한다.

```
Player / Enemy / UI / GameManager
            │
            ▼
      AudioDirector          ← Wwise를 아는 유일한 클래스
            │
            ▼
      AkUnitySoundEngine
```

이 구조를 택한 이유는 세 가지다.

1. **교체 가능성** — Wwise를 제거하더라도 `AudioDirector`만 수정하면 게임이 컴파일된다
2. **이름의 단일 관리 지점** — 이벤트·RTPC 이름이 여러 곳에 흩어지지 않는다 (§2와 직결)
3. **가시성** — 어떤 게임 데이터가 오디오로 전달되는지 파일 하나로 확인할 수 있다

기존 `BackgroundMusic` / `SoundEffects`의 public API는 유지하고 내부 구현만 Wwise 호출로
교체한다. `GameSettings.cs`에 "볼륨 값은 스피커에 적용하는 쪽이 소유한다"는 규칙이 명시되어
있고, 이 규칙은 백엔드를 교체해도 그대로 성립한다. 따라서 `GameManager`·`ResultSequence`·
`GameSettings`·`SettingsMenu`는 수정하지 않는다.

---

## 2. 계약 — 게임과 Wwise가 주고받는 것

경계를 넘나드는 데이터는 아래 목록이 전부이며, 이 표가 게임과 Wwise 사이의 계약에 해당한다.

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
| `LevelUp` | Music 버스 Volume −8dB, SFX 버스 −4dB. 카드 선택 중에는 사운드를 낮춘다 |
| `Title` / `Playing` / `Dead` | 오프셋 미지정. 그룹에는 연결되어 있어 값만 입력하면 즉시 적용된다 |

버스의 State 속성 오버라이드는 Wwise UI에서만 지정할 수 있다(§8). 게임 코드는 상태 이름만
전달하고 덕킹 값은 알지 못하므로, 덕킹 정도를 조정할 때 C# 코드를 수정하거나 다시 컴파일할
필요가 없다.

### 계약을 타입으로 강제하기

문자열로 이벤트를 호출하면 오타가 컴파일을 통과하고 런타임에는 아무 동작 없이 실패한다.
원인을 추적하기 어려운 유형의 오류다.

Wwise가 뱅크와 함께 뱉는 `Main.json`을 읽어 C# 상수 파일을 생성한다.

생성기는 `Assets/_Project/Scripts/Editor/WwiseIdGenerator.cs`, 결과물은
`Assets/_Project/Scripts/Audio/WwiseIds.generated.cs`다. `.wwu`(저작 파일)가 아니라
생성된 뱅크를 읽는다. 런타임이 실제로 로드하는 대상이 뱅크이므로, 저작 쪽에서 이름을 변경한 뒤
뱅크를 다시 생성하지 않았다면 게임이 참조하는 이름은 이전 이름으로 남기 때문이다.

```csharp
// Assets/_Project/Scripts/Audio/WwiseIds.generated.cs
// 자동 생성 파일. 직접 수정하지 않는다. Swarm → Audio → Rebuild로 갱신한다.
public static class WwiseIds
{
    public static class Events { public const uint Play_XP_Pickup = 2836847905; /* ... */ }
    public static class RTPC   { public const uint Enemy_Count    = 1539036812; /* ... */ }
}
```

이 구조에서는 오타가 컴파일 오류로 드러나고, Wwise에서 이벤트 이름을 변경하면 재생성 시 빌드가
실패해 즉시 확인할 수 있다. 계약이 문서가 아니라 타입 시스템에 명시된다.

### 이름 규칙

- 이벤트 — `<동작>_<대상>` : `Play_XP_Pickup`, `Stop_Music`
- RTPC — `<명사>_<속성>` : `Enemy_Count`, `Player_Health`. 해석이 포함된 이름은 사용하지 않는다
  (`Music_Intensity`는 "강도"라는 해석이 이름에 포함된 사례다. 게임 코드가 아는 것은 적 수뿐이다)
- State — `<영역>_<상태>` : `Game_State` 그룹 아래 상태명은 단수형

---

## 3. 자산 흐름

```
Audio_src/*.wav                  소스 파일. 커밋 대상
      │  Wwise Import
      ▼
Swarm_WwiseProject/Originals/    Wwise가 관리하는 원본 사본. 커밋 대상
      │  변환 (Vorbis / PCM)
      ▼
Swarm_WwiseProject/.cache/*.wem  중간 산출물. 커밋 제외
      │  SoundBank Generate
      ▼
GeneratedSoundBanks/Windows/     Main.bnk + Init.bnk. 커밋 제외
      │  복사 (§4의 자동화 대상)
      ▼
Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/   런타임이 읽는 곳
```

> **주의 — 생성 위치와 런타임 참조 위치가 다르다**
>
> Wwise는 `Swarm_WwiseProject/GeneratedSoundBanks/`에 만들고, Unity 런타임은
> `Assets/StreamingAssets/Audio/GeneratedSoundBanks/<플랫폼>/`에서 읽는다.
> 그 사이를 잇는 복사가 `WwiseSettings.xml`의 `CopySoundBanksAsPreBuildStep: true`이며,
> 이 설정은 빌드 시점에만 동작한다. 에디터 Play 모드에는 적용되지 않는다.
>
> 이 문제로 `Bank Load Failed Name: 1355168291`(= `Init`의 FNV 해시) 오류가 매 프레임
> 발생한 사례가 있었다. 뱅크 자체에는 문제가 없었고 복사 누락이 원인이었다.
> §4의 에디터 메뉴는 이 문제를 방지하기 위한 장치다.

> **주의 — 사용자 뱅크는 자동으로 로드되지 않는다**
>
> `AkInitializer`가 자동으로 로드하는 것은 Init 뱅크뿐이다. 이벤트·구조·미디어가 포함된 뱅크는
> 명시적으로 `LoadBank`를 호출해야 한다. 이를 누락하면 엔진은 정상적으로 초기화되고 RTPC도
> 정상 전달되지만 이벤트만 전부 `Event ID not found`로 실패한다. 뱅크 파일의 위치·복사·ID가
> 모두 정상이므로 로그만으로는 원인을 파악하기 어렵다.
>
> 이 처리는 `AudioDirector.EnsureBanks()`가 담당한다. 씬에 `AkBank` 컴포넌트를 두는 방법도
> 있으나, 그 경우 뱅크 로드 설정이 씬에 분산되어 코드만으로 파악할 수 없게 된다. §5의 마지막
> 검사가 "이벤트가 포함된 뱅크를 코드가 로드하는가"를 대조하는 이유가 여기에 있다.

### BGM은 한 단계가 더 있다

```
Music_Layer_{Base,Mid,High}.wav
      │  render_demo.py --game   ← 레이어를 합쳐 한 파일로 굽는다
      ▼
Audio_src/Music_BGM_Loop.wav     이것이 게임에 들어가는 BGM
```

`--game` 옵션으로 실행하면 체력 로우패스를 렌더링에 포함하지 않는다. 게임에서는 Wwise의
`Player_Health` RTPC가 이를 실시간으로 적용하므로, 파일에 미리 적용하면 체력이 가득한
상태에서도 루프마다 필터가 걸리고 체력이 낮을 때는 이중으로 적용된다. `--game` 없이 실행하면
감상용 데모(`Claude outputs/`)가 생성된다.

### 음원 교체 절차

`Audio_src/`에 같은 파일명으로 덮어쓴 뒤 Wwise에서 해당 Sound를 Reimport하고 Rebuild한다.
Wwise 구조·이벤트·RTPC 배선은 그대로 유지된다.

`gen_placeholder.py`는 삭제하지 않는다. 각 플레이스홀더가 대체할 음원의 성격이 스크립트에
남아 있어, 실제 음원 제작 시 사양서 역할을 한다.

---

## 4. 자동화

### Unity 에디터: `Swarm → Audio → Rebuild` (`Ctrl+Shift+A`)

`Assets/_Project/Scripts/Editor/AudioPipeline.cs`. 메뉴 실행 시 아래 작업을 순서대로 수행한다.

1. `GeneratedSoundBanks/` → `StreamingAssets/` 복사 (플랫폼 폴더별로 수행하며, 이전 산출물은 먼저 삭제한다)
2. 뱅크 메타데이터 → `WwiseIds.generated.cs` 재생성
3. `AssetDatabase.Refresh()`

경로는 하드코딩하지 않고 `Assets/WwiseSettings.xml`의 `RootOutputPath` /
`WwiseStreamingAssetsPath`를 읽는다. 설정을 변경하면 메뉴 동작도 함께 반영된다.

SoundBank 생성 자체는 이 메뉴에서 수행하지 않는다. 생성은 Wwise Authoring 또는 WAAPI(MCP)의
역할이고, 이 메뉴는 생성된 결과물을 Unity에 반영하는 범위까지만 담당한다. 2025.1 통합에는
생성 메뉴가 제공되지 않아 책임 범위를 이와 같이 구분했다.

이 절차가 없으면 뱅크 복사 누락으로 인한 무음 문제가 반복적으로 발생한다.

### 빌드 시점

`IPreprocessBuildWithReport`(callbackOrder −100)로 같은 절차를 한 번 더 강제하고, 실패하면
`BuildFailedException`으로 빌드를 중단한다. 작업자가 절차를 누락해도 빌드 단계에서 다시 확인된다.

---

## 5. 검증 — `Audio_src/lint_audio.py`

파이프라인은 문제를 사람이 발견하기 전에 검출해야 한다. 아래 항목은 이 프로젝트에서 실제로
발생했거나, 발생 시 원인 파악이 어려운 유형의 실패다. 검사에 실패하면 종료 코드 1을 반환하므로
커밋 훅이나 CI에 그대로 연결할 수 있다.

| 검사 | 기준 | 왜 |
|---|---|---|
| 음악 레이어 길이 일치 | 3개 전부 동일 | 길이가 다르면 루프가 점차 어긋난다. 재현이 어려운 버그 |
| 루프 이음매 불연속 | < 0.005 | 루프마다 클릭음이 발생한다. 타악기 레이어는 어택에 가려지므로 경고로만 처리 |
| 채널 / 샘플레이트 / 비트뎁스 | 모노 / 48kHz / 16bit | 스테레오일 경우 Wwise의 공간 처리가 적용되지 않는다 |
| 픽업 효과음 길이 | ≤ 0.3초 | 자석으로 수십 개가 한 프레임에 수집될 때 소리가 뭉친다 |
| `Audio_src` ↔ Wwise `Originals` | 해시 일치 | 소스를 수정하고 Reimport하지 않으면 이전 음원이 계속 재생된다 |
| `GeneratedSoundBanks` ↔ `StreamingAssets` | 해시 일치 | §3의 주의 사항. 복사를 누락하면 이전 뱅크가 로드된다 |
| `WwiseIds` ↔ 뱅크 | 이름·ID 일치 | 뱅크를 다시 생성한 뒤 상수 파일을 재생성하지 않은 불일치 |
| 이벤트가 든 뱅크를 코드가 로드하는가 | `Banks.<이름>` 참조 존재 | §3의 두 번째 주의 사항. 누락 시 이벤트가 오류 없이 전부 실패한다 |

뒤의 세 항목이 핵심이다. 서로 다른 위치에 있는 동일한 대상을 대조하는 검사로, 오류 없이 소리가
나지 않거나 이전 음원이 재생되는 유형의 실패를 겨냥한다. 이런 실패는 원인 파악에 가장 많은
시간이 소요된다.

## 6. 버전관리 경계

원칙: 재생성 가능한 산출물은 커밋하지 않고, 재생성 절차를 문서로 남긴다.

| 커밋함 | 제외함 | 제외 이유 |
|---|---|---|
| `Audio_src/*.wav`, `*.py` | `.cache/` | 변환 중간물 |
| `Swarm_WwiseProject/*.wwu` | `GeneratedSoundBanks/` | 뱅크는 산출물 |
| `Assets/Wwise/` C# + `.meta` | `StreamingAssets/` 뱅크 | 위와 동일 |
| | `Assets/Wwise/API/Runtime/Plugins/` | 743MB 네이티브 바이너리. Launcher가 재생성 |
| | `Assets/Wwise/Documentation/` | 59MB 오프라인 문서 |
| | `Tools/Wwise-MCP/` | 30MB 실행 파일 |

`Assets/Wwise/` 전체를 제외하지 않은 이유는 다음과 같다. 재통합 시 `.meta` GUID가 새로 생성되어
씬·프리팹에 붙은 `AkGameObj` 등의 참조가 전부 끊어진다. C#과 `.meta`는 남기고 네이티브
바이너리만 제외해서, 831MB → 약 30MB로 줄이면서 참조는 보존했다.

**재생성 절차**: Wwise Launcher → UNITY 탭 → *Integrate Wwise into Project*

---

## 7. 하지 않은 것과 그 이유

1인 개발 프로젝트의 규모를 고려해 아래 항목은 의도적으로 구현하지 않았다.

| 안 한 것 | 이유 |
|---|---|
| CI에서 뱅크 빌드 | Wwise 라이선스와 전용 머신이 필요하다. 1인 프로젝트에서는 비용 대비 효용이 낮다 |
| 다중 플랫폼 뱅크 | Windows 단일 타깃. 플랫폼이 늘면 Launcher에서 추가하고 §4에 platform 인자만 늘리면 된다 |
| SoundBank 분할 | 뱅크 하나로 충분한 규모(2.3MB). 분할 기준은 "씬 단위 + 상시 상주 분리"로 정해두되 지금은 적용하지 않는다 |
| Addressables 연동 | 에셋 규모가 이 복잡도를 정당화하지 못한다 |
| 오디오 담당자용 별도 브랜치 전략 | 단독 작업이므로 불필요 |
| **음악 레이어의 런타임 크로스페이드** | 아래 참고 |

### BGM을 구워서 쓰기로 한 것

인게임 BGM은 세 레이어를 실시간으로 섞지 않고, `render_demo.py --game`이 미리 합쳐 구운
64초 루프 한 파일(`Music_BGM_Loop.wav`)이다. 아르페지오가 추가되고 드럼이 들어왔다가 역순으로
빠지는 곡의 전개가 그 자체로 완결성이 있다고 판단해 고정된 형태로 사용한다.

**절충 사항**: `Enemy_Count` RTPC는 저작 구조(`Music_Bed`의 Blend Track 크로스페이드)에 그대로
남아 있으나, 현재 재생 경로가 `Music_Loop`이므로 소리에 영향을 주지 않는다. 게임 코드는 매
프레임 적 수를 보고하고 프로파일러에도 값이 표시되지만, 이를 수신하는 대상이 없다.

`Player_Health` → Lowpass는 유지된다. 이중 적용을 피하기 위해 렌더링한 파일에서는 로우패스를
제외했다. 한쪽을 미리 렌더링했다고 해서 다른 쪽도 함께 렌더링해야 하는 것은 아니다.

되돌리려면 `Play_Music` / `Stop_Music` 이벤트의 Target을 `Music_Bed`로 변경하면 된다.
게임 코드는 수정하지 않아도 되며, 앞서 정한 책임 경계가 이 지점에서 효과를 보인다.

이 표는 부록이 아니라 문서의 일부다. 구현하지 않은 항목과 그 이유를 함께 기록해 두면, 이후
규모가 커졌을 때 어떤 조건에서 해당 항목을 도입해야 하는지 판단할 근거가 된다.

---

## 8. 알려진 부채

| 항목 | 내용 |
|---|---|
| `render_demo.py`의 이중 소스 | Wwise의 크로스페이드·LPF 곡선을 수작업으로 복제해 두었다. Wwise 쪽만 수정하면 미리듣기 결과가 실제와 달라지므로, WAAPI로 읽어오도록 변경해야 한다 |
| `Game_State` 미배선 | 상태는 정상적으로 전달되지만 수신하는 대상이 없다 |
| 8초 루프 | 10분 플레이 기준 약 75회 반복된다. 이음매는 처리했으나 반복감은 남는다. 32초(16마디) 길이에 화성 진행을 더하는 것이 일반적이다 |
| `Enemy_Count` 최대 0.5초 지연 | `SweepActiveEnemies()` 주기에 따른 지연. RTPC 용도로는 문제가 없으나 정확한 값이 필요한 용도에는 부적합하다 |
| State 오버라이드는 자동화 불가 | Wwise-MCP의 `set_object`가 오디오 노드의 `@StateGroups` / `@States`를 노출하지 않아 `Invalid property`로 거부된다. 값 조정은 Wwise UI 작업으로 남는다 |
| BGM이 6MB PCM | Conversion Settings가 기본값이라 무압축 상태다. 음악은 Vorbis + 스트리밍 설정이 일반적이다 |

---

## 9. 현재 상태

**완료**

- Wwise 구조 — 버스, RTPC 5종, Blend Container 크로스페이드, 이벤트 4종, SoundBank
- 게임 코드 연결 — `AudioDirector` / `WwiseGameSync`, 픽업 4종, 상태 전환 4개 지점
- `WwiseIds.generated.cs` 자동 생성 — 이벤트 이름이 타입으로 고정되어 오타가 컴파일 오류로 검출된다
- `Swarm → Audio → Rebuild` 메뉴 + 빌드 전 훅
- `Audio_src/lint_audio.py` 검사 7종
- `Game_State` 배선 — `LevelUp`에서 Music −8dB / SFX −4dB
- 버전관리 경계, 플레이스홀더 음원과 생성·렌더 스크립트

**미완**

- §8의 부채 항목들 (BGM 무압축 6MB, `render_demo.py` 이중 소스, 8초 루프 소스, `Enemy_Count` 지연)

파이프라인은 한 사이클이 완성된 상태다. 소스 수정 → Wwise에서 뱅크 생성 → `Rebuild` 실행으로
게임에 반영 → `lint_audio.py`로 불일치 검출까지 이어진다.
