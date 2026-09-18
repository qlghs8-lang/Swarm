# WebGL 빌드 설정

Swarm을 브라우저에서 돌리기 위한 설정 문서. 대상은 이 저장소의 현재 상태다 —
**Unity 6000.4.3f1 / URP 2D Renderer / Input System 1.19 / Wwise 2025.1.10 (Windows·Mac만 설치)**.

결론부터: **막히는 곳은 딱 하나, Wwise다.** 그래픽·입력·물리는 전부 WebGL에서 그대로 돈다.
아래 §2가 이 문서의 8할이고 나머지는 스위치를 어디에 두느냐의 문제다.

이 문서는 **WebGL 빌드에서 소리를 끄는 경로**를 기준으로 쓴다.
소리가 나는 WebGL 빌드가 필요해지면 §7을 본다.

> **적용 현황 (2026-09-18)**
> **2026-09-18: 배포 완료.** Cloudflare Workers에서 실행 확인 — 전송 20.3MB,
> wasm 1.2초 / data 1.25초, 한글 렌더링 정상.
>
> 저장소에 반영 완료: §2-1(asmdef 5개), §2-3(`AudioDirector` 백엔드 분리),
> §2-5(`AkBuildPreprocessor` 가드), §4의 WebGL 템플릿.
> Web Build Support 모듈도 설치 확인됨.
> 남은 것은 전부 Unity 에디터 UI에서 해야 하는 설정이다 — §1의 Build Profile, §3, §4의 Graphics/Quality, §5.

---

## 0. 왜 Wwise가 막는가

`Assets/Wwise/API/Runtime/Generated/` 안에는 `Common`, `Mac`, `Windows` 세 폴더만 있다.
플랫폼별 폴더가 곧 `AkUnitySoundEngine`의 구현이고, 각 파일 첫 줄이 이렇게 잠겨 있다.

```csharp
// Generated/Windows/AkUnitySoundEngine_Windows.cs:1
#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
```

즉 **WebGL 플레이어 빌드에서는 `AkUnitySoundEngine`의 메서드가 한 개도 존재하지 않는다.**
`Common` 폴더의 열거형(`AKRESULT` 등)만 남는다. 그 상태로 빌드를 걸면

- `AK.Wwise.Unity.API` — `AkUnitySoundEnginePINVOKE` 부재
- `AK.Wwise.Unity.API.WwiseTypes` — 11개 파일이 `AkUnitySoundEngine` 호출
- `AK.Wwise.Unity.MonoBehaviour` — `AkInitializer`, `AkGameObj`, `AkAudioListener` 전부
- `AK.Wwise.Unity.Timeline` — 6개 파일

이 네 어셈블리가 컴파일 에러로 무너지고, 이들을 참조하는 `Swarm.Runtime`이 같이 무너진다.
`Assets/Wwise/API/Runtime/Plugins/`에 `Mac`, `Windows`뿐이라 네이티브 쪽도 없다.

**다행인 점**은 이 프로젝트가 처음부터 Wwise를 한 파일에 가둬뒀다는 것이다.

```
$ grep -rl "AkUnitySoundEngine" Assets/_Project/Scripts
Assets/_Project/Scripts/Audio/AudioDirector.cs
```

`AudioDirector.cs` 안의 호출 지점은 8곳뿐이고, `BackgroundMusic` / `SoundEffects` /
`WwiseGameSync`는 전부 `AudioDirector`만 부른다. 게임 코드는 한 줄도 손대지 않아도 된다.

---

## 1. 모듈 설치와 플랫폼 전환

1. **Unity Hub → Installs → 6000.4.3f1 → ⚙ → Add modules → `Web Build Support`**.
   Unity 6.4에서 모듈 이름이 **`WebGL Build Support` → `Web Build Support`** 로 바뀌었다.
   디스크 4.14GB(IL2CPP + Emscripten 툴체인 포함). — ✅ 이 머신에는 이미 설치되어 있다.

   > 이름만 바뀌었고 **내부 식별자는 여전히 `WebGL`** 이다. §2-1의 asmdef에 `"WebGL"`을 쓰는 게 맞는지는
   > Unity가 스스로 증명한다 — 6000.4.3f1에 딸려오는 URP core 패키지의
   > `Unity.UnifiedRayTracing.Runtime.asmdef`가 `"excludePlatforms": ["WebGL"]`을 쓴다.
   > `BuildTarget.WebGL`, `UNITY_WEBGL` 심볼도 그대로다. 바뀐 것은 UI 표기뿐이다.
2. Unity 재시작 후 **File → Build Profiles**.
   현재 `Assets/Settings/Build Profiles/Windows.asset` 하나만 있다.
3. **Web(WebGL) 선택 → Build Profiles 목록에서 `+` → 새 프로필 생성 → 이름 `WebGL`**.
   `Assets/Settings/Build Profiles/WebGL.asset`으로 저장하면 Windows 프로필과 나란히 관리된다.
4. **Switch Profile** — 첫 전환은 텍스처 재임포트 때문에 5~15분 걸린다. 한 번뿐이다.

> Build Profile을 쓰면 WebGL 전용 설정(압축·메모리·씬 목록)이 Windows 빌드 설정을 건드리지 않는다.
> 데스크톱 포트폴리오 빌드와 웹 빌드를 동시에 유지할 거면 이 방식이 맞다.

