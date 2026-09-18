# Swarm 오디오 파이프라인

**Unity 6000.4.3f1 / Wwise SDK 2025.1.10.9233 / Unity Integration 2025.1.10.4304**

Swarm은 BGM과 픽업 효과음을 Wwise로 재생한다. 게임의 상태 전달과 음원·믹싱 설정을 분리하고,
SoundBank 복사와 ID 생성을 에디터 도구로 묶어 코드와 오디오 자산을 함께 관리한다.
Windows는 Wwise 오디오 경로를 사용하며, WebGL은 무음으로 배포한다.

이 문서는 런타임 구조, 고정 루프 BGM을 선택한 절충, 자산 반영 절차와 정적 검사의 범위를 설명한다.

프로젝트 개요: [README](../README.md) · [아트 파이프라인](./art-plan.md) · [WebGL 빌드](./webgl-build.md)

## 0. 설계 원칙

게임 코드는 픽업·체력·게임 상태를 전달하고, Wwise는 음원 선택과 버스의 볼륨·필터·피치 곡선을 관리한다.
예를 들어 레벨업 카드 선택 중의 감쇠량은 Wwise에서 조정하므로 C# 호출부를 바꿀 필요가 없다.
변경한 설정은 SoundBank를 다시 생성해 게임에 반영한다.

호출 시점과 빈도는 게임 코드가 관리한다. 픽업이 한꺼번에 수집될 때는 같은 종류의 효과음을
0.04초 간격으로 제한하고, 사망 연출에서는 음악 피치 값을 전달한다.
BGM의 레이어 전개는 오프라인 렌더링으로 고정하고, 체력·게임 상태에 따른 버스 제어를 별도로 둔다.

## 1. 런타임 구조

프로젝트의 게임 코드가 `AkUnitySoundEngine`을 호출하는 지점을
[AudioDirector](../Assets/_Project/Scripts/Audio/AudioDirector.cs)로 모았다.
픽업·설정·게임 진행 코드는 이 경계를 통해 Wwise에 요청을 전달한다.

```text
픽업 ──────────── SoundEffects ─────┐
볼륨 설정 ─────── SoundEffects ─────┤
             └── BackgroundMusic ──┤
사망 연출 ─────── BackgroundMusic ──┤
체력·적 수 ────── WwiseGameSync ────┼─ AudioDirector ─ AkUnitySoundEngine
게임 상태 전환 ────────────────────┘
```

| 구성 요소 | 역할 |
|---|---|
| `AudioDirector` | 이벤트·RTPC·State 전달, 영속 호스트 등록, `Main` 뱅크 로드, 초기화 전 요청 보관 |
| [BackgroundMusic](../Assets/_Project/Scripts/Audio/BackgroundMusic.cs) | 씬 로드 전 BGM 시작 요청, PlayerPrefs 볼륨 보관, 사망 연출의 피치 전달 |
| [SoundEffects](../Assets/_Project/Scripts/Audio/SoundEffects.cs) | 픽업 두 종류의 이벤트 선택, 볼륨 보관, 종류별 재호출 간격 제한. 볼륨이 0이면 이벤트 생략 |
| [WwiseGameSync](../Assets/_Project/Scripts/Audio/WwiseGameSync.cs) | 체력 변경 구독, 0.25초 주기의 적 수 조회와 변경값 전달, 매 프레임 지연 요청 처리 |
| [WwiseIds.generated](../Assets/_Project/Scripts/Audio/WwiseIds.generated.cs) | 생성된 뱅크 메타데이터에서 가져온 이벤트·RTPC·State ID와 뱅크 이름 |

`GameSettings`는 BGM·효과음 볼륨을 각각 `BackgroundMusic`과 `SoundEffects`에 위임한다.
`ResultSequence`는 사망 연출에서 피치 0.6을 전달하고 이후 1로 복원한다.

### 초기화와 요청 수명

