# Swarm

**적 무리를 밀어내며 성장하고, 10분 뒤 등장하는 보스를 처치하는 로그라이크 서바이버**

Unity 6000.4.3f1 · URP · C# · 1인 개발

**[브라우저에서 플레이하기 →](https://curly-mud-2f7b.qlghs8.workers.dev/)** · WebGL 데모 / 키보드 플레이 / 무음

캐릭터별 자동 공격과 레벨업 카드 선택으로 빌드를 구성합니다. 기본 무기 9종은 각각 레벨 8에서 진화하며, 넉백으로 무리를 밀어내는 전투와 슬램·운석 2패턴 보스전을 구현했습니다.

| 일반 전투 | 보스전 |
| --- | --- |
| ![일반 전투와 무기 효과](docs/media/swarm-combat.png) | ![보스전과 운석 패턴](docs/media/swarm-boss.png) |
| **레벨업 카드 선택** | **영구 강화 상점** |
| ![레벨업 카드 선택 화면](docs/media/swarm-levelup.png) | ![영구 강화 상점 화면](docs/media/swarm-upgrades.png) |

| 항목 | 내용 |
| --- | --- |
| 개발 기간 | 2026.07 ~ 현재 |
| 담당 | 요구사항·게임 규칙 결정, 기능 통합, 플레이 테스트 및 수정 판단 |
| 콘텐츠 | 캐릭터 3종 · 기본 무기 9종 + 진화 무기 9종 · 시간 기반 웨이브 · 보스전 |
| 주요 기술 | 전투 판정 버퍼 재사용 · 공통 무기 레벨링 · 이펙트 제작·연동 도구 |
| 기술 환경 | Unity 6000.4.3f1 · C# · URP · Input System · Wwise |
| 플랫폼 | Android 타깃 · Windows Development Build 성능 측정 · WebGL 데모 배포 |

## 제작 방식과 기여

코드 생성에는 **Claude Code**를 활용했습니다. 제작 범위와 순서, 요구사항·게임 규칙을 정하고, 생성된 기능을 통합해 직접 플레이하면서 채택하거나 수정했습니다. 아트는 생성형 AI와 Aseprite·MCP를 활용해 제작·통합했습니다([아트 제작 과정](docs/art-plan.md)).

플레이 검증에서는 기능의 작동 여부와 전투 감각을 함께 확인했습니다. 빠른 적은 상시 등장할 때의 불쾌감을 줄이기 위해 웨이브 전용으로 바꿨고, 보스는 넉백·스턴만 제한해 공격 패턴과 상태이상 빌드가 함께 유효하도록 조정했습니다.

## 전투 판정의 반복 할당 줄이기

초기 범위 판정은 `OverlapCircleAll`로 배열을 만들고 태그로 대상을 걸렀습니다. 반복되는 탐색과 생성 비용을 줄이기 위해 결과 버퍼와 오브젝트를 재사용하는 경로로 정리했습니다.

- **탐색:** `ContactFilter2D`로 적 레이어를 필터링합니다. 단일 타깃 탐색은 내부 버퍼를 소비한 뒤 대상을 반환하고, 다중 타깃은 호출자 소유 리스트에 결과를 전달해 데미지 처리 중 다른 탐색이 발생해도 결과를 유지합니다.
- **생성:** 적·투사체는 `ObjectPool`, 여러 생성자가 공유하는 픽업·데미지 숫자는 `SharedObjectPool`, 코드로 조립하는 이펙트는 `RuntimeObjectPool`로 재사용합니다.
- **밀도 관리:** 멀어진 적을 재배치하고 일반 스폰 상한을 적용합니다. 웨이브는 상한을 넘어 생성될 수 있으므로 400마리를 모든 상황의 최대 개체 수로 보지는 않습니다.

버퍼 용량 확장과 풀의 초기 생성에는 할당이 발생할 수 있습니다. 성능은 아래의 데스크톱 측정 조건에서 확인했습니다.

코드: [EnemyTargeting](Assets/_Project/Scripts/Weapon/EnemyTargeting.cs) · [ChasingBlade](Assets/_Project/Scripts/Weapon/ChasingBlade.cs) · [ObjectPool](Assets/_Project/Scripts/Weapon/ObjectPool.cs) · [EnemySpawner](Assets/_Project/Scripts/Spawner/EnemySpawner.cs)

### 측정 결과

**2026-09-06:** Windows 11 / Ryzen 5 5600 + RTX 4060 Ti / Mono / Development Build / 1920×1080 / Ultra / vSync off.
구간별 안정화 3초 후 10초씩 측정한 **3회 평균값의 중앙값**입니다. 측정 중 무기 구성을 고정하고 디버그 오버레이를 껐습니다.

| 실측 적 수 | 평균 프레임 시간 | 평균 GC Alloc/frame | GC 수집 |
| --- | ---: | ---: | ---: |
| 149 | 0.84 ms | 1 B | 0회 |
| **399** | **1.41 ms** | **3 B** | **0회** |
| 699 | 2.88 ms | 6 B | 0회 |

목표 400마리 구간의 회차별 값은 **1.41 / 1.40 / 1.71 ms**, GC는 **3 / 3 / 4 B/frame**입니다. Android·보스/다중 장판 복합 조건은 미측정이며, 경험치 구슬 수는 통제하지 않았습니다. 이 표는 WebGL 성능 측정값이 아니며 평균값과 최악 프레임은 구분합니다.

계측 과정에서는 스폰 보충의 `MethodInfo.Invoke` 박싱과 디버그 오버레이 할당을 측정 경로에서 제거했습니다. 더 높은 개체 수의 결과, 회차 기록과 검증 범위는 [성능 문서](docs/performance-profiling.md)에 정리했습니다.

[Profiler 캡처](docs/media/img-02-profiler.png) · [DebugPerfProbe 코드](Assets/_Project/Scripts/Debug/DebugPerfProbe.cs)

## 성장 규칙과 제작 흐름

### 1. 기본·진화 무기가 공유하는 레벨링 계약

`LevelableWeapon`에 레벨, 카드 표시 정보, 사용 가능 여부(`IsAvailable`), 진화 대상 참조를 두었습니다. `LevelUpManager`는 `ILevelableWeapon`을 조회하고 최대 레벨에 도달한 무기의 진화 대상을 카드 후보에 올립니다. 기본 9종과 진화 9종이 같은 선택 흐름을 사용합니다.

**카드 후보 확인 → 진화 선택 → 기존 무기 비활성화 → 진화 무기 활성화 → 공통 경로로 다음 레벨업**

```csharp
// LevelUpManager의 진화 카드 선택 처리 발췌
leveledWeapon.Retire();
evolution.SetAvailable(true);
evolution.SetStartingLevel(1);
```

대시와 힐도 같은 레벨링 계약을 사용합니다. 무기마다 카드 표시와 레벨 증가 로직을 별도로 만드는 부담을 줄였습니다.

코드: [LevelUpManager](Assets/_Project/Scripts/LevelUp/LevelUpManager.cs) · [LevelableWeapon](Assets/_Project/Scripts/Weapon/LevelableWeapon.cs) · [CharacterDefinition](Assets/_Project/Scripts/Game/CharacterDefinition.cs)

### 2. 보스 패턴과 상태이상 빌드의 양립

연속 타격마다 보스가 밀리거나 기절하면 공격 준비 동작을 마치지 못했습니다. 넉백·스턴만 면역으로 바꾸고 둔화·지속 피해를 남겨, 보스 패턴을 유지하면서 상태이상 무기를 선택한 의미도 살렸습니다. 슬램·운석의 시전 중첩은 `BossPatternGate`로 제어합니다.

코드: [EnemyChaser](Assets/_Project/Scripts/Enemy/EnemyChaser.cs) · [EnemyStatusImmunity](Assets/_Project/Scripts/Enemy/EnemyStatusImmunity.cs) · [BossSlamAttack](Assets/_Project/Scripts/Enemy/BossSlamAttack.cs) · [BossPatternGate](Assets/_Project/Scripts/Enemy/BossPatternGate.cs)

### 3. 이펙트 파일을 보스 공격에 연결하는 제작 도구

아트 제작 후 Unity에서 실제 공격 효과로 연결되는 과정까지 다뤘습니다. `BossSlamSetup`은 `bossslam.aseprite`의 3프레임을 읽어 충격 이펙트 프리팹을 만들고, 보스의 슬램 공격에 연결합니다.

원본을 다시 제작해 스프라이트 참조가 바뀐 경우에도 프레임과 연결 상태를 검사해 갱신합니다. 프레임뿐 아니라 크기·불투명도도 확인해, 이미지 교체 이후 반복되는 설정 작업을 줄였습니다.

코드·에셋: [BossSlamSetup](Assets/_Project/Scripts/Editor/BossSlamSetup.cs) · [Effect_BossSlam_Impact](Assets/_Project/Prefabs/Effect_BossSlam_Impact.prefab) · [Enemy_Boss](Assets/_Project/Prefabs/Enemy_Boss.prefab)

## 플레이와 로컬 실행

적 처치 → 경험치 수집 → 강화 카드 선택 → 무기 진화 → 10분 보스전으로 이어집니다. 런에서 얻은 골드는 캐릭터 해금과 영구 강화에 사용합니다.

| 조작 | 입력 |
| --- | --- |
| 이동 | WASD / 방향키 / 화면 조이스틱 |
| 공격 | 자동 |
| 궁수 대시 | 스킬 획득 후 Space / 화면 대시 버튼 |
| 카드 선택 | 화면의 카드 클릭·터치 |

**[WebGL 데모 실행](https://curly-mud-2f7b.qlghs8.workers.dev/)** — 웹 버전은 무음으로 동작합니다.

소스 실행은 Unity Hub에서 저장소 루트를 Unity **6000.4.3f1**로 열고, 패키지 임포트 후 `Assets/_Project/Scenes/Title.unity`에서 Play를 실행합니다. 오디오 통합은 Wwise **2025.1.10** 기준이며 Windows용 `Init.bnk`와 `Main.bnk`가 포함되어 있습니다. 뱅크 수정·재생성에는 `Swarm_WwiseProject/Swarm_WwiseProject.wproj`를 사용합니다.

## 상세 문서

| 주제 | 문서 |
| --- | --- |
| 게임 규칙·무기·웨이브 | [게임 디자인](docs/game-design.md) |
| 측정 조건·회차 기록·계측 도구 | [성능 측정](docs/performance-profiling.md) |
| 독 장판 소유권·풀·군중 물리·UI·트러블슈팅 | [상세 설계](docs/technical-notes.md) |
| 사운드 이벤트 라우팅 | [Wwise 파이프라인](docs/wwise-pipeline.md) |
| 아트 제작 과정 | [아트 문서](docs/art-plan.md) |
| WebGL 설정·배포 기록 | [빌드 문서](docs/webgl-build.md) |
