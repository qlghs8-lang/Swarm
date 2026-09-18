# Swarm 오디오 파이프라인

**Unity 6000.4.3f1 / Wwise SDK 2025.1.10.9233 / Unity Integration 2025.1.10.4304**

게임의 오디오 호출, Wwise 저작 구조, SoundBank를 Unity에 반영하는 절차를 정리한다.
2026-09-18 기준 코드·씬의 직렬화 참조·Wwise 프로젝트·로컬 생성 파일·Git 이력을 대조했다.
아래의 현재 동작은 정적 확인에 근거하며, 이번 검토에서 Unity 실행·빌드·오디오 청취는 하지 않았다.

프로젝트 개요: [README](../README.md) · [아트 파이프라인](./art-plan.md) · [WebGL 빌드](./webgl-build.md)

---

## 0. 설계 원칙

게임은 픽업·체력·게임 상태를 전달하고, Wwise는 연결된 음원과 버스의 볼륨·필터·피치를 처리한다.
다만 소리에 관한 판단을 전부 Wwise에 위임한 구조는 아니다. 게임 코드가 효과음 재호출 간격,
볼륨 0일 때의 이벤트 생략, BGM 시작 시점과 사망 연출의 피치 값을 결정한다.
또한 현재 BGM의 레이어 전개는 실시간 적 수가 아니라 오프라인 렌더 스크립트에 들어 있다.

## 1. 경계 — 코드와 Wwise의 역할

프로젝트 런타임 코드에서 `AkUnitySoundEngine`을 직접 호출하는 파일은
[AudioDirector.cs](../Assets/_Project/Scripts/Audio/AudioDirector.cs)다.
이는 게임 호출의 경계이며, Wwise 통합 코드·씬 컴포넌트·어셈블리 참조까지 없애 주는 경계는 아니다.
백엔드를 제거할 때 이 파일 하나만 수정하면 된다고 보장하지 않는다.

```text
픽업 ──────────── SoundEffects ─────┐
설정 ──────────── BackgroundMusic ──┤
사망 연출 ─────── BackgroundMusic ──┤
체력·적 수 ────── WwiseGameSync ────┼─ AudioDirector ─ AkUnitySoundEngine
게임 상태 전환 ────────────────────┘                  (Windows/Editor 경로)
```

| 파일 | 현재 역할과 사용 여부 |
|---|---|
| `AudioDirector` | 이벤트 4종, RTPC 5종, State 전달. 영속 호스트 등록 및 `Main` 뱅크 로드. 초기화 전의 음악 시작 요청과 RTPC·State 최신값을 보관한다 |
| [BackgroundMusic](../Assets/_Project/Scripts/Audio/BackgroundMusic.cs) | 씬 로드 전 음악 시작 요청, PlayerPrefs 볼륨 보관(기본 0.5), 피치 전달. BGM은 씬 전환 시 다시 시작하지 않는다 |
| [SoundEffects](../Assets/_Project/Scripts/Audio/SoundEffects.cs) | 픽업 두 종류의 이벤트 선택, PlayerPrefs 볼륨 보관(기본 0.8). 종류별 0.04초 간격을 `unscaledTime`으로 제한하며 볼륨 0이면 이벤트를 보내지 않는다 |
| [WwiseGameSync](../Assets/_Project/Scripts/Audio/WwiseGameSync.cs) | 런타임 생성 후 씬 간 유지. 매 프레임 `Flush()`, 0.25초마다 적 수 조회 후 변경 시 전달, 체력 변경 이벤트 구독. 참조가 없으면 0.5초 간격으로 탐색한다 |
| [WwiseIds.generated](../Assets/_Project/Scripts/Audio/WwiseIds.generated.cs) | 생성된 JSON에서 가져온 이벤트·RTPC·State ID와 뱅크 이름. `AudioDirector`가 사용한다 |
| [AudioPipeline](../Assets/_Project/Scripts/Editor/AudioPipeline.cs) / [WwiseIdGenerator](../Assets/_Project/Scripts/Editor/WwiseIdGenerator.cs) | 에디터 메뉴·빌드 훅에서 뱅크 복사와 상수 재생성을 수행한다(§4) |