[Title 씬](../Assets/_Project/Scenes/Title.unity)의 `WwiseGlobal`에 `AkInitializer`가,
`Main Camera`에 기본 리스너인 `AkAudioListener`와 `AkGameObj`가 연결되어 있다.
[초기화 설정](../Assets/Wwise/ScriptableObjects/AkWwiseInitializationSettings.asset)을 통해
엔진과 `Init` 뱅크를 준비하고, `AudioDirector.EnsureBanks()`가 첫 이벤트 전에 `Main`을 동기 로드한다.
로드 실패는 한 번 기록하며 같은 실행 상태에서 반복 시도하지 않는다.

오디오 초기화와 게임 초기화의 순서 차이는 지연 요청으로 처리한다.
음악 시작 요청과 RTPC·State의 최신값을 보관했다가 `WwiseGameSync.Update()`에서 다시 전달한다.
픽업처럼 순간적인 효과음은 재생 시점이 지나면 버린다.

BGM 호스트와 `WwiseGameSync`는 씬 전환 후에도 유지된다. BGM은 타이틀·게임·결과 화면 사이에서
계속 이어지도록 구성했으며, 씬 이탈 시 `StopMusic()`을 호출하는 경로는 없다.

## 2. 게임과 Wwise의 계약

### Events

[이벤트 Work Unit](../Swarm_WwiseProject/Events/Default%20Work%20Unit.wwu)에 정의한 이벤트는 4종이다.

| 이벤트 | 대상 | 호출 경로 |
|---|---|---|
| `Play_Music` | `Music_Loop` | `BackgroundMusic.Install()` → `AudioDirector.PlayMusic()` |
| `Stop_Music` | `Music_Loop` | `AudioDirector.StopMusic()`에 정의. 현재 게임 흐름에서는 미사용 |
| `Play_XP_Pickup` | `SFX/XP_Pickup` | `ExperiencePickup.TryCollect()` → `SoundEffects.Play()` |
| `Play_Object_Pickup` | `SFX/Object_Pickup` | 골드·자석·회복 픽업 → `SoundEffects.Play()` |

Windows `Main` 뱅크의 메타데이터에는 이 이벤트들과 BGM·픽업 음원 3종이 포함된다.
음악 이벤트는 `Music_BGM_Loop.wav`를 사용한다.

### Game Parameters (RTPC)

게임에서 보낸 값을 [버스](../Swarm_WwiseProject/Busses/Default%20Work%20Unit.wwu)와
[컨테이너](../Swarm_WwiseProject/Containers/Default%20Work%20Unit.wwu)의 곡선에 연결한다.

| 이름 | 게임 입력 | Wwise 연결 |
|---|---|---|
| `Enemy_Count` | 스포너의 관리 목록 수 | `Music_Bed`의 Mid 8–30, High 35–65 크로스페이드. 현재 재생 경로에서는 사용하지 않음(§7) |
| `Player_Health` | 현재/최대 체력의 백분율, 0–100 | Music·SFX 버스 Lowpass. 체력 50 이상에서 곡선값 0 |
| `Volume_Music` | BGM 볼륨 × 100, 0–100 | Music 버스 Volume. 곡선 양 끝은 −96dB / 0dB |
| `Volume_SFX` | 효과음 볼륨 × 100, 0–100 | SFX·UI 버스 Volume. 곡선 양 끝은 −96dB / 0dB |
| `Music_Pitch` | 피치 입력, 0.1–3 | Music 버스 Pitch. 0.1/0.5/1/2/3을 −2400/−1200/0/1200/1902센트에 대응 |

볼륨과 피치 입력은 Wwise 곡선을 통해 변환된다. `Music_Pitch`의 입력값은 전 구간에서
정확한 재생 속도 배율을 나타내는 값이 아니라 이 곡선의 제어값이다.
`Enemy_Count`는 코드에서 상한을 제한하지 않으며, 저작된 크로스페이드 곡선은 100까지다.

### States

`Game_State` 그룹의 상태에 따라 버스 설정을 적용한다.