씬 목록은 Windows와 같다: `Title` → `Game` → `TestStage`.
웹 배포본에서 `TestStage`는 빼는 걸 권한다(빌드 크기와 오해 방지).

### ⚠️ Build Profile이 Player Settings를 덮어쓴다

**이것 때문에 두 번의 빌드를 날렸다.** 프로필을 만들면 유니티가 그 시점의 Player Settings를
**통째로 스냅샷해서 프로필 안에 복사해 넣는다**(`Web.asset`의 `m_PlayerSettingsYaml`, 866줄).
그 뒤로 Project Settings → Player에서 무엇을 바꾸든 **빌드는 프로필 안의 사본을 쓴다.**

에러도 경고도 없다. Player Settings 화면에는 바꾼 값이 그대로 보이고, `ProjectSettings.asset`에도
정상으로 저장되어 있다. 빌드 결과물만 옛날 값으로 나온다.

실제로 이 프로젝트에서 무시된 것들:

| 설정 | Project Settings (06:48에 설정) | 프로필 사본 (06:43 생성 시점) |
|---|---|---|
| `webGLTemplate` | `PROJECT:Swarm` | **`APPLICATION:Default`** |
| `webGLInitialMemorySize` | `256` | **`32`** |
| `runInBackground` | `0` | **`1`** |
| `m_ShowUnitySplashScreen` | — | `1` |

증상은 "커스텀 템플릿이 적용되지 않는다"로 나타난다. 그래서 템플릿 파일 자체를 의심하게 되는데,
템플릿에는 아무 문제가 없다. `webGLTemplate: PROJECT:Swarm`이 `ProjectSettings.asset`에 멀쩡히
들어 있는 것을 확인하고도 결과물이 기본 템플릿이면, **템플릿이 아니라 프로필을 봐야 한다.**

**조치**: Build Profiles 창 → 해당 프로필 → **Player Settings 섹션의 `⋮` 메뉴 → Remove**.
섹션이 사라지고 프로필은 전역 Player Settings를 그대로 따른다.
(`Reset`은 섹션을 남긴 채 값만 전역과 맞추는 것이고, `Remove`가 "이 프로필은 전역을 쓴다"는 뜻이다.)

프로필별로 다른 값이 정말 필요할 때만 오버라이드를 남긴다. 이 프로젝트는 Web 전용 값이
Player Settings의 Web 탭에 이미 플랫폼별로 갈려 있으므로 오버라이드가 할 일이 없다.

**확인**: 프로필 에셋에 `m_Settings: []` 이면 오버라이드가 없는 상태다.

```bash
grep -A2 "m_PlayerSettingsYaml" "Assets/Settings/Build Profiles/Web.asset"
```

---

## 2. Wwise 분리 — 핵심 작업

네 단계다. 순서대로 하면 된다.

### 2-1. AK 런타임 어셈블리를 WebGL에서 제외 — ✅ 적용됨

아래 **5개 파일**의 `excludePlatforms`에 `"WebGL"`을 추가한다.

| 파일 | 어셈블리 | 왜 |
|---|---|---|
| `Assets/Wwise/API/Runtime/AK.Wwise.Unity.API.asmdef` | `AK.Wwise.Unity.API` | PINVOKE 본체 |
| `Assets/Wwise/API/Runtime/WwiseTypes/AK.Wwise.Unity.API.WwiseTypes.asmdef` | `AK.Wwise.Unity.API.WwiseTypes` | 11개 파일이 `AkUnitySoundEngine` 호출 |
| `Assets/Wwise/MonoBehaviour/Runtime/AK.Wwise.Unity.MonoBehaviour.asmdef` | `AK.Wwise.Unity.MonoBehaviour` | `AkInitializer` / `AkGameObj` 등 |
| `Assets/Wwise/Timeline/Runtime/AK.Wwise.Unity.Timeline.asmdef` | `AK.Wwise.Unity.Timeline` | 6개 파일이 호출 |
| `Assets/Wwise/API/Runtime/Handwritten/WAAPI/Ak.Wwise.Api.WAAPI.asmdef` | `Ak.Wwise.Api.WAAPI` | **전이 의존** — 아래 참고 |

```diff
   "excludePlatforms": [
     "Lumin",
-    "Stadia"
+    "Stadia",
+    "WebGL"
   ],
```

`Logging` / `Utilities`만 WebGL 빌드에 남는다. 순수 C#이고 `AkUnitySoundEngine`을 참조하지 않는다.
`ProjectDatabase`, `OpenXR`, `*.Editor` 어셈블리들은 `includePlatforms`가 이미 WebGL을 빼고 있어 무관하다.

#### 전이 의존을 손으로 세지 말 것

`AkUnitySoundEngine`을 직접 부르는 어셈블리만 빼면 안 된다. `Ak.Wwise.Api.WAAPI`는
`AkUnitySoundEngine`을 한 번도 부르지 않지만 `WwiseTypes`의 `WwiseObjectType`을 쓴다.
그래서 WwiseTypes만 빼면 **26초를 태우고** 이렇게 죽는다.

```
Assets\Wwise\API\Runtime\Handwritten\WAAPI\AkWaapiHelper.cs(368,9):
error CS0246: The type or namespace name 'WwiseObjectType' could not be found
Error building Player because scripts had compiler errors
```