`GameSettings`는 볼륨을 위 두 클래스에 위임한다. `ResultSequence`는 사망 연출에서 피치 0.6을
전달하고 이후 1로 복원한다. `SoundEffects.PlayPreview()`는 정의되어 있지만 현재 호출부는 없다.

### 초기화와 씬 연결

[Title.unity](../Assets/_Project/Scenes/Title.unity)의 `WwiseGlobal`에는 `AkInitializer`가,
`Main Camera`에는 기본 리스너인 `AkAudioListener`와 `AkGameObj`가 연결되어 있다.
스크립트 GUID와 [초기화 설정 에셋](../Assets/Wwise/ScriptableObjects/AkWwiseInitializationSettings.asset)의
참조가 일치한다. `Game`·`TestStage` 씬 및 프로젝트 프리팹에서는 같은 컴포넌트 참조를 찾지 못했다.
따라서 다른 씬을 직접 실행하는 경우의 초기화·리스너 상태까지 확인한 것으로 해석하면 안 된다.

통합 초기화 코드는 `Init` 뱅크를 로드한다. 사용자 뱅크 `Main`은
`AudioDirector.EnsureBanks()`가 첫 이벤트 전송 시 동기 로드한다. 실패하면 로그를 남기고
해당 정적 상태가 유지되는 동안 재시도하지 않는다. RTPC·State 전송은 `Main` 로드와 별도다.
초기화 전 픽업 이벤트는 큐에 저장하지 않으며, 음악 요청과 RTPC·State만 나중에 재시도한다.

## 2. 계약 — 이벤트, RTPC, State

### Events

[이벤트 Work Unit](../Swarm_WwiseProject/Events/Default%20Work%20Unit.wwu)과 로컬 Windows `Main.json`을 대조했다.

| 이벤트 | 저작 대상 | 현재 호출 경로 |
|---|---|---|
| `Play_Music` | `Music_Loop` | `BackgroundMusic.Install()` → `AudioDirector.PlayMusic()`. 초기화가 늦으면 `Flush()`에서 재시도 |
| `Stop_Music` | `Music_Loop` | `AudioDirector.StopMusic()`에 구현되어 있으나 호출부 없음. 씬 이탈 시 자동 호출되지 않음 |
| `Play_XP_Pickup` | `SFX/XP_Pickup` | `ExperiencePickup.TryCollect()` → `SoundEffects.Play()` |
| `Play_Object_Pickup` | `SFX/Object_Pickup` | `GoldPickup`·`MagnetPickup`·`HealthPackPickup` → `SoundEffects.Play()` |

로컬 `Main.json`에는 위 이벤트 4종과 BGM·픽업 음원 3종이 포함되어 있다.
`Play_Music`의 미디어 참조는 `Music_BGM_Loop.wav`이며, `Music_Bed`의 개별 레이어는 포함되지 않는다.

### Game Parameters (RTPC)

[버스 Work Unit](../Swarm_WwiseProject/Busses/Default%20Work%20Unit.wwu)과
[컨테이너 Work Unit](../Swarm_WwiseProject/Containers/Default%20Work%20Unit.wwu)의 연결은 다음과 같다.