| 상태 | 전달 지점 | Music·SFX 버스 설정 |
|---|---|---|
| `Title` | `TitleManager.Start()` | 별도 볼륨 오프셋 없음 |
| `Playing` | `GameManager.Start()`, 카드 선택 완료 | 별도 볼륨 오프셋 없음 |
| `LevelUp` | `LevelUpManager.HandleLevelUp()` | Music −8dB, SFX −4dB |
| `Dead` | `GameManager.ShowResult()` | 별도 볼륨 오프셋 없음. 사망·클리어가 공통 사용 |

Wwise 그룹에는 기본 상태 `None`도 있으며, 게임의 `GameAudioState` enum은 나머지 4종을 사용한다.

### ID 생성으로 이름 불일치 줄이기

[WwiseIdGenerator](../Assets/_Project/Scripts/Editor/WwiseIdGenerator.cs)는 복사된 플랫폼 폴더의
JSON에서 ID를 읽어 C# 상수를 만든다. 현재 이벤트는 `Main.json`, RTPC·State는 `Init.json`에서 가져온다.

호출부가 문자열 대신 생성 상수를 사용하므로 이름 오타를 컴파일 단계에서 발견할 수 있다.
이벤트 이름을 바꾼 뒤 뱅크와 상수를 재생성하면 이전 이름을 참조하는 호출부도 컴파일 오류로 드러난다.
상수는 `uint` 형식이며, 뱅크 복사 여부와 메타데이터 일치는 별도의 검사기로 확인한다(§5).

## 3. 자산 흐름

```text
Audio_src/*.wav
  → Swarm_WwiseProject/Originals/SFX/SFX/*.wav
  → Wwise 변환 캐시(.cache/) 및 SoundBank 생성
  → Swarm_WwiseProject/GeneratedSoundBanks/Windows/
  → Swarm → Audio → Rebuild
  → Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/
```

[WwiseSettings.xml](../Assets/WwiseSettings.xml)은 Wwise 프로젝트와 출력·복사 경로를 관리한다.

| 설정 | 값 |
|---|---|
| `WwiseProjectPath` | `../Swarm_WwiseProject/Swarm_WwiseProject.wproj` (Assets 기준) |
| `RootOutputPath` | `../Swarm_WwiseProject/GeneratedSoundBanks/` (Assets 기준) |
| `WwiseStreamingAssetsPath` | `Audio/GeneratedSoundBanks` |
| 빌드 전 복사 / 생성 | `true` / `false` |

에디터의 [AkBasePathGetter](../Assets/Wwise/API/Runtime/Handwritten/Common/AkBasePathGetter.cs)는
Wwise 프로젝트의 플랫폼 출력 경로를 우선 조회한다. Windows 플레이어는 기본 설정에서
StreamingAssets 아래의 뱅크를 읽는다. 이 경로 차이 때문에 에디터 재생용 산출물과 빌드에 포함할
사본을 함께 관리한다.

### 음원 구성

| 음원 | 길이 | 용도 |
|---|---:|---|
| `Music_BGM_Loop.wav` | 64초 | 현재 BGM |
| `Music_Layer_Base/Mid/High.wav` | 각각 8초 | BGM 렌더링 소스 |
| `SFX_XP_Pickup.wav` | 0.14초 | 경험치 획득 |
| `SFX_Object_Pickup.wav` | 0.30초 | 골드·자석·회복 획득 |

소스 규격은 모노·48kHz·16bit다.
[gen_placeholder.py](../Audio_src/gen_placeholder.py)는 레이어와 픽업 음원의 합성 스크립트이며,
[render_demo.py](../Audio_src/render_demo.py)는 레이어를 정해진 전개에 따라 섞는 렌더러다.
`--game` 옵션은 게임용 BGM을 만들고, 옵션이 없으면 근사 로우패스를 포함한 감상용 파일을 출력한다.
게임용 파일에는 체력 필터를 굽지 않아 Wwise의 실시간 제어와 중복되지 않도록 했다.

음원 교체는 다음 순서로 진행한다.

1. `Audio_src`의 소스와 Wwise가 참조하는 Originals 사본을 함께 갱신한다.
2. 기존 Sound 객체와 이름을 유지해 임포트한 뒤 SoundBank를 생성한다.
3. Unity에서 `Swarm → Audio → Rebuild`를 실행한다.
4. 정적 검사 후 게임에서 재생·루프·믹싱을 확인한다.