asmdef를 건드린 뒤에는 참조 폐포가 닫혔는지 확인한다.
"WebGL 빌드에 들어가면서, 빠지는 어셈블리를 참조하는" 것이 남아 있으면 안 된다
(`Swarm.Runtime` 하나는 예외 — §2-2·§2-3에서 `#if`로 가드했다).

```python
# 프로젝트 루트에서 실행
import json, io, glob
a = {}
for q in glob.glob("Assets/**/*.asmdef", recursive=True):
    x = json.load(io.open(q, encoding="utf-8"))
    a[x["name"]] = (x.get("references") or [], x.get("includePlatforms") or [], x.get("excludePlatforms") or [])

def inbuild(n):
    if n not in a: return None                      # 패키지 — 판단 제외
    refs, inc, exc = a[n]
    return ("WebGL" in inc) if inc else ("WebGL" not in exc)

for n, (refs, _, _) in sorted(a.items()):
    if inbuild(n) is not True: continue
    bad = [r for r in refs if inbuild(r) is False]
    if bad: print(n, "->", bad)
```

기대 출력은 `Swarm.Runtime -> ['AK.Wwise.Unity.API', 'AK.Wwise.Unity.API.WwiseTypes']` 한 줄뿐이다.

asmdef의 플랫폼 목록에서 **Editor는 별개 항목**이다. `WebGL`만 제외하면
에디터는 빌드 타깃이 WebGL이어도 계속 Wwise를 컴파일한다 — 즉 **에디터 플레이 모드에서는 소리가 그대로 난다.**

> ⚠️ 이 5개 파일은 Wwise가 제공한 파일이다. **Wwise Launcher로 통합을 다시 돌리면 덮어쓰인다.**
> 재통합 후에는 이 표를 다시 적용해야 한다. `docs/wwise-pipeline.md`에 한 줄 남겨두면 좋다.

### 2-2. `Swarm.Runtime.asmdef`는 그대로 둔다

```json
"references": [ ..., "AK.Wwise.Unity.API", "AK.Wwise.Unity.API.WwiseTypes" ]
```

WebGL 빌드 시 이 참조는 "해당 플랫폼에 없는 어셈블리"로 조용히 무시된다.
콘솔에 참조 경고가 뜰 수 있으나 빌드는 진행된다. **단, 다음 단계가 전제다.**

### 2-3. `AudioDirector`에 WebGL 무음 경로를 만든다 — ✅ 적용됨

`Assets/_Project/Scripts/Audio/AudioDirector.cs`의 **Wwise를 부르는 부분만**
`#if UNITY_WEBGL && !UNITY_EDITOR`로 갈라낸다. public API 시그니처는 한 글자도 바꾸지 않는다 —
그래야 `WwiseGameSync`, `BackgroundMusic`, `SoundEffects`, `GameManager`가 전부 그대로 컴파일된다.

조건이 `UNITY_WEBGL`이 아니라 **`UNITY_WEBGL && !UNITY_EDITOR`** 인 이유:
2-1에서 본 대로 에디터는 WebGL 타깃에서도 Wwise를 들고 있다. 에디터에서까지 소리를 죽일 이유가 없다.

권장 형태 (파일 전체를 감싸지 말고, Wwise 호출 구현부만 스텁으로 교체):

```csharp
public static class AudioDirector
{
    // ── public API는 플랫폼과 무관하게 동일 ──────────────────────────
    public static void SetEnemyCount(int count)      => SetRtpc(Rtpc.EnemyCount, count);
    public static void SetPlayerHealth(int c, int m) => SetRtpc(Rtpc.PlayerHealth, ...);
    public static void Flush()                       { /* ... */ }

#if UNITY_WEBGL && !UNITY_EDITOR
    // ── WebGL: Wwise 어셈블리가 빌드에 없다. 전부 무음 no-op ──────────
    private static bool IsReady => false;
    private static void SetRtpc(Rtpc slot, float value) { }
    private static bool  Post(uint eventId)             => false;
    private static void  PushState()                    { }
    private static bool  EnsureBanks()                  => false;
#else
    // ── 기존 Wwise 구현 그대로 ───────────────────────────────────────
    private static bool IsReady { get { if (!AkUnitySoundEngine.IsInitialized()) return false; ... } }
    // AkUnitySoundEngine.SetRTPCValue / PostEvent / SetState / LoadBank / RegisterGameObj
#endif
}
```

`AKRESULT`, `AkUnitySoundEngine`, `WwiseIds.Events.*`를 쓰는 줄이 **전부** `#else` 쪽에 들어가야 한다.
`WwiseIds.generated.cs`는 `uint` 상수뿐이라 그대로 두어도 무해하다(참조만 안 하면 된다).

빠뜨리기 쉬운 곳: `_host` GameObject 생성/등록, 뱅크 로드 실패 로그, `Rtpc` 배열 초기화.
배열과 열거형은 플랫폼 공통이므로 `#if` 밖에 둔다.

**검증**: `UNITY_WEBGL && !UNITY_EDITOR` 블록에서 `Ak`로 시작하는 식별자가 0개여야 한다.

#### 실제 적용된 구조

파일을 두 층으로 갈랐다.

- **위층(플랫폼 공통)** — 공개 API 11개 + 지연 플러시 상태 관리(`SetRtpc`, `FlushState`, `Flush`).
  `_rtpcValues` / `_rtpcPending` / `_musicWanted` / `_statePending` 같은 상태는 WebGL에서도 그대로 돈다.
- **아래층(플랫폼별)** — 백엔드 4개뿐이다.