| 이름 | 코드에서 전달하는 값 | 저작 연결 |
|---|---|---|
| `Enemy_Count` | `EnemySpawner.ActiveEnemyCount` 정수 그대로. 코드에 100 상한 클램프 없음 | `Music_Bed`의 Mid 8–30, High 35–65 크로스페이드. 곡선 끝은 100이며 현재 음악 이벤트의 재생 대상은 아님 |
| `Player_Health` | 현재/최대 체력의 백분율, 0–100 클램프 | Music·SFX 버스 Lowpass. 체력 50 이상은 두 곡선 모두 0 |
| `Volume_Music` | `BackgroundMusic.Volume` × 100, 0–100 | Music 버스 Volume. 곡선 양 끝은 −96dB / 0dB |
| `Volume_SFX` | `SoundEffects.Volume` × 100, 0–100 | SFX·UI 버스 Volume. 곡선 양 끝은 −96dB / 0dB |
| `Music_Pitch` | `BackgroundMusic.SetPitch()` 입력, 0.1–3 클램프 | Music 버스 Pitch. 입력 0.1/0.5/1/2/3을 −2400/−1200/0/1200/1902센트에 대응 |

`Music_Pitch`는 위 곡선으로 변환되므로 입력값이 전 구간에서 정확한 재생 속도 배율이라고
단정하지 않는다. 볼륨 입력도 dB와 선형 대응하지 않는다.

`Music_Loop`은 Music 버스를 참조하고, 픽업의 부모 `SFX` 컨테이너는 SFX 버스를 참조한다.
픽업 Sound 자체에는 Main Audio Bus 참조도 직렬화되어 있으므로, 개별 참조만으로 유효 라우팅을
단정하지 않는다. 버스의 RTPC·State 설정 존재는 확인했지만 실제 상속 결과와 청감은 재생 검증 대상이다.

### States

`Game_State`에는 `None`·`Title`·`Playing`·`LevelUp`·`Dead`가 있다.
게임의 `GameAudioState` enum은 `None`을 제외한 4종을 사용한다.

| 상태 | 전달 지점 | 저작 설정 |
|---|---|---|
| `Title` | `TitleManager.Start()` | Music·SFX의 별도 볼륨 오프셋 없음 |
| `Playing` | `GameManager.Start()`, `LevelUpManager.Select()` | 동일 |
| `LevelUp` | `LevelUpManager.HandleLevelUp()` | Music −8dB, SFX −4dB |
| `Dead` | `GameManager.ShowResult()` | 별도 볼륨 오프셋 없음. 사망과 클리어 모두 이 경로를 사용 |

따라서 `Game_State`는 미배선 상태가 아니다. 값 조정은 Wwise 저작 설정에서 하고,
게임에 반영하려면 뱅크 생성·복사가 필요하다. 이번 검토는 감쇠값의 저장 여부까지 확인했다.

### 생성 상수가 보장하는 범위

`WwiseIdGenerator`는 복사된 플랫폼 폴더 아래 JSON을 재귀 탐색하며 `*Info.json`은 건너뛴다.
현재 이벤트는 `Main.json`, RTPC·State는 `Init.json`에서 가져오므로 `Main.json`만 읽는 생성기가 아니다.
예를 들어 실제 상수는 `WwiseIds.Events.Play_XP_Pickup = 1598016745u`,
`WwiseIds.Rtpc.Enemy_Count = 799803063u`다.

존재하지 않는 상수명 참조는 컴파일 오류가 된다. 이벤트를 변경하고 뱅크와 상수를 모두
재생성하면 이전 이름을 쓰는 호출부를 찾을 수 있다. 다만 상수는 `uint`이므로 이벤트와 RTPC의
종류를 타입으로 구별하지 않으며, 오래된 뱅크·잘못된 대상·로드 성공·실제 재생을 보장하지 않는다.

이름은 현재 `Play_`/`Stop_` 이벤트, `Enemy_Count`와 같은 RTPC, `Game_State` 그룹과 상태명으로 관리한다.

## 3. 자산 흐름과 뱅크 경로

```text
Audio_src/*.wav
  → Swarm_WwiseProject/Originals/SFX/SFX/*.wav
  → Wwise 변환 캐시(.cache/) 및 SoundBank 생성
  → Swarm_WwiseProject/GeneratedSoundBanks/Windows/
  → Swarm → Audio → Rebuild
  → Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/
```

