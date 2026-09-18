# WebGL 빌드와 배포

Swarm의 [브라우저 데모](https://curly-mud-2f7b.qlghs8.workers.dev/)는 PC 키보드 플레이를 위한 **무음 WebGL 버전**이다. 게임 코드는 데스크톱과 공유하며, WebGL 플레이어에서는 Wwise 백엔드를 제외한다. 이 문서는 저장소의 빌드 구성, 플랫폼별 처리, 정적 파일 배포 방식과 현재 제약을 설명한다.

## 1. 빌드 구성

프로젝트는 Unity **6000.4.3f1**, URP **17.4.0**, Input System **1.19.0**을 사용한다. Web 빌드에는 해당 Unity 버전의 Web Build Support 모듈이 필요하다.

[Web 프로필](../Assets/Settings/Build%20Profiles/Web.asset)은 다음 구성을 사용한다.

- 대상: WebGL, Development Build 및 Script Debugging 꺼짐.
- 씬: [전역 씬 목록](../ProjectSettings/EditorBuildSettings.asset)의 `Title → Game`. `TestStage`는 목록에 남아 있지만 비활성화되어 빌드에서 제외된다.
- Player Settings: 프로필의 `m_PlayerSettingsYaml.m_Settings`가 비어 있어 [전역 설정](../ProjectSettings/ProjectSettings.asset)을 사용한다. 별도 오버라이드를 추가하면 해당 프로필의 값도 확인해야 한다.
- 출력 경로: 프로필에 고정되어 있지 않으며 빌드할 때 선택한다. 기존 로컬 Web 산출물은 `Build/WebGl/`에 있다.

### 저장소에 반영된 Player Settings

| 항목 | 설정 |
|---|---|
| WebGL Template | `PROJECT:Swarm` |
| Graphics API | 자동 선택 꺼짐, OpenGLES3(WebGL 2) |
| Compression Format | Brotli (`webGLCompressionFormat: 0`) |
| Decompression Fallback | 켜짐 |
| Data Caching | 켜짐 |
| Name Files As Hashes | 꺼짐 |
| Initial / Maximum Memory Size | 256 / 2048 MB |
| Memory Growth Mode | Geometric |
| Exception Support | Explicitly Thrown Exceptions Only |
| Debug Symbols / Show Diagnostics | 꺼짐 / 꺼짐 |
| Threads Support | 꺼짐 |
| Power Preference | High Performance |
| Run In Background | 꺼짐 |

압축·예외·메모리 모드의 직렬화 값은 [Unity의 enum 정의](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/PlayerSettingsWebGL.bindings.cs)에 대응한다. 메모리 값은 할당 정책이며 실제 사용량이나 브라우저에서 확보 가능한 메모리를 뜻하지 않는다.

[Quality Settings](../ProjectSettings/QualitySettings.asset)의 WebGL 기본 레벨은 `High`(인덱스 3), `antiAliasing`은 0이다. 연결된 [URP 에셋](../Assets/Settings/UniversalRP.asset)의 MSAA도 1×로 설정되어 있다. 이 값들만으로 웹에서의 프레임 성능을 보장하지는 않는다.

### 소스와 배포본의 관계

Web 프로필, Player Settings, 템플릿과 오디오 빌드 훅은 Git으로 관리한다. `Build/`, `Library/`, `UserSettings/`는 추적하지 않으므로 로컬 출력물과 에디터의 활성 타깃은 저장소 설정과 구분해야 한다.

공개 페이지는 `Build/WebGl.data.unityweb`, `WebGl.framework.js.unityweb`, `WebGl.wasm.unityweb`을 참조한다. 로컬 출력물도 같은 파일 구조와 Brotli 식별자를 갖지만, 파일명만으로 현재 소스·로컬 빌드·배포본이 같은 버전이라고 판단할 수는 없다. 저장소에는 배포본과 소스 커밋을 연결하는 릴리스 메타데이터가 없다.

## 2. WebGL에서 Wwise를 제외하는 구조

현재 Wwise 통합의 `Generated/`에는 Common·Windows·Mac 코드가 있고, `Plugins/`에는 Windows·Mac 바이너리가 있다. WebGL용 사운드 엔진은 포함되어 있지 않다. 게임의 오디오 요청은 [AudioDirector](../Assets/_Project/Scripts/Audio/AudioDirector.cs)를 통해 전달한다.

### 컴파일 경계

다음 다섯 어셈블리는 `excludePlatforms`에 `WebGL`을 지정한다.

| 어셈블리 | 역할 |
|---|---|
| `AK.Wwise.Unity.API` | 사운드 엔진 API |
| `AK.Wwise.Unity.API.WwiseTypes` | Wwise 타입 래퍼 |
| `AK.Wwise.Unity.MonoBehaviour` | 초기화·리스너 등 Unity 컴포넌트 |
| `AK.Wwise.Unity.Timeline` | Timeline 연동 |
| `Ak.Wwise.Api.WAAPI` | Wwise API·타입 어셈블리를 참조하는 WAAPI 코드 |

`Swarm.Runtime.asmdef`에는 Wwise 참조가 남아 있으나, 실제 Wwise 타입을 사용하는 구현은 `AudioDirector`의 조건부 컴파일로 분리한다.

| 백엔드 멤버 | WebGL 플레이어 | 에디터·데스크톱의 Wwise 경로 |
|---|---|---|
| `IsReady` | `false` | 엔진 초기화와 호스트 확인 |
| `TryPostEvent` | `false` | 뱅크 로드 후 이벤트 전달 |
| `TryApplyRtpc` | `false` | RTPC 적용 |
| `TryApplyState` | `false` | State 적용 |

무음 경로의 조건은 `UNITY_WEBGL && !UNITY_EDITOR`다. 공개 API와 상태 저장은 공통이고, WebGL의 `Flush()`는 준비 상태 검사에서 바로 반환한다. 에디터는 WebGL 타깃을 선택해도 Wwise 경로를 사용하므로, Play 모드의 사운드는 웹 플레이어의 동작과 다르다. 볼륨 UI와 설정 저장은 웹에서도 남지만 재생되는 소리는 없다.

### 빌드 전후 처리

[AkBuildPreprocessor](../Assets/Wwise/MonoBehaviour/Editor/WwiseSetupWizard/AkBuildPreprocessor.cs)는 WebGL 대상에서 전처리와 후처리를 즉시 반환한다. WebGL용 뱅크 복사, ProjectDatabase 초기화, 플러그인 배포 활성화를 진행하지 않는다.

별도의 [AudioPipeline.BuildHook](../Assets/_Project/Scripts/Editor/AudioPipeline.cs)은 콜백 순서 `-100`으로 사운드뱅크가 웹 산출물에 포함되지 않도록 처리한다.

1. WebGL 빌드 전 `Assets/StreamingAssets/Audio/GeneratedSoundBanks` 폴더와 해당 `.meta`를 `Library/SwarmWebGLAudioStash`로 이동한다.
2. WebGL 빌드 후 원래 위치로 복원한다.
3. 빌드 중단으로 임시 폴더가 남으면, 다음 에디터 코드 로드 시 지연 콜백으로 복원을 시도한다. 중단 직후의 즉시 복원을 보장하는 구조는 아니다.

다른 타깃에서는 기존 뱅크 복사와 `WwiseIds` 상수 재생성을 수행한다. 오디오 구조의 자세한 설명은 [Wwise 파이프라인](wwise-pipeline.md)을 참고한다.

Title 씬에는 `AkInitializer`, `AkAudioListener`, `AkGameObj` 참조가 남아 있다. WebGL 빌드 시 이 컴포넌트를 별도로 제거하는 씬 처리기는 없다. 해당 스크립트가 제외되므로 빌드·실행 환경에 따라 누락 스크립트 경고가 발생할 수 있다.

Wwise 제공 파일인 다섯 asmdef와 `AkBuildPreprocessor`에는 프로젝트용 변경이 들어 있다. Wwise 통합을 갱신할 때는 WebGL 제외 설정과 빌드 가드가 유지되는지 확인해야 한다.

## 3. 입력·저장·한글 UI

**입력:** `PlayerController`는 `PlayerInputActions`의 WASD·방향키 바인딩을 읽고 `FixedUpdate()`에서 Rigidbody2D 속도에 적용한다. 가상 조이스틱 입력 경로도 있지만 웹 템플릿은 모바일 안내 화면을 표시한다. 현재 이동 액션에는 게임패드 바인딩이 없다. UI 선택에는 마우스를 사용하며, 궁수 구르기는 Space, 설정 메뉴 토글은 Esc를 읽는다. 템플릿의 “나머지는 자동” 문구는 이 추가 조작을 모두 설명하지 않는다.

**저장:** 볼륨 변경은 이미 `PlayerPrefs.Save()`를 호출한다. `GoldWallet`은 골드 변경을 캐시하고 포커스 상실·씬 언로드·종료 이벤트 및 구매 시 저장한다. 브라우저 종료나 저장소 제한 상황까지 저장 완료를 보장하는 별도 웹 처리는 없다. 마지막 저장 이후 진행은 유실될 수 있다.

**한글:** [Galmuri11-Swarm.ttf](../Assets/_Project/Resources/Galmuri11-Swarm.ttf)를 번들에 포함하고 씬·프리팹의 Text에서 참조한다. 코드로 생성하는 설정 UI·버프 카운트·개발용 골드 버튼은 [UiFont.Current](../Assets/_Project/Scripts/UI/UiFont.cs)를 사용한다. 폰트 리소스 로드에 실패하면 내장 폰트로 대체하므로 이 경로에서는 한글 표시를 보장하지 않는다. 라이선스는 [SIL OFL 원문](../Assets/_Project/Fonts/Galmuri-OFL.txt)에 포함되어 있다.

## 4. WebGL 템플릿

[Swarm 템플릿](../Assets/WebGLTemplates/Swarm/index.html)은 별도 [CSS](../Assets/WebGLTemplates/Swarm/TemplateData/style.css)와 함께 다음 기능을 제공한다.

- 16:9 화면 비율을 유지하는 레터박스 배치.
- 로딩 진행률과 진행 바, 초기화 완료 후 로딩 화면 페이드아웃.
- 초기화 완료·페이지 클릭 시 캔버스 포커스 지정.
- 방향키와 Space의 기본 스크롤 동작 방지.
- Android·iPhone·iPad 등의 **User-Agent 문자열**이 일치하면 로더를 요청하지 않고 PC 브라우저 안내 표시. 모든 터치 기기를 감지하는 방식은 아니다.
- 로더 다운로드 실패, Unity 초기화 실패 및 오류 배너 표시.

템플릿은 `Build/`의 파일을 상대 경로로 읽는다. 로더 실패 안내에 압축 설정이 언급되지만, 그 메시지만으로 실패 원인이 압축이라고 확정할 수는 없다.

## 5. 압축과 배포

현재 구성은 **Brotli + Decompression Fallback**이다. Unity 로더가 압축 해제 코드를 포함하며, 압축 자산은 `.unityweb` 확장자로 출력된다. 이는 서버의 네이티브 압축 해제 헤더에 대한 의존을 줄이지만 로더 크기와 압축 해제 비용이 추가되고 WebAssembly 스트리밍 컴파일에 제약이 있다. [Unity Web 배포 문서](https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-deploying.html)

Fallback을 끄는 구성에서는 Brotli 파일에 `Content-Encoding: br`, Gzip 파일에 `Content-Encoding: gzip`이 필요하며, WebAssembly 스트리밍에는 `Content-Type: application/wasm`이 필요하다. 헤더는 실제 응답 본문의 인코딩과 맞아야 한다. `Content-Length`의 유무만으로 재압축이나 이중 압축을 판별할 수는 없다.

템플릿의 [_headers](../Assets/WebGLTemplates/Swarm/_headers)는 현재 **주석만 포함**하며 적용되는 헤더 규칙이 없다. 주석의 과거 문제 설명을 Cloudflare 전체의 동작 규칙으로 일반화하지 않는다. Cloudflare Workers 정적 자산의 사용자 정의 헤더는 업로드 루트의 `_headers`에서 설정한다. [Cloudflare 헤더 문서](https://developers.cloudflare.com/workers/static-assets/headers/)

### 수동 빌드·배포 절차

저장소에는 WebGL 전체 빌드를 실행하는 전용 스크립트, Wrangler 설정 또는 자동 배포 워크플로가 없다. Unity 빌드와 Cloudflare Workers 정적 파일 업로드를 별도로 수행하는 구성이다.

1. Unity Build Profiles에서 `Web` 프로필을 선택하고 Web Build Support 모듈, 씬 목록과 Player Settings를 확인한다.
2. Development Build가 꺼진 상태에서 출력 폴더를 선택해 빌드한다. 기존 경로를 따를 경우 `Build/WebGl/`을 사용한다.
3. Build And Run 또는 HTTP 서버로 출력물을 열어 로딩·입력·UI·씬 전환을 확인한다. HTML 파일을 직접 여는 방식은 사용하지 않는다.
4. Cloudflare Workers의 정적 파일 업로드에 **`index.html`이 있는 출력 루트 전체**를 올린다. 프로젝트의 `Build/` 전체나 출력물 안쪽의 `Build/`만 올리는 것이 아니다.
5. 공개 URL에서 HTML·로더·data·wasm·framework 파일이 함께 갱신되었는지 확인한다. 파일명 해시가 꺼져 있으므로 같은 이름의 이전 자산이 캐시에 남는 경우도 구분한다.

기존 로컬 출력 구조는 다음과 같다. 출력 폴더 이름을 바꾸면 빌드 파일 이름도 달라질 수 있다.

```text
Build/WebGl/
├── index.html
├── _headers
├── TemplateData/style.css
├── StreamingAssets/desc.txt
└── Build/
    ├── WebGl.loader.js
    ├── WebGl.data.unityweb
    ├── WebGl.framework.js.unityweb
    └── WebGl.wasm.unityweb
```

## 6. 현재 제약과 후속 과제

- **사운드:** 웹은 무음이다. WebGL용 Wwise 통합이나 다른 오디오 백엔드는 구현되어 있지 않다. 사운드 지원을 추가하려면 엔진 통합, 뱅크 로딩, 브라우저 오디오 시작 정책을 함께 설계해야 한다.
- **지원 환경:** PC 키보드 플레이를 기준으로 한다. 모바일·게임패드 지원 및 브라우저별 호환성 범위를 확정한 테스트 결과는 제공하지 않는다.
- **성능:** 웹의 적 밀집 구간 프레임·메모리와 브라우저별 로딩 성능은 후속 측정 대상이다. Windows Development Build의 결과를 WebGL 수치로 사용하지 않는다.
- **개발용 측정:** `DebugPerfProbe`는 에디터 또는 Development Build에서만 포함된다. 보고서는 `Application.persistentDataPath` 아래에 기록하며 저장 실패를 예외 처리한다. 브라우저 다운로드 기능은 없으므로 웹에서 파일을 사용자에게 전달하는 측정 경로는 별도로 필요하다.
- **배포 추적:** 공개 데모에 대응하는 소스 커밋·빌드 설정·산출물 식별자를 남기는 절차는 후속 과제다. 현재 소스에 반영된 기능이 기존 공개 데모에도 반영되었다고 가정하지 않는다.