| 백엔드 멤버 | Wwise | WebGL |
|---|---|---|
| `IsReady` | `AkUnitySoundEngine.IsInitialized()` + 호스트 오브젝트 | `false` |
| `TryPostEvent(uint)` | 뱅크 보장 후 `PostEvent` | `false` |
| `TryApplyRtpc(int, float)` | `SetRTPCValue` | `false` |
| `TryApplyState(GameAudioState)` | `SetState` | `false` |

`EnsureHost` / `EnsureBanks` / `_host` / `_banksLoaded`도 Wwise 쪽으로 내려갔다.
`WwiseIds` 상수와 `StateIds` / `RtpcIds` 배열은 순수 `uint`라 공통 층에 남겼다 — WebGL에서도 컴파일된다.

부수 효과로 Wwise 경로가 전보다 얇아졌다. `Flush()`의 RTPC 루프가 `AkUnitySoundEngine`을
직접 부르던 것이 `TryApplyRtpc` 한 줄이 됐고, 동작은 동일하다.

이 네 개를 채우면 다른 백엔드(Unity `AudioSource`, Wwise WebGL)를 붙일 수 있다.

### 2-4. Title 씬의 Wwise 오브젝트

`Assets/_Project/Scenes/Title.unity`에만 Wwise 컴포넌트가 4개 있다.

- `WwiseGlobal` 오브젝트 — `AkInitializer`
- 메인 카메라 — `AkAudioListener`, `AkGameObj`

WebGL 빌드에서 이 스크립트 클래스들은 존재하지 않으므로
런타임에 `The referenced script on this Behaviour is missing` 경고가 뜬다.
**게임 동작에는 영향이 없다.** 브라우저 콘솔이 지저분한 게 싫으면 두 가지 중 하나:

- (간단) 그대로 두고 경고를 무시한다 — 권장.
- (깔끔) `WwiseGlobal`을 씬에서 빼고, `#if !UNITY_WEBGL || UNITY_EDITOR` 가드가 걸린
  부트스트랩 코드에서 `AkInitializer` 프리팹을 런타임 생성하도록 옮긴다.
  `AkAudioListener`/`AkGameObj`도 같은 자리에서 `AddComponent`.

### 2-5. 빌드 전처리기가 빌드를 죽인다 — ✅ 적용됨

**이게 실제로 빌드를 멈춘 원인이다.** 경고가 아니라 에러였다.

`Assets/Wwise/MonoBehaviour/Editor/WwiseSetupWizard/AkBuildPreprocessor.cs`는
Wwise가 등록한 유일한 빌드 콜백이고(`IPreprocessBuildWithReport`), 빌드 직전에
`StreamingAssets/Audio/GeneratedSoundBanks/<플랫폼>`을 복사하고 플러그인을 활성화한다.
WebGL SDK가 없으니 세 곳이 전부 `LogLevel.Error`를 뱉는다.

```
WwiseUnity: (ERROR) Could not find source folder for <WebGL> platform. Did you remember to generate your banks?
WwiseUnity: (ERROR) SoundBank folder has not been copied for <WebGL> target at <...\Build\WebGl>.
WwiseUnity: (ERROR) Unable to find Plugin Activator for Build Target WebGL.
Error building Player: 3 errors
Build completed with a result of 'Failed' in 0 seconds
```

Unity는 **빌드 전처리기가 뱉은 에러를 빌드 실패로 취급한다.** 그래서 0초 만에 멈춘다 —
컴파일이나 링크까지 가보지도 못한다. 그 앞에 뜨는 경고 두 줄
(`Target WebGL is not supported by default by Wwise`)은 같은 원인의 전조일 뿐이다.

경고문이 제안하는 `GetCustomTargetPlatformName` 델리게이트는 답이 아니다.
WebGL을 Windows로 매핑해봐야 쓰지도 않을 6MB 뱅크를 웹 빌드에 밀어넣을 뿐이고,
세 번째 에러(Plugin Activator)는 그대로 남는다.

**조치**: 지원되지 않는 타깃이면 전처리기 전체를 건너뛴다.
`AkBuildPreprocessor`에 가드를 하나 넣고 `OnPreprocessBuildInternal` /
`OnPostprocessBuildInternal` 양쪽에서 조기 반환한다(대칭이어야 한다 —
전처리에서 건드리지 않은 것을 후처리가 되돌리려 들면 안 된다).

```csharp
private static bool IsWwiseUnsupportedTarget(UnityEditor.BuildTarget target)
{
    return target == UnityEditor.BuildTarget.WebGL;
}
```

WebGL 빌드는 어차피 무음이므로(§2-3) 이 전처리기가 할 일이 애초에 없다.

> ⚠️ **이 파일도 Wwise 제공 파일이다.** §2-1의 asmdef 4개와 함께,
> Launcher로 재통합할 때마다 다시 적용해야 하는 목록에 들어간다.

#### StreamingAssets의 6MB — ✅ 적용됨

`Assets/StreamingAssets/Audio/GeneratedSoundBanks/Windows/`에 `Main.bnk` 6MB가 들어 있고,
StreamingAssets는 **모든 플랫폼 빌드에 무조건 포함된다.** WebGL 빌드가 절대 읽지 않을 6MB다.
폰트를 480KB로 서브셋한 빌드에 이걸 얹는 건 말이 안 된다.

`AudioPipeline.BuildHook`이 WebGL일 때만 다르게 움직인다 — 복사하는 대신 **치운다.**