## 4. 에디터·빌드 자동화

### `Swarm → Audio → Rebuild` (`Ctrl+Shift+A`)

[AudioPipeline](../Assets/_Project/Scripts/Editor/AudioPipeline.cs)은 생성된 SoundBank를 Unity에 반영한다.

1. 설정에서 생성·복사 경로를 읽는다.
2. 루트 JSON과 플랫폼 폴더 직하위 `.bnk`·`.json`을 복사한다. 목적지의 기존 뱅크·JSON과 해당 `.meta`는 먼저 정리한다.
3. 복사된 JSON으로 `WwiseIds.generated.cs`를 재생성한다. 내용이 같으면 파일을 유지한다.
4. `AssetDatabase.Refresh()`로 에디터에 반영한다.

SoundBank 생성은 Wwise Authoring이나 통합의 `Generate SoundBanks` 기능에서 수행한다.
자체 메뉴는 생성 이후의 복사와 ID 갱신을 한 번에 처리하는 역할이다.

### 빌드 타깃별 처리

WebGL 외 타깃에서는 `callbackOrder = -100`인 빌드 훅이 복사·ID 생성을 실행한다.
원본 폴더 부재, 복사 예외, 복사 파일 0건이면 빌드를 중단한다.
빌드 훅은 산출물 준비를 담당하고, 파일·ID 대조는 §5의 검사기로 보완한다.

WebGL 플레이어는 `UNITY_WEBGL && !UNITY_EDITOR` 조건에서 무음 백엔드를 사용한다.
Wwise 런타임 어셈블리를 빌드에서 제외하고, 통합의 뱅크 전처리도 건너뛴다.
불필요한 Windows 뱅크가 웹 빌드에 포함되지 않도록 뱅크 폴더와 `.meta`를
`Library/SwarmWebGLAudioStash`로 옮긴 뒤 빌드 후 복원한다.
중단된 빌드가 남긴 폴더는 에디터 코드 로드 후 복원하도록 구성했다.

## 5. 정적 검사

[lint_audio.py](../Audio_src/lint_audio.py)는 소스·Wwise Originals·Unity 사본 사이의 불일치를 검사한다.
생성 위치가 다른 자산을 해시와 ID로 대조해, 소스를 바꾼 뒤 이전 음원이나 뱅크를 사용하는 실수를 찾는다.

| 검사군 | 기준 |
|---|---|
| 레이어 길이 | Base·Mid·High의 샘플 프레임 수 일치 |
| 루프 이음매 | BGM·레이어의 마지막/첫 샘플 차이 ≤ 0.005. High 초과는 경고, 나머지는 실패 |
| 포맷 | 모노 / 48kHz / 16bit |
| 픽업 길이 | ≤ 0.30초 |
| 소스 ↔ Originals | BGM·픽업 두 종류의 SHA-256 일치 |
| 생성 폴더 ↔ StreamingAssets | `.bnk`·`.json` 사본의 존재·해시 일치 |
| 코드 ↔ 뱅크 메타데이터 | 선언된 ID 대조, 이벤트 보유 뱅크의 코드 참조 확인, 미사용 이벤트명 경고 |

```shell
python -X utf8 Audio_src/lint_audio.py
```

실패가 있으면 종료 코드 1을 반환한다. 검사는 파일과 코드의 정적 일치를 대상으로 하며,
실제 뱅크 로드, 버스 라우팅, 필터·덕킹과 루프의 청감은 게임 실행으로 별도 확인한다.

2026-09-18 로컬 검사에서는 7개 검사군이 통과했고, `Music_Layer_High.wav`의 이음매 차이
0.1035가 경고로 남았다. 이는 원본 레이어의 샘플 불연속에 대한 결과이며 청취 평가는 포함하지 않는다.

## 6. 버전관리와 환경 구성