[WwiseSettings.xml](../Assets/WwiseSettings.xml)은 다음 값을 사용한다.

| 설정 | 값 |
|---|---|
| `WwiseProjectPath` | `../Swarm_WwiseProject/Swarm_WwiseProject.wproj` (Assets 기준) |
| `RootOutputPath` | `../Swarm_WwiseProject/GeneratedSoundBanks/` (Assets 기준) |
| `WwiseStreamingAssetsPath` | `Audio/GeneratedSoundBanks` |
| `CopySoundBanksAsPreBuildStep` | `true` |
| `GenerateSoundBanksAsPreBuildStep` | `false` |

초기화 에셋의 기본 뱅크 경로도 `Audio/GeneratedSoundBanks`다.
**에디터와 플레이어의 탐색 경로는 구분해야 한다.** 현재 통합의
[AkBasePathGetter](../Assets/Wwise/API/Runtime/Handwritten/Common/AkBasePathGetter.cs)는
에디터에서 Wwise 프로젝트의 플랫폼 출력 경로를 먼저 조회하고, 이를 얻지 못하면 대체 경로를 사용한다.
Windows 플레이어의 기본 경로는 StreamingAssets 아래다.
따라서 에디터 Play가 항상 StreamingAssets 사본만 읽는다거나, 그 사본이 없으면 반드시 실패한다고 쓰지 않는다.

### 현재 로컬 산출물

| Windows 뱅크 | 파일 크기 | 확인 범위 |
|---|---:|---|
| `Init.bnk` | 1,399바이트 | 생성 폴더와 StreamingAssets 사본 존재, SHA-256 일치 |
| `Main.bnk` | 6,187,071바이트 | 동일. 함께 생성된 JSON에는 이벤트 4종과 메모리 상주 미디어 3종이 기록됨 |

이는 로컬 파일 확인이며 **Git에 뱅크가 포함되어 있다는 뜻은 아니다**(§6).
프로젝트에는 Mac·Windows 플랫폼 설정이 있지만 이번에 확인한 `.bnk`는 Windows용뿐이다.

### BGM과 음원 교체

[render_demo.py](../Audio_src/render_demo.py)의 `--game` 경로는 8초짜리 Base·Mid·High 레이어를
정해진 전개에 따라 섞어 64초 `Music_BGM_Loop.wav`를 만든다. `--game`에서는 체력 로우패스를
굽지 않고, 옵션이 없으면 근사 필터를 넣은 감상용 파일을 `Claude outputs/`에 출력한다.
이 렌더러는 Wwise의 DSP를 그대로 실행하는 도구가 아니다.

현재 WAV는 모두 모노·48kHz·16bit이며, BGM 64초, 레이어 각 8초, XP 픽업 0.14초,
일반 픽업 0.30초다. 음원과 Originals 사본이 있으므로 음원 미투입 상태로 분류하지 않는다.
[gen_placeholder.py](../Audio_src/gen_placeholder.py)는 레이어·픽업 합성 소스를 남겨 둔 도구다.

음원을 교체할 때는 `Audio_src`뿐 아니라 **Wwise가 참조하는 Originals 사본도 갱신**해야 한다.
같은 이름과 객체를 유지해 임포트/교체한 뒤 SoundBank를 생성하고 `Rebuild`, 검사기를 실행한다.
`Audio_src`만 덮어쓰고 기존 Originals를 다시 읽는 것으로는 새 소스가 전달되지 않는다.
이번 문서 검토에서는 음원·뱅크를 재생성하지 않았다.

## 4. 자동화

### `Swarm → Audio → Rebuild` (`Ctrl+Shift+A`)

`AudioPipeline` 메뉴는 다음 작업을 한다.

