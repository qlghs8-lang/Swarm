# Swarm 상세 설계

[README](../README.md)의 핵심 구현을 보충하는 설계 문서입니다. 상태 효과의 소유권, 풀링과 물리 좌표 동기화, 군중·보스 처리 등에서 선택한 구조와 제약을 설명합니다.

각 절에는 관련 코드와 주요 변경 커밋을 연결했습니다. 성능 수치와 측정 조건은 [성능 측정](performance-profiling.md), 아트 연결은 [아트 문서](art-plan.md), 웹 배포는 [WebGL 문서](webgl-build.md)에서 다룹니다.

## 독 장판의 상태 소유권

### 신호 전달과 틱 처리

겹친 독 장판이 각각 피해 타이머를 진행하지 않도록, 장판은 중독 신호를 보내고 적이 상태와 틱 타이머 하나를 관리합니다. 효과 값은 최근 호출을 기준으로 갱신합니다.

1. `PoisonGasCloud.Update()`가 범위 내 적에게 `ApplyPoison()`을 호출합니다.
2. `EnemyHealth`가 신호와 피해·디버프 값을 저장하고 `RefreshPoisonDebuffs()`를 호출합니다. 이미 중독 중이면 틱 타이머를 다시 초기화하지 않습니다.
3. `EnemyHealth.Update()`가 신호를 소비하며 틱 타이머를 한 번 진행합니다. 만료 시 피해를 한 번 적용하고 타이머를 틱 간격으로 되돌립니다.
4. 신호가 없는 갱신에서는 중독 상태와 독의 방어 감소·이동속도 배율을 해제합니다.

이는 장판 안에서 신호가 계속 들어오는 동안 적용하는 규칙입니다. 피해 값·틱 간격·디버프 값은 **가장 최근 `ApplyPoison()` 호출 값**으로 덮어씁니다. 디버프 세터는 직전에 적용한 값과 `Mathf.Approximately`로 비교해 달라졌을 때만 호출합니다. 서로 다른 값의 장판이 번갈아 보고하면 같은 프레임에도 여러 번 갱신될 수 있습니다.

### 변경 이력