원본과 저작 설정을 저장하고, 변환 캐시와 SoundBank는 재생성하는 방식으로 관리한다.
정책은 [.gitignore](../.gitignore)와 [.gitattributes](../.gitattributes)에 정의되어 있다.

| 저장소에 포함 | 생성·설치 대상 |
|---|---|
| `Audio_src`의 WAV·Python 스크립트 | Wwise 변환 캐시, 사용자 설정 |
| Wwise `.wproj`·`.wwu`·Originals WAV | 양쪽 `GeneratedSoundBanks/`의 뱅크·메타데이터 |
| Wwise 통합 C#·추적 중인 `.meta`·초기화 설정 | API 네이티브 플러그인, 오프라인 문서 |
| `WwiseIds.generated.cs`, 에디터 도구 | 로컬 Wwise-MCP 실행 파일, 감상용 렌더 출력 |

WAV는 Git LFS로 관리한다. SoundBank는 저장소에서 제외하므로 새 환경에서는 LFS 원본과
해당 버전의 네이티브 플러그인을 준비하고, SoundBank 생성 → `Rebuild` → 정적 검사 순서로 구성한다.
통합을 재설치할 때는 기존 `.meta`와 WebGL용 로컬 패치의 변경 여부도 확인한다.

## 7. BGM 구성의 절충

현재 BGM은 세 레이어를 미리 합친 64초 루프다. 곡의 전개를 고정하는 대신,
체력 Lowpass·설정 볼륨·사망 피치·레벨업 감쇠를 버스에서 제어하도록 구성했다.

적 수 기반 크로스페이드를 설정한 `Music_Bed`는 저작 프로젝트에 남아 있다.
하지만 `Play_Music`의 대상은 `Music_Loop`이며, 현재 Main 뱅크에는 개별 레이어가 포함되지 않는다.
따라서 `Enemy_Count`를 전달하는 코드가 있어도 현재 음악 전개는 적 수에 반응하지 않는다.

적 수 기반 음악으로 확장할 때는 같은 이벤트 이름을 유지하면서 대상을 `Music_Bed`로 바꾸고,
레이어가 포함된 뱅크를 생성·반영할 수 있다. 게임의 이벤트 호출부를 유지한 채 저작 구성을 바꿀 수 있는 지점이다.

## 8. 현재 제약과 확장 시 고려사항

사용자 뱅크는 `Main` 하나이며, Windows 생성본은 약 6.19MB다.
현재 변환 설정은 PCM이고 미디어는 메모리에 상주한다. 스트리밍이나 뱅크 분할을 도입할 때는
메모리·로드 비용과 함께 자산 복사 범위도 조정해야 한다.

| 항목 | 현재 제약 |
|---|---|
| 복사 도구 | 플랫폼 폴더 직하위 `.bnk`·`.json` 대상. 외부 `.wem`·언어 하위 폴더는 추가 대응 필요 |
| 생성 실패 처리 | ID 생성기의 반환값이 실패와 변경 없음을 구별하지 않아 빌드 훅만으로 메타데이터 오류를 모두 차단하지 못함 |
| 검사 범위 | State 그룹 ID, 일부 파일 누락, 목적지에만 남은 파일은 검사 대상에서 빠짐. 뱅크 코드 참조 검사는 문자열 기반 |
| 렌더링 곡선 | Wwise와 Python에 값을 각각 보관하므로 저작 변경 시 렌더 스크립트도 동기화 필요 |
| 적 수 보고 | 스포너 관리 목록만 포함. 0.5초 scaled-time 정리와 0.25초 unscaled-time 폴링으로 지연 발생 |
| 씬 진입 | 초기화·리스너 컴포넌트는 Title 씬에 배치. 다른 씬의 직접 실행 경로는 별도 검증 필요 |
| 체력 상태 | 씬 전환 시 체력 RTPC를 초기화하지 않으므로 플레이어가 없는 씬에서 이전 값이 유지될 가능성 있음 |

Mac 플랫폼 설정은 저작 프로젝트에 포함되어 있지만, 이 문서의 뱅크 구성과 검사 결과는 Windows 기준이다.