1. 설정에서 생성·복사 경로를 읽는다. 읽을 수 없거나 비어 있으면 코드의 기본 경로를 쓴다.
2. 루트 JSON과 각 플랫폼 폴더 바로 아래 `.bnk`·`.json`을 복사한다. 해당 목적지의 기존 뱅크·JSON과 그 `.meta`를 먼저 삭제한다.
3. 복사된 JSON으로 `WwiseIds.generated.cs`를 재생성한다. 내용이 같으면 쓰지 않는다.
4. 메뉴 경로에서 `AssetDatabase.Refresh()`를 호출한다.

SoundBank 생성 자체는 이 메뉴의 역할이 아니다. Wwise Authoring 등에서 먼저 생성해야 한다.
통합의 `AkWwiseBrowser.cs`에는 `Generate SoundBanks` UI가 있으므로, 이 버전의 통합에
생성 기능이 없다는 설명은 맞지 않는다.

### 빌드 훅과 한계

`callbackOrder = -100`인 빌드 훅은 WebGL 외 타깃에서 복사·상수 생성을 실행한다.
원본 폴더 부재, 복사 예외, 복사 파일 0건이면 `BuildFailedException`으로 중단한다.
그러나 복사 건수에는 JSON도 포함되고, 생성기는 실패와 변경 없음 모두 `false`를 반환하며
호출부는 이를 구별하지 않는다. 따라서 **필수 뱅크·JSON의 모든 오류를 빌드 전에 차단하는 검증기는 아니다.**

복사는 플랫폼 폴더 직하위 파일만 다루며 외부 `.wem`이나 언어 하위 폴더는 복사하지 않는다.
현재 `Main.json`의 미디어는 모두 `Streaming: false`, `Location: Memory`다.
스트리밍·다국어 구성을 추가한다면 복사 절차도 함께 검토해야 한다.

WebGL에서는 반대로 뱅크 폴더와 `.meta`를 `Library/SwarmWebGLAudioStash`로 잠시 옮기고,
빌드 후 복원한다. 중단 시 남은 폴더는 에디터 코드 로드 후 복원하도록 구현되어 있다.
`AudioDirector`는 `UNITY_WEBGL && !UNITY_EDITOR`에서 무음 백엔드를 사용한다.
관련 Wwise 런타임 어셈블리의 WebGL 제외와 통합 빌드 전처리 우회도 적용되어 있다.
이는 현재 WebGL 무음 배포 경로의 코드 근거이며, 데모를 다시 실행해 확인한 결과는 아니다.

## 5. 검증 — `Audio_src/lint_audio.py`

[검사기](../Audio_src/lint_audio.py)는 파일을 읽어 다음 7개 검사군을 수행한다.

| 검사군 | 구현된 기준 |
|---|---|
| 레이어 길이 | Base·Mid·High의 샘플 프레임 수 일치 |
| 루프 이음매 | BGM·레이어의 마지막/첫 샘플 차이 ≤ 0.005. High 초과는 경고, 나머지는 실패 |
| 포맷 | BGM·레이어·픽업의 모노 / 48kHz / 16bit 여부. 프로젝트의 소스 규격 검사 |
| 픽업 길이 | ≤ 0.30초 |
| 소스 ↔ Originals | BGM·픽업 두 종류의 SHA-256 비교. 레이어는 이 비교에서 제외 |
| 생성 폴더 ↔ StreamingAssets | 생성 폴더의 `.bnk`·`.json`에 대응하는 사본의 존재·해시 비교 |
| 코드 ↔ 뱅크 메타데이터 | 선언된 ID 대조, 이벤트 보유 뱅크의 `Banks.<이름>` 문자열 참조 확인, 미사용 이벤트명 경고 |

실패가 있으면 종료 코드 1을 반환한다. CI나 커밋 훅에 연결할 수 있지만 이 문서는 자동 실행을
보장하지 않는다. Windows 콘솔 인코딩 문제를 피하려면 `python -X utf8 Audio_src/lint_audio.py`로 실행한다.