| | 데스크톱 | WebGL |
|---|---|---|
| 전처리 | 뱅크 복사 + `WwiseIds` 재생성 | 뱅크를 `Library/SwarmWebGLAudioStash`로 이동 |
| 후처리 | (없음) | 제자리로 되돌림 |

**그냥 지우지 않는 이유**가 중요하다. StreamingAssets의 뱅크가 사라지면 에디터 Play 모드에서
`Bank Load Failed`가 프레임마다 쏟아지는데, 뱅크에는 아무 문제가 없고 복사가 안 됐을 뿐이라
로그만 봐서는 원인이 보이지 않는다 — `AudioPipeline` 클래스 주석이 말하는 바로 그 실패다.
웹 빌드를 한 번 돌렸다는 이유로 그 상태에 빠지면 안 된다.

빌드가 중간에 실패하면 후처리가 돌지 않아 뱅크가 `Library`에 남는다. 그래서
`[InitializeOnLoadMethod]`가 에디터 코드 로드 때마다 남은 것을 확인하고 되돌린다.
`.meta`도 함께 옮긴다 — 폴더만 사라지고 meta가 남으면 유니티가 고아 meta를 지우고
되돌릴 때 GUID가 새로 발급된다.

---

## 3. Player Settings — Web 탭

`Project Settings → Player → Web` (Build Profile을 쓰면 프로필의 Player Settings Overrides).

### Publishing Settings

| 항목 | 현재값 | 권장값 | 이유 |
|---|---|---|---|
| **Compression Format** | Gzip(0) | **Brotli** | wasm이 20~30% 더 줄어든다. 단 §5의 호스팅 제약 확인 |
| **Decompression Fallback** | Off | **호스팅에 따라** | 서버 헤더를 못 건드리면 **반드시 On** (§5) |
| **Data Caching** | On | On | IndexedDB 캐시. 재방문 로딩이 즉시 |
| **Name Files As Hashes** | Off | On | 캐시 무효화가 파일명으로 해결된다 |
| **Debug Symbols** | Off | Off (릴리스) | |

### 메모리

| 항목 | 현재값 | 권장값 |
|---|---|---|
| **Initial Memory Size** | 32 MB | **256 MB** |
| **Memory Growth Mode** | Geometric(2) | Geometric 유지 |
| **Maximum Memory Size** | 2048 MB | 512 MB |

초기값 32MB는 Unity 기본값이다. Swarm은 적 400마리 풀을 시작 시점에 미리 잡으므로
32MB에서 출발하면 로딩 중 힙 성장이 여러 번 일어나 첫 프레임이 눈에 띄게 늦다.
256MB로 시작해두고, 실제 사용량을 **Show Diagnostics** 오버레이로 한 번 재서 조인다.

### 기타

| 항목 | 권장값 | 이유 |
|---|---|---|
| **Exception Support** | **None** (릴리스) / Explicitly Thrown (개발) | None이면 wasm이 15~20% 작아지고 빨라진다. 단 `try/catch`가 무력화되므로 개발 중에는 켜둔다 |
| **Power Preference** | High Performance | 노트북에서 내장 GPU 대신 외장 GPU 선택 |
| **Run In Background** | Off | 탭이 가려지면 멈추게 — 서바이버 게임은 백그라운드 진행이 버그다 |
| **Managed Stripping Level** | Low → Medium | Medium에서 리플렉션 의존 코드가 깨지면 `link.xml` 추가 |
| **Strip Engine Code** | On (이미 On) | |
| **Splash Screen** | 취향 | 현재 On. 웹에서는 로딩이 이미 기니 Off 권장 |
| **WebGL Template** | Default → 커스텀 | §4 |

---

## 4. 그래픽 · 품질 · 템플릿

**Graphics APIs**: `Auto Graphics API` 끄고 **WebGL 2.0 단독**으로 고정.
WebGL 1.0 폴백을 남겨두면 URP 2D Renderer가 제대로 안 그려지는 브라우저가 생긴다.
WebGPU는 Unity 6에서 아직 실험적이라 포트폴리오 배포본에는 넣지 않는다.

**Quality**: `Project Settings → Quality`에서 WebGL 열의 기본 레벨을 지정한다.
현재 레벨 5개 중 `antiAliasing: 2`인 레벨이 하나 있는데, 웹에서는 **antiAliasing 0**을 쓴다
(2D 픽셀 아트라 MSAA가 득이 없고 필레이트만 먹는다). `vSyncCount`는 웹에서 무시된다.

**프레임레이트**: WebGL은 `Application.targetFrameRate`를 무시하고 브라우저의
`requestAnimationFrame`(대개 60Hz, 고주사율 모니터에서는 120/144Hz)에 묶인다.
Swarm의 이동·쿨다운·웨이브 타이머가 전부 `deltaTime` 기반인지 한 번 확인한다
(데스크톱 실측에서 프레임 독립성은 이미 잡혀 있으나 고주사율은 별개 케이스다).

**WebGL Template** — ✅ `Assets/WebGLTemplates/Swarm/index.html` 추가됨.
Player Settings → Resolution and Presentation → **WebGL Template = Swarm**으로 바꿔 쓴다.

담긴 것:

