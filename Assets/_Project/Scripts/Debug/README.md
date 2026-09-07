# Debug — 테스트 전용

출시 전에 **이 폴더를 통째로 삭제**하면 됩니다. 삭제하기 전에도, 아래 스위치가 꺼져 있는 한
화면은 출시 빌드와 동일합니다.

## 개발용 UI 스위치

`DevUi.cs`가 개발/테스트용 UI를 한 곳에서 켜고 끕니다.

- **기본값은 꺼짐** — 에디터에서 그냥 Play 하면 실제 출시 화면과 똑같이 보입니다.
- 켜려면 Unity 상단 메뉴 **`Swarm ▸ 개발용 UI 표시`** 체크. (EditorPrefs 저장 =
  이 PC의 에디터에만 적용, 프로젝트 파일·빌드에는 영향 없음)
- 값은 **Play 시작 시점에 읽습니다.** 켜거나 끈 뒤에는 Play를 다시 시작하세요.
- Development Build에서는 항상 켜짐(성능 측정용), 릴리즈 빌드에서는 항상 꺼짐.

스위치의 영향을 받는 것:

| 대상 | 어디에 있나 | 꺼졌을 때 |
| --- | --- | --- |
| `TestStageButton` (테스트 스테이지) | `Title.unity` | 비활성화 |
| `ResetButton` (진행도 초기화) | `Title.unity` | 비활성화 |
| `+100 G` 버튼 | 런타임 생성 | 생성 안 됨 |
| `DebugLevelUpButton` (레벨업 강제) | `Game.unity` | 비활성화 |
| 좌상단 통계 오버레이 | 런타임 생성 | 생성 안 됨 |

타이틀의 두 버튼은 `TitleManager`의 `Dev Only Objects` 배열에 연결되어 있습니다. 개발용
버튼을 새로 만들면 이 배열에 넣기만 하면 됩니다.

스위치와 무관한 것: `DebugFastForward`(F1 10배속)와 `DebugPerfProbe`(F3~F6 측정)는 화면에
상시 표시되는 UI가 아니라 단축키라서 종전대로 에디터·개발 빌드에서 동작합니다.

## 파일

| 파일 | 하는 일 | 씬 참조 |
| --- | --- | --- |
| `DevUi.cs` | 개발용 UI 표시 여부 스위치 | 없음 |
| `DebugFastForward.cs` | F1을 누르는 동안 10배속 | `Game.unity`의 GameManager 오브젝트 |
| `DebugLevelUpButton.cs` | 레벨업 강제 버튼 | `Game.unity`의 DebugLevelUpButton 오브젝트 |
| `DebugEvolveWeaponsButton.cs` | 무기 강제 진화 버튼 | `TestStage.unity` |
| `DebugGoldButton.cs` | 타이틀에 `+100 G` 버튼 | 없음 — 런타임에 자기 UI 생성 |
| `DebugStatsOverlay.cs` | 좌상단에 시간/적 수/레벨/FPS 표시 | 없음 — 런타임에 자기 오브젝트 생성 |
| `DebugPerfProbe.cs` | F3/F4/F5 단발 측정, **F6 전자동 스윕**(150~700 각 3회) → 프레임·GC Alloc 집계 후 콘솔 한 줄 + 마크다운 리포트 파일 | 없음 — 런타임에 자기 오브젝트 생성 |

메뉴 항목은 `Assets/_Project/Scripts/Editor/DevUiMenu.cs`에 있습니다(에디터 전용).

## 삭제할 때

`DebugGoldButton`, `DebugStatsOverlay`, `DebugPerfProbe`는 씬이 참조하지 않으므로 파일만
지우면 흔적이 없습니다. `DebugFastForward` / `DebugLevelUpButton` / `DebugEvolveWeaponsButton`은
씬에 컴포넌트로 붙어 있으므로, 파일을 지우기 전에 **씬에서 해당 컴포넌트/오브젝트를 먼저
제거**하세요. 순서를 바꾸면 "missing script" 참조가 씬에 남습니다.

`DevUi.cs`는 `TitleManager`가 참조하므로 폴더를 지울 때는 `TitleManager`의 `Awake`와
`devOnlyObjects` 필드도 함께 정리하거나, `DevUi.cs`만 다른 폴더로 옮기세요.

## 릴리즈 빌드에서의 동작

- `DebugEvolveWeaponsButton` / `DebugGoldButton` / `DebugStatsOverlay` / `DebugPerfProbe` —
  `#if`로 컴파일 자체가 제외됩니다.
- `DebugFastForward` / `DebugLevelUpButton` — 빌드에 **포함되되** `DevUi`가 꺼져 있으면
  스스로 꺼집니다. 릴리즈 씬(`Game.unity`)이 이들을 참조하고 있어서, 컴파일에서 빼면
  "missing script"가 되기 때문입니다.

## 네임스페이스

폴더는 `Debug`지만 네임스페이스는 `Swarm.Game`으로 두었습니다.
`Swarm.Debug`로 바꾸면 내부에서 쓰는 `Debug.isDebugBuild` 등이
`UnityEngine.Debug`가 아니라 네임스페이스로 해석되어 컴파일이 깨집니다.