**이번 실행 결과:** 종료 코드 0, 7개 검사군 통과, 경고 1건.
`Music_Layer_High.wav`의 이음매 차이는 0.1035로 경고 기준을 넘었다.
타악기에 가려져 실제로 들리지 않는지는 청취하지 않았으므로 판단하지 않았다.

통과의 범위에는 제한이 있다.

- 모노 규격은 프로젝트 선택이다. 이를 스테레오의 공간 처리가 불가능하다는 일반 규칙으로 해석하지 않는다.
- 파일 누락 중 일부는 건너뛰거나 경고만 낸다. 생성 폴더가 비어 있는 경우나 목적지에만 남은 파일까지 모두 검출하지 않는다.
- `Group` 상수는 ID 대조에서 건너뛴다. `Banks.Main` 문자열이 있어도 실제 로드 실행·성공을 보장하지 않는다.
- 저작 파일과 뱅크 내부의 모든 설정을 비교하거나 실제 재생·라우팅·클릭음·덕킹을 검사하지 않는다.

## 6. 버전관리 경계

[.gitignore](../.gitignore), [.gitattributes](../.gitattributes), `git ls-files`를 기준으로 구분한다.

| Git 추적 | Git 제외 |
|---|---|
| `Audio_src/*.wav`·Python 스크립트 | Wwise `.cache/`, 사용자 설정·검증 캐시 |
| Wwise `.wproj`·하위 폴더 `.wwu`·Originals WAV | Wwise 프로젝트와 StreamingAssets 양쪽 `GeneratedSoundBanks/` |
| Wwise 통합 C#·추적 중인 `.meta`·초기화 설정 | `Assets/Wwise/API/Runtime/Plugins/`, 오프라인 Documentation |
| `WwiseIds.generated.cs`, 에디터 도구 | `Tools/Wwise-MCP/`, 통합 설치 ZIP, 감상용 렌더 출력 |

WAV는 Git LFS 대상이다. `git ls-files`에 `.bnk`는 없고 양쪽 뱅크 경로는 ignore 규칙에 해당한다.
따라서 새 체크아웃에는 로컬에서 확인한 Windows 뱅크가 자동으로 따라오지 않는다.
생성 ID 파일은 추적하지만 뱅크 자체는 제외하는 정책이다.

새 환경에서는 LFS 원본과 해당 버전의 Wwise 통합 네이티브 플러그인을 준비하고,
프로젝트에서 SoundBank를 생성한 뒤 `Rebuild`·검사를 수행해야 한다.
통합을 복구할 때는 추적된 C#·`.meta`와 로컬 패치(WebGL 제외 등)의 diff도 확인한다.
Launcher 재통합만으로 프로젝트의 모든 변경이 그대로 보존된다고 가정하지 않는다.

## 7. 현재 채택한 범위

### BGM은 고정된 64초 루프

`Music_Bed`와 `Enemy_Count` 크로스페이드 연결은 저작 프로젝트에 남아 있지만,
현재 `Play_Music`/`Stop_Music`의 대상은 `Music_Loop`이다.
**정적 연결로 판단하면 적 수의 변화는 현재 BGM의 레이어 구성에 영향을 주지 않는다.**
체력 Lowpass, 볼륨, 피치, 레벨업 State는 별도의 버스 설정으로 남아 있다.

적 수에 반응하는 음악으로 전환하려면 이벤트 대상을 변경하고 필요한 레이어가 든 뱅크를
다시 생성·복사한 뒤 재생을 검증해야 한다. 이벤트 이름을 유지하면 게임 호출부는 유지할 수 있지만,
대상만 바꾸면 현재 빌드에 즉시 적용되는 것은 아니다.

현재 사용자 뱅크는 `Main` 하나이고 Addressables 기반 로드나 CI 뱅크 생성 경로는 사용하지 않는다.
Mac 플랫폼 설정의 존재와 해당 플랫폼 재생 지원의 검증은 별개다.
뱅크 분할·압축·스트리밍 도입 여부는 향후 요구와 측정에 따라 판단하며, 이번 검토에서는 변경하지 않았다.