- **16:9 레터박스**. 캔버스를 창에 꽉 채우지 않는다 — UI 앵커가 데스크톱 빌드와 같은 자리에 온다.
- **로딩 화면**: 타이틀 + 진행 바 + 퍼센트. 다 받으면 페이드 아웃.
- **캔버스 포커스 처리**. 로드 직후와 클릭 시 `canvas.focus()`. 안 하면 첫 키 입력이 페이지로 새서
  화면이 스크롤된다 — WebGL 빌드에서 제일 흔한 첫인상 버그다.
- **방향키/스페이스 `preventDefault`**. 같은 이유.
- **모바일 차단**. 터치 기기면 로더를 받기 전에 안내 화면으로 끊는다(40MB 헛다운로드 방지).
- **로더 실패 메시지**가 §5의 압축/헤더 문제를 직접 지목한다. 흰 화면 대신 원인이 뜬다.

하단 힌트 문구("이동 WASD / 방향키 · 나머지는 자동")는 `index.html`의 `.hint`에서 고친다.

#### 템플릿이 적용되지 않을 때

**먼저 §1의 Build Profile 오버라이드를 의심한다.** 이 프로젝트에서 "커스텀 템플릿이 무시된다"의
실제 원인은 그것이었다. 템플릿 파일에는 문제가 없었다.

구별법은 출력 폴더다.

```
Build/<이름>/index.html        → <title>Unity Web Player | Swarm</title> 이면 기본 템플릿
Build/<이름>/TemplateData/     → unity-logo-dark.png 가 있으면 기본 템플릿
```

그와 별개로, 이 템플릿은 CSS를 `TemplateData/style.css`로 분리해 두었다.
유니티의 템플릿 전처리기는 `index.html`에서 줄 첫머리의 `#`를 지시자로 훑으므로,
`#loader { ... }` 같은 선택자를 인라인으로 두는 것은 피하는 편이 안전하다.
유니티 기본 템플릿이 늘 CSS를 밖으로 빼는 것과 같은 모양새다.
(이것이 위 증상의 원인은 아니었다 — 원인은 프로필 오버라이드였다.)

`index.html`을 고친 뒤에는 이걸로 확인한다 — `#if` / `#endif` 외에 아무것도 나오면 안 된다.

```bash
grep -n "^#" Assets/WebGLTemplates/Swarm/index.html
```

---

## 5. 압축과 호스팅 — 여기서 제일 많이 막힌다

Unity는 `.br` / `.gz` 파일을 만들고, **브라우저가 알아서 풀도록 서버가 헤더를 붙여주길 기대한다.**

| 압축 | 서버가 보내야 하는 헤더 |
|---|---|
| Brotli | `Content-Encoding: br` |
| Gzip | `Content-Encoding: gzip` |
| wasm 공통 | `Content-Type: application/wasm` |

헤더를 못 붙이는 호스팅에서는 **Decompression Fallback을 켠다.**
켜면 Unity가 JS 디코더를 로더에 끼워넣고 확장자가 `.unityweb`이 된다.
로더가 커지고 로딩이 느려지지만 **어떤 정적 호스팅에서도 돈다.**

| 호스팅 | 설정 |
|---|---|
| **Cloudflare Pages / Netlify** | `_headers`로 `Content-Encoding` 지정 → **Brotli + Fallback Off** (최적, 이 프로젝트의 선택) |
| **itch.io** | 헤더 제어 불가 → **Brotli + Decompression Fallback On**. zip 업로드 후 "This file will be played in the browser" 체크 |
| **GitHub Pages** | 헤더 제어 불가 → 동일하게 **Fallback On** |
| **직접 서버(nginx)** | `add_header Content-Encoding br;` → Fallback Off |

#### 포트폴리오 링크: Cloudflare Workers — ✅ 배포 완료 (2026-09-18, 실행 확인)

포트폴리오에서 이어지는 링크 하나가 목적이면 로딩 속도가 먼저다.
Cloudflare Workers의 "Upload your static files"에 `Build/<이름>` 폴더를 통째로 올린다
(`_headers`가 **올리는 폴더의 최상단**에 있어야 규칙이 먹는다).

#### ⚠️ Cloudflare에서는 Content-Encoding을 직접 붙이면 안 된다

**이 프로젝트에서 마지막으로 막힌 지점이다.** 처음에는 `_headers`에 이렇게 뒀다.

```
/*.wasm.br
  Content-Type: application/wasm
  Content-Encoding: br
```

헤더는 **정상으로 붙었다.** 응답을 직접 확인하면 `content-encoding: br`도,
`content-type: application/wasm`도 그대로 있다. 그런데 게임은 로딩 90%에서 멈췄다.

```
Unable to parse Build/WebGl.framework.js.br!
Malformed binary data URL Build/WebGl.data.br.
No "Content-Length" HTTP Response header present.
```

원인은 **이중 압축**이다. Cloudflare는 응답을 자기가 다시 압축하는데, 올린 파일은 이미
유니티가 brotli로 압축해 둔 것이다. 본문은 `br(br(파일))`이 되고 `Content-Encoding`은
한 겹만 선언된다. 브라우저가 한 겹 벗기고 나면 유니티에게는 여전히 압축된 덩어리가 남는다.

직접 바이트를 찍어 확인한 것이 결정적이었다 — **한 겹 디코드한 뒤에도** 선두가 이랬다.

```
firstBytesAsText : "UnityWeb Compressed Content (brotli)"   ← JavaScript가 아니다
contentLength    : null                                     ← 재인코딩의 증거
```

