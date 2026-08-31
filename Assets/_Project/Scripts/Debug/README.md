# Debug — 테스트 전용

출시 전에 **이 폴더를 통째로 삭제**하면 됩니다.

| 파일 | 하는 일 | 씬 참조 |
| --- | --- | --- |
| `DebugFastForward.cs` | F1을 누르는 동안 10배속 | `Game.unity`의 GameManager 오브젝트 |
| `DebugLevelUpButton.cs` | 레벨업 강제 버튼 | `Game.unity`의 DebugLevelUpButton 오브젝트 |
| `DebugEvolveWeaponsButton.cs` | 무기 강제 진화 버튼 | `TestStage.unity` |
| `DebugGoldButton.cs` | 타이틀에 `+100 G` 버튼 | 없음 — 런타임에 자기 UI 생성 |

## 삭제할 때

`DebugGoldButton`은 씬이 참조하지 않으므로 파일만 지우면 흔적이 없습니다.
나머지 셋은 씬에 컴포넌트로 붙어 있으므로, 파일을 지우기 전에
**씬에서 해당 컴포넌트/오브젝트를 먼저 제거**하세요. 순서를 바꾸면
"missing script" 참조가 씬에 남습니다.

## 릴리즈 빌드에서의 동작

- `DebugEvolveWeaponsButton` / `DebugGoldButton` — `#if`로 컴파일 자체가 제외됩니다.
  씬 참조가 없거나(골드) 빌드 대상이 아닌 씬에만 있어(진화) 안전합니다.
- `DebugFastForward` / `DebugLevelUpButton` — 빌드에 **포함되되** 에디터·개발 빌드가
  아니면 스스로 꺼집니다. 릴리즈 씬(`Game.unity`)이 이들을 참조하고 있어서,
  컴파일에서 빼면 "missing script"가 되기 때문입니다.

## 네임스페이스

폴더는 `Debug`지만 네임스페이스는 `Swarm.Game`으로 두었습니다.
`Swarm.Debug`로 바꾸면 내부에서 쓰는 `Debug.isDebugBuild` 등이
`UnityEngine.Debug`가 아니라 네임스페이스로 해석되어 컴파일이 깨집니다.