## 8. 남은 제약과 미검증 항목

| 항목 | 확인한 사실 / 판단 범위 |
|---|---|
| 렌더 스크립트의 중복 곡선 | 크로스페이드·체력 LPF 값을 Python에도 보관한다. Wwise 변경이 자동 반영되지 않으며 감상용 LPF는 근사 구현이다 |
| PCM·메모리 상주 | 저작의 Default Conversion Settings는 PCM이고 로컬 Main 메타데이터의 미디어는 메모리 상주다. Main은 약 6.19MB이며 압축·스트리밍의 효과는 측정하지 않았다 |
| 반복 단위 | 원본 레이어는 8초, 현재 BGM 파일은 64초다. 원본 레이어 반복감과 BGM 전체 반복 주기를 구분해야 한다 |
| 적 수 보고 | 스포너 관리 목록 수이며 보스·별도 생성 적은 포함하지 않는다. 0.5초 scaled-time 정리와 0.25초 unscaled-time 폴링이 겹치므로 최대 지연을 0.5초로 단정할 수 없다 |
| 체력 초기값·씬 전환 | 저작 기본값은 0이고 PlayerHealth를 찾으면 초기 체력을 보낸다. 씬 로드 때 적 수는 0으로 리셋하지만 체력 RTPC는 별도로 초기화하지 않아 플레이어 없는 씬에서 이전 값이 남을 수 있다(코드로부터의 추론) |
| 재생 검증 | Title → Game → 결과 → Title의 리스너·뱅크 상태, 픽업 라우팅, 볼륨·덕킹·필터·피치 청감은 이번에 실행 검증하지 않았다 |
| State 편집 자동화 | 기존 문서에는 Wwise-MCP의 `set_object`가 State 속성을 거부했다는 기록이 있다. 당시 요청·오류 원본을 대조하지 않았으므로 특정 도구 사용 이력으로만 남기며, Wwise 전체의 자동화 불가로 일반화하지 않는다 |

## 9. 과거 기록과 이번 확인 결과

### Git diff로 확인한 변경

- `ccde11c` — 기존 `BackgroundMusic`의 Resources/AudioSource 경로와 `SoundEffects`의 Unity 오디오 경로를 Wwise 호출로 교체하고 파이프라인 도구·저작 프로젝트·음원을 추가했다. `GameManager` 등에도 State 호출이 추가되었으므로 게임 코드가 전혀 변경되지 않은 전환은 아니다.
- 같은 커밋에 이미 `Music_Loop` 대상 이벤트와 LevelUp 버스 감쇠가 들어 있다. 문서에 함께 남아 있던 “Game_State 미배선”은 해당 커밋의 저작 설정과 맞지 않는다.
- `bf8e1d5` — WebGL 무음 백엔드, Wwise 어셈블리 제외, 뱅크 임시 이동·복원 및 통합 빌드 전처리 우회를 추가했다.

### 과거 증상 기록의 한계

기존 문서와 코드 주석에는 Init 로드 실패(`Bank Load Failed Name: 1355168291`)를 복사 누락으로,
이벤트 실패(`Event ID not found`)를 사용자 뱅크 로드 누락으로 설명한 기록이 있다.
Git에는 복사 도구와 `EnsureBanks()`가 들어온 결과가 확인되지만, 그 이전 실패 로그·실행 환경과
개별 수정 전후 재현까지 남아 있는 것은 이번에 확인하지 못했다.
특히 현재 에디터의 출력 경로 우선 탐색(§3)을 고려하면 Init 오류의 원인을 복사 누락 하나로 단정할 수 없다.

이번에 확인한 것은 코드의 호출 경로, 저작 객체·설정, 씬 참조, 로컬 Windows 뱅크와 메타데이터,
소스·사본의 정적 일치 및 검사기 실행 결과다. 실제 오디오 재생 성공이나 배포물의 청취 결과와는 구분한다.