`content-length`가 없는 것이 신호다. 원본을 그대로 보냈다면 길이를 알 수 있다.
없다는 건 서버가 스트리밍으로 다시 인코딩하고 있다는 뜻이다.

**헤더가 응답에 보인다고 해서 의도대로 동작하는 것이 아니다.** 이 건은 "규칙이 안 먹었나"를
의심하며 시간을 쓰기 쉬운데, 규칙은 처음부터 잘 먹고 있었고 문제는 그 반대편에 있었다.

**조치**: Publishing Settings → **Decompression Fallback On**.
유니티가 자체 JS 디코더를 로더에 넣고 파일 확장자가 `.unityweb`으로 바뀐다.
서버가 무엇을 하든 상관이 없어진다 — Cloudflare가 전송 중에 한 겹 압축하든 말든,
그건 브라우저가 정상적으로 처리하는 한 겹일 뿐이다.
`_headers`의 규칙은 전부 걷어내고 경위만 주석으로 남겼다.

대가는 로딩 1~3초다. 내려받기가 끝나야 압축을 풀기 시작하기 때문이다.
직접 운영하는 서버(nginx 등)로 옮겨 재압축이 없는 환경이라면 Fallback을 끄고
`_headers` 규칙을 되살리는 편이 빠르다. **판별법은 `content-length`의 유무다.**

#### 왜 "압축 끄기"는 답이 아닌가

"유니티 압축을 끄고 Cloudflare가 압축하게 하면 되지 않나"가 자연스러운 다음 수인데,
Cloudflare Workers의 정적 자산은 **파일당 25MiB 제한**이 있다. 무압축 `.data`와 `.wasm`은
그 선을 넘기 쉽다. 게다가 Cloudflare가 `application/octet-stream`을 자동 압축한다는 보장도 없어,
가장 큰 파일이 무압축으로 나갈 위험이 있다.
---

## 6. 코드 레벨 점검

- **`DebugPerfProbe.cs`** — `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 안에서 `File.WriteAllText`로
  마크다운 리포트를 쓴다. WebGL **개발 빌드**를 만들면 브라우저 샌드박스에서 예외가 난다.
  해당 저장 경로를 `#if !UNITY_WEBGL`로 한 번 더 감싸거나, WebGL은 릴리스 빌드만 만든다.
- **`PlayerPrefs`** — 볼륨 설정(`swarm.bgm.volume`, `swarm.sfx.volume`)이 여기 저장된다.
  WebGL에서는 IndexedDB에 비동기로 flush되므로, 설정을 바꾼 직후 `PlayerPrefs.Save()`를
  명시 호출해두는 게 안전하다. 시크릿 모드/쿠키 차단 환경에서는 유실될 수 있다.