[8ecf042](https://github.com/qlghs8-lang/Swarm/commit/8ecf042e225ed635967c98cf950d651899013844) 이전에는 중독 중 새 신호가 들어와도 피해 필드만 바뀌고 방어 감소·둔화는 첫 진입 값에 남았습니다. 이 커밋에서 디버프 갱신을 중독 여부에 따른 조기 반환 앞으로 옮겼습니다. 이후 피해와 디버프가 모두 최근 호출 값을 따릅니다.

### 중첩과 재사용 시 고려사항

- 피해 타이머는 적이 공유하지만 범위 탐색은 장판마다 수행하므로, 탐색 비용은 장판 수에 따라 늘어납니다.
- 신호 전달과 소비는 서로 다른 `Update()`에서 수행하며 실행 순서를 별도로 지정하지 않았습니다. 피해 틱은 소비 시점까지 전달된 값을 사용합니다. 진입·이탈·중첩 경계에서는 이 순서에 따른 동작 확인이 필요합니다.
- 화상은 다른 규칙입니다. `FirePatch`는 자신의 `_ignited` 집합에 처음 추가된 `EnemyHealth`에만 `ApplyBurn()`을 호출합니다. 같은 장판에 머문다고 매 프레임 지속시간을 갱신하지 않습니다. 다른 장판이 재점화하면 피해 값을 덮어쓰고 틱 타이머를 초기화하며, 남은 지속시간은 기존 값과 새 값의 최댓값을 사용합니다. 장판 밖에서도 남은 시간 동안 화상이 진행됩니다.
- `_ignited`는 장판 재사용 시 `Configure()`에서 비워집니다. 동일한 장판이 살아 있는 동안 적 인스턴스가 풀에서 재사용되면 기존 참조 때문에 점화가 생략될 수 있습니다. 풀 재사용 경계에서 확인할 잠재적 제약입니다.

코드: [PoisonGasCloud](../Assets/_Project/Scripts/Weapon/PoisonGasCloud.cs) · [EnemyHealth](../Assets/_Project/Scripts/Enemy/EnemyHealth.cs) · [FirePatch](../Assets/_Project/Scripts/Weapon/FirePatch.cs)

## 진화 전후 액티브 스킬 UI

`IActiveRoll`은 레벨·쿨다운 표시 값과 `TryRoll()`을 공통 계약으로 제공합니다. `RollCooldownUI`는 시작 시 기본 대시와 맹독 대시 컴포넌트를 찾고, 레벨이 1 이상인 맹독 대시를 우선 선택합니다. 화면 버튼의 요청은 선택한 구현체의 `TryRoll()`로 전달합니다. Space 키 입력은 각 무기의 `Update()`가 별도로 처리합니다.

UI는 두 구현체를 직접 참조하므로 새 액티브 스킬을 추가할 때 선택 로직도 확장해야 합니다. 쿨다운 문자열은 `ToString()`으로 갱신하며, 해당 경로는 GC 할당 확인 대상입니다.

코드: [IActiveRoll](../Assets/_Project/Scripts/Weapon/IActiveRoll.cs) · [RollCooldownUI](../Assets/_Project/Scripts/UI/RollCooldownUI.cs) · [ArcherRollWeapon](../Assets/_Project/Scripts/Weapon/ArcherRollWeapon.cs) · [ArcherToxicRollWeapon](../Assets/_Project/Scripts/Weapon/ArcherToxicRollWeapon.cs)

## 오브젝트 풀의 역할 분리

| 클래스 | 사용 상황 | 현재 구조 |
| --- | --- | --- |
| `ObjectPool` | 무기·스포너가 자기 프리팹을 생성 | 인스턴스별 큐와 중복 반납 방지 집합 |
| `SharedObjectPool` | 여러 생성자가 같은 픽업·데미지 숫자를 생성 | 프리팹을 키로 `ObjectPool` 공유, 씬 로드 시 공유 사전 초기화 |
| `RuntimeObjectPool` | 코드로 조립하는 번개·운석·장판 등을 생성 | 팩토리와 스택, 중복 반납 방지 집합, 꺼낼 때 파괴된 참조 건너뛰기 |

초기 생성과 용량 확장에는 할당이 발생할 수 있습니다. 재사용 시 체력·경과 시간·타격 상태 등의 초기화는 호출부와 각 컴포넌트가 담당합니다. 예를 들어 `EnemySpawner`는 풀에서 꺼낸 적에 난이도를 적용하고 `ResetHealth()`를 호출하며, 장판은 `Configure()`에서 경과 시간을 초기화합니다. 보스 본체와 보스 운석의 낙하·착탄 이펙트는 `Instantiate`로 생성합니다.

### 스폰 위치 반영과 변경 이력

[Physics2D 설정](../ProjectSettings/Physics2DSettings.asset)의 `m_AutoSyncTransforms`는 0입니다. 현재 `ObjectPool.Get()`은 위치·활성 상태를 설정한 뒤 같은 오브젝트의 `Rigidbody2D.position`과 `rotation`에도 좌표를 반영합니다. 같은 오브젝트에 Rigidbody2D 없이 Collider2D만 있으면 `Physics2D.SyncTransforms()`를 호출합니다.

[7e5104d](https://github.com/qlghs8-lang/Swarm/commit/7e5104da2272cd8d58676b037a6a63742af174ae)에서 스폰마다 전체 동기화를 호출하던 코드를 이 분기로 바꿨습니다. 물리 탐색이 이전 위치를 읽지 않도록 좌표 반영 경로를 유지하면서 전체 동기화 호출을 줄인 변경입니다. 이 변경만을 분리한 전후 성능 수치는 없습니다.

코드: [ObjectPool](../Assets/_Project/Scripts/Weapon/ObjectPool.cs) · [SharedObjectPool](../Assets/_Project/Scripts/Weapon/SharedObjectPool.cs) · [RuntimeObjectPool](../Assets/_Project/Scripts/Weapon/RuntimeObjectPool.cs) · [BossMeteorAttack](../Assets/_Project/Scripts/Enemy/BossMeteorAttack.cs)

## 군중 물리와 보스 패턴

플레이어의 물리 바디와 피해 판정용 `PlayerHurtbox`를 분리했습니다. Hurtbox는 런타임에 트리거 콜라이더를 만들며, 반경이 물리 바디 안에 묻히지 않도록 바디 반경 + 0.03 이상으로 보정합니다. Hurtbox 크기는 이 최소 반경 제약 안에서 조정합니다.

적은 `FixedUpdate()`에서 현재 속도를 목표 추적 속도로 점진적으로 바꾸고, 넉백 타이머가 남아 있으면 추적 조향을 건너뜁니다. 스턴은 기존 시간과 새 시간의 최댓값을 사용합니다. `ApplySlow()`의 시간제 둔화는 더 작은 이동속도 배율과 더 긴 남은 시간을 택하므로, 약한 둔화가 나중에 들어와도 강한 배율의 지속시간이 늘어날 수 있습니다. 독이 설정하는 배율은 별도 필드이며 시간제 둔화와 곱해집니다.

[보스 프리팹](../Assets/_Project/Prefabs/Enemy_Boss.prefab)의 `EnemyStatusImmunity`는 넉백·스턴 면역을 켜고 시간제 둔화 면역은 끕니다. 독·화상·방어 감소를 차단하는 경로도 없습니다. 이 설정으로 보스의 이동·시전 방해를 제한하면서 지속 피해와 방어 감소를 사용하는 빌드를 유지합니다.

슬램과 운석은 각자 타이머를 가지며 발동 전 `BossPatternGate`에서 다른 `IBossPattern.IsCasting`을 확인합니다. 막힌 패턴은 타이머를 초기화하지 않고 다음 갱신에서 다시 확인합니다. 운석은 마지막 낙하 판정이 끝날 때까지 시전 상태를 유지합니다. 슬램은 `HoldMovement()`로 이동을 정지시키며, 이때 같은 레이어와의 충돌을 제외하고 해제 시 기존 마스크를 복원합니다.

게이트의 제어 범위는 패턴의 시전 상태입니다. 시전 후 남는 시각 효과는 별도 수명을 따릅니다. 다수 적·장판·보스 패턴이 겹치는 조건의 성능은 [후속 측정 대상](performance-profiling.md)입니다.

코드: [PlayerHurtbox](../Assets/_Project/Scripts/Player/PlayerHurtbox.cs) · [EnemyChaser](../Assets/_Project/Scripts/Enemy/EnemyChaser.cs) · [EnemyStatusImmunity](../Assets/_Project/Scripts/Enemy/EnemyStatusImmunity.cs) · [BossPatternGate](../Assets/_Project/Scripts/Enemy/BossPatternGate.cs) · [BossSlamAttack](../Assets/_Project/Scripts/Enemy/BossSlamAttack.cs)

## 데미지 계산과 표시

무기가 레벨·능력치·크리티컬 배율을 반영한 피해량을 전달하고, `EnemyHealth`가 피해 유형에 맞는 방어력에서 임시 방어 감소와 관통력을 뺍니다. 그 값을 0~0.9로 제한한 뒤 `RoundToInt(피해량 × (1 - 방어 비율))`을 적용합니다. `isCritical`은 적 쪽에서 피해를 다시 배가하지 않고 `DamageNumber`의 색·크기·느낌표 표시에 사용합니다.

크리티컬 추첨 단위는 무기 구조에 따라 나뉩니다.

- 일반적인 발사·범위 공격은 대상 처리 전에 한 번 뽑은 값과 플래그를 해당 발사 묶음이나 범위의 대상에게 전달합니다.
- `WarriorOrbitWeapon`과 `WarriorSwordChasingWeapon`은 매 `Update()`에서 한 번 뽑아 모든 칼날의 전투 값을 갱신합니다. 칼날은 해당 갱신에서 전달받은 값을 타격에 사용합니다.
- 독·화상과 전사의 추가 피해 발동(흡혈·레이지 프록)은 크리티컬 추첨 없이 `BaseDamageMultiplier`를 사용합니다. 능력치·레벨 배율은 반영하되 크리티컬 배율은 적용하지 않습니다.

**변경 이력:** [41ae3f6](https://github.com/qlghs8-lang/Swarm/commit/41ae3f607f6c4576c002d175774699bb2f309cb3)에서 기본 배율과 크리티컬 추첨 경로를 함께 도입했습니다. 추가 피해는 `BaseDamageMultiplier`를 사용해 원래 공격의 `LastAttackWasCritical`을 유지합니다.

코드: [PlayerStats](../Assets/_Project/Scripts/Player/PlayerStats.cs) · [EnemyHealth](../Assets/_Project/Scripts/Enemy/EnemyHealth.cs) · [DamageNumber](../Assets/_Project/Scripts/UI/DamageNumber.cs) · [WarriorOrbitWeapon](../Assets/_Project/Scripts/Weapon/WarriorOrbitWeapon.cs) · [WarriorSwordChasingWeapon](../Assets/_Project/Scripts/Weapon/WarriorSwordChasingWeapon.cs) · [WarriorRageWeapon](../Assets/_Project/Scripts/Weapon/WarriorRageWeapon.cs)

## 화면 밖 적의 재배치

`EnemySpawner`는 추적 목록을 주기적으로 정리하고 멀어진 적을 같은 인스턴스인 채 플레이어 주변으로 옮깁니다. 현재 `Game`·`TestStage` 씬의 설정은 검사 간격 0.5초, 재배치 거리 22, 스폰 반경 8, 일반 스폰 상한 400입니다. 이동 방향에 따른 스폰 편향과 아레나 경계 보정도 적용합니다.

재배치 시 현재 체력과 생성 때 적용한 난이도·경험치 배율을 그대로 유지합니다. 풀 반납 후 재스폰과 구분되는 동작입니다. 일반 스폰은 목록 수를 기준으로 제한하지만, 웨이브와 `DebugPerfProbe` 보충 스폰은 상한을 우회합니다. 보스와 별도로 만든 훈련 더미는 `ActiveEnemyCount`에 포함되지 않으며, 죽은 적도 다음 목록 정리 전까지 남을 수 있습니다.

재배치 기준은 플레이어와의 거리입니다. 해상도·카메라 범위·벽 근처 조건을 바꿀 때는 재배치가 화면에 노출되는지 확인해야 합니다.

코드·설정: [EnemySpawner](../Assets/_Project/Scripts/Spawner/EnemySpawner.cs) · [DebugPerfProbe](../Assets/_Project/Scripts/Debug/DebugPerfProbe.cs) · [Game 씬](../Assets/_Project/Scenes/Game.unity) · [TestStage 씬](../Assets/_Project/Scenes/TestStage.unity)

## 스프라이트 정렬

프로젝트 역할별 레이어는 `Ground → Decal → Prop → Wall → Pickup → Character → Effect → Overlay`의 8개이며, 기본 `Default`까지 설정에는 9개가 있습니다. 플레이어와 적·보스는 같은 `Character` 레이어와 `sortingOrder = 0`을 사용합니다. GraphicsSettings와 URP의 `Renderer2D`는 커스텀 정렬 축 `(0, 1, 0)`으로 설정되어 있습니다.

[변경 이력 f4450e8](https://github.com/qlghs8-lang/Swarm/commit/f4450e856b32548fdcc2c7b50361d40265037729)에서 레이어 상수와 Y축 정렬 설정을 도입했습니다. 플레이어만 높은 레이어에 두면 위치와 무관하게 적 앞에 그려질 수 있으므로, 같은 레이어에서 깊이를 정렬하려는 구성입니다. 같은 레이어에서도 `sortingOrder`가 다르면 그 값이 우선하므로, 별도 순서가 필요한 이펙트 등은 예외로 관리합니다.

코드·설정: [SortingLayers](../Assets/_Project/Scripts/Rendering/SortingLayers.cs) · [TagManager](../ProjectSettings/TagManager.asset) · [GraphicsSettings](../ProjectSettings/GraphicsSettings.asset) · [Renderer2D](../Assets/Settings/Renderer2D.asset)

## 타격 이력의 수명 관리

`OrbitingBlade`와 `ChasingBlade`는 `Dictionary<Collider2D, float>`에 대상별 마지막 근접 타격 시각을 기록합니다. 정리 함수는 이전 정리로부터 2초가 지났을 때, 파괴된 대상 또는 타격 쿨다운이 지난 항목을 제거합니다. 비활성 대상이라는 이유만으로 즉시 제거하지는 않습니다.

`OrbitingBlade`는 피벗이 유효한 `Update()`에서 정리를 시도합니다. `ChasingBlade`는 궤도 상태의 `DealProximityDamage()`에서 정리하며, 추적·복귀 중에는 이 정리 경로를 실행하지 않습니다. 추적 중 직접 타격은 이 사전 대신 `_attackTimer`를 사용합니다.

정리 코드는 [7e5104d](https://github.com/qlghs8-lang/Swarm/commit/7e5104da2272cd8d58676b037a6a63742af174ae)에서 도입했습니다. 파괴되거나 쿨다운이 지난 대상의 이력을 제거해 사전의 보관 범위를 제한합니다.

같은 콜라이더가 풀에서 재사용되면 기존 키를 다시 사용합니다. 쿨다운 안에 재사용되는 경우에는 이전 개체의 타격 이력이 새 생애에 적용될 가능성이 있습니다. 장시간 실행의 항목 수·메모리 추이와 이 재사용 경계는 후속 검증 대상입니다.

코드: [OrbitingBlade](../Assets/_Project/Scripts/Weapon/OrbitingBlade.cs) · [ChasingBlade](../Assets/_Project/Scripts/Weapon/ChasingBlade.cs)

## 런타임 아레나 리소스

`ArenaBuilder`는 씬 로드 시 Player 태그의 오브젝트가 있고 아레나 루트가 없으면 지형을 생성합니다. 이 생성 조건을 씬 간에 공통으로 사용합니다. `Resources/Ground`에서 이름으로 스프라이트를 읽고 같은 시드로 배치해 `Game`과 `TestStage`의 지형 구성을 공유합니다. 일부 스프라이트가 없으면 건너뛰며, 잔디가 하나도 없으면 아레나 생성 자체를 중단합니다.

같은 리소스 폴더에는 `PotScatterer`가 읽는 `Props/Prop_Pot`, 설정 메뉴의 `SettingsSkin`, `UiFont`의 `Galmuri11-Swarm`도 있습니다. 이름 기반 로드는 씬별 직접 참조 설정을 줄이지만, 경로 변경과 누락은 호출부·리소스를 함께 확인해야 합니다.

현재는 `Resources`로 공통 자산을 로드합니다. 스테이지별 로딩이나 원격 배포가 필요해지면 로딩 시간·메모리를 측정하고, 자산의 로드·해제 수명과 배포 단위를 함께 설계해야 합니다.

코드·에셋: [ArenaBuilder](../Assets/_Project/Scripts/Arena/ArenaBuilder.cs) · [PotScatterer](../Assets/_Project/Scripts/Arena/PotScatterer.cs) · [SettingsMenu](../Assets/_Project/Scripts/UI/SettingsMenu.cs) · [UiFont](../Assets/_Project/Scripts/UI/UiFont.cs) · [Resources 폴더](../Assets/_Project/Resources)