- **한글 폰트** — ✅ 적용됨. WebGL에서 **한글만** 안 보이고 영문·숫자는 멀쩡한 증상의 원인이다.
  프로젝트의 모든 `Text`가 유니티 내장 폰트(`m_Font: {fileID: 10102, guid: 0000...e000...}` =
  `LegacyRuntime.ttf`)를 물고 있었는데, 거기에는 한글 글리프가 없다. 데스크톱에서는 유니티가
  빠진 글자를 OS 폰트(맑은 고딕)로 대신 그려줘서 드러나지 않았다. **WebGL에는 OS 폰트가 없다.**

  조치는 세 갈래다.

  1. **폰트를 빌드에 넣는다** — `Assets/_Project/Resources/Galmuri11-Swarm.ttf`.
     [Galmuri](https://galmuri.quiple.dev) 11 (SIL OFL, 한국 인디 픽셀게임용 비트맵 스타일).
     원본은 20,965 글리프 5.4MB라 **상용한글 2350자 + ASCII + 자모로 서브셋해 480KB**로 줄였다.
     `pyftsubset Galmuri11.ttf --text-file=<문자목록> --no-hinting --desubroutinize`.
     실제로 프로젝트가 쓰는 한글은 415자뿐이지만, 2350자를 넣어 앞으로 추가될 문자열까지 덮는다.
     라이선스 원문은 `Assets/_Project/Fonts/Galmuri-OFL.txt`.
  2. **씬·프리팹의 참조를 바꾼다** — `m_Font`를 `{fileID: 12800000, guid: <폰트 guid>, type: 3}`로.
     씬 3개 + 프리팹 2개, 총 44곳.
  3. **런타임 생성 UI를 모은다** — 인스펙터를 거치지 않고 코드가 `new GameObject`로 만드는 Text가
     세 군데 있었다(`SettingsMenu`, `BuffIconBarUI`, `DebugGoldButton`). 전부 내장 폰트를 직접
     불렀으므로 2번만으로는 설정 메뉴의 한글이 그대로 안 보인다. `Swarm.UI.UiFont.Current` 하나로 모았다.

  > 렌더링 모드는 `fontRenderingMode: 0`(Smooth)이다. 픽셀 폰트지만 프로젝트의 `m_FontSize`가
  > 18·20·24·28…로 11의 배수가 아니라, HintedRaster로 두면 자간이 들쭉날쭉해진다.
  > 픽셀을 살리려면 폰트 크기를 11의 배수(22·33·44)로 맞춘 뒤 래스터로 바꾼다.

- **`GoldWallet`** — 잔액을 캐시해 두고 `Application.focusChanged` / `sceneUnloaded` /
  `Application.quitting` 세 지점에서만 flush한다. 웹에서 **`Application.quitting`은 탭을 닫을 때 안 뜬다.**
  다만 탭 전환·창 밖 클릭이면 `focusChanged`가, 사망→결과→타이틀이면 `sceneUnloaded`가 잡아주므로
  실제로 잃는 경우는 "골드를 먹고 곧장 탭을 닫는" 한 갈래뿐이다. 신경 쓰이면
  `#if UNITY_WEBGL`에서 `OnApplicationPause`나 `visibilitychange` 훅을 하나 더 건다.
- **Input System** — 키보드·마우스는 문제없다. 게임패드는 브라우저 Gamepad API에 의존해
  연결 감지가 첫 입력 이후에 되는 경우가 있다. 웹 빌드는 키보드 기준으로 안내한다.
- **`Application.Quit()`** — 웹에서는 아무 일도 하지 않는다. 결과/타이틀 화면에 종료 버튼이
  있다면 `#if !UNITY_WEBGL`로 숨긴다.
- **첫 입력 전 오디오** — §2대로 무음이므로 해당 없음. §7 경로를 택하면
  브라우저 자동재생 정책 때문에 **사용자 클릭 이후에 사운드 엔진을 초기화**해야 한다.

---

## 7. (대안) 소리가 나는 WebGL 빌드

Wwise 2025.1 통합 자체는 WebGL을 지원한다 —
`AkInitializer.cs`, `AkSoundEngineController.cs`에 `#if UNITY_WEBGL` 분기가 이미 들어 있다.
지금 안 되는 이유는 **Launcher에서 WebGL 플랫폼 SDK를 안 깔았기 때문**이다.

경로는 이렇다.

1. Wwise Launcher → 해당 Wwise 버전 → **Add/Remove Platforms → WebGL** 설치
2. Unity 프로젝트에 통합 재적용 → `Generated/WebGL`, `Plugins/WebGL` 생성
3. **Addressables + Wwise Addressables 패키지**가 사실상 필수다.
   WebGL에서는 사운드뱅크를 바이너리로 받아 `LoadBankFromBytes`로 올려야 하고,
   커뮤니티 보고로는 `AkAddressableBankManager.cs`에 손을 대야 하는 경우가 많다.
4. `SharedArrayBuffer` 지원(= COOP/COEP 헤더)이 필요한 구성이면 itch.io에서는 별도 옵션이 필요하다.

즉 **현재 구조(뱅크 직접 로드, Addressables 미사용)에서 꽤 큰 공사**다.
포트폴리오용 웹 데모가 목적이라면 §2의 무음 경로로 먼저 띄우고,
"웹 버전은 무음, 사운드는 데스크톱 빌드에서" 라고 안내하는 편이 비용 대비 낫다.

---

## 8. 체크리스트

```
[x] Unity Hub에서 Web Build Support 모듈 설치 (6.4에서 개명, 설치됨)
[x] Build Profile 생성 (이름 Web)
[x] AK asmdef 5개에 "WebGL" excludePlatforms 추가 (WAAPI 포함 — 전이 의존)
[x] 참조 폐포 검사 통과
[x] AudioDirector.cs — #if UNITY_WEBGL && !UNITY_EDITOR 무음 스텁
[x]   └ 스텁 블록에 Ak* 식별자 0개인지 확인
[x] Assets/WebGLTemplates/Swarm/index.html 추가
[x] Player Settings → WebGL Template = Swarm
[x] Build Profile의 Player Settings 오버라이드 제거 (⋮ → Remove)
[x] Graphics API를 WebGL 2.0 단독으로 고정
[ ] Quality: antiAliasing 0
[x] Initial Memory Size 256MB
[ ] Exception Support: None (릴리스) / Explicitly Thrown (개발)
[x] Run In Background Off (Splash Screen은 유지하기로 함)
[x] Compression Brotli + Decompression Fallback On (Cloudflare 이중 압축 때문)
[x] AkBuildPreprocessor에 WebGL 가드 (빌드 실패 원인)
[x] Build And Run으로 로컬 확인 — python http.server 아님
[x] StreamingAssets 사운드뱅크 6MB를 WebGL 빌드에서 제외 (치웠다 되돌리기)
[x] Cloudflare Pages용 _headers (템플릿 폴더 → 출력 루트 자동 복사)
[x] Cloudflare 이중 압축 확인 → Decompression Fallback으로 전환
[x] Decompression Fallback On으로 재빌드 후 재배포 — Cloudflare Workers에서 실행 확인
[x] 브라우저 콘솔 확인 — 에러 없음, 로더 정상 종료, WebGL2 컨텍스트 정상
[ ] 적 400마리 구간 프레임 확인 (Show Diagnostics 오버레이)
[x] 한글 폰트 번들 + 44곳 참조 교체 + 런타임 생성 UI 3곳 통합
[x] 한글 렌더링 확인 — 타이틀 화면 라벨 전부 정상
[ ] 다른 브라우저(Chrome/Firefox/Safari) 1회씩 확인
[ ] 배포본 씬 목록에서 TestStage 제외
```

---

## 9. 되돌리기

Windows 빌드로 돌아갈 때 손댈 것은 없다. asmdef의 `"WebGL"` 제외와
`AudioDirector`의 `#if`는 Windows 빌드에 영향을 주지 않는다 —
`Build Profiles`에서 `Windows` 프로필로 Switch만 하면 된다.
