# Swarm 상세 설계

[README](../README.md)의 핵심 구현을 보충하는 설계 기록입니다. 현재 코드에서 확인할 수 있는 동작과 추가 검증이 필요한 부분을 구분했습니다.

## 독 장판의 상태 소유권

### 문제와 선택

쿨타임 감소로 독 장판이 겹치는 상황에서, 적 하나에 적용되는 독 피해 주기를 하나로 유지하는 것이 목표였습니다. 장판마다 독립적으로 피해를 주면 중첩 수에 따라 피해 주기가 달라집니다.

장판별 상태를 적에게 등록하고 가장 강한 효과를 고르는 방식도 가능하지만, 현재 독 장판 종류는 하나입니다. 별도 목록과 우선순위 관리 대신 장판이 중독 신호를 보내고 적이 상태와 타이머를 관리하는 방식을 선택했습니다.

1. `PoisonGasCloud.Update()`가 범위 내 적에게 `ApplyPoison()`을 호출합니다.
2. `EnemyHealth`가 신호 여부와 피해·디버프 값을 저장합니다. 이미 중독된 적의 틱 타이머는 호출마다 초기화하지 않습니다.
3. `EnemyHealth.Update()`에서 틱 타이머를 한 번 진행하고, 만료 시 피해를 적용합니다.
4. 신호가 없으면 중독 상태를 내리고 방어 감소·둔화를 해제합니다.

지속시간 누적 대신 **장판 안에 있는 동안 적용**하는 규칙입니다. 초기 구현에서 피해 값과 디버프 갱신 시점이 달랐던 부분은 `RefreshPoisonDebuffs()` 호출로 맞췄습니다. 실제 값이 달라진 경우에만 디버프 세터를 호출합니다.

### 현재 한계

- 값이 다른 장판이 겹치면 마지막으로 전달된 값을 사용합니다. 다른 종류를 추가할 때는 강도·출처에 따른 우선순위를 정해야 합니다.
- 틱 타이머는 하나지만 범위 탐색은 장판마다 수행합니다.
- 신호 전달과 소비가 별도 `Update()`에 있어 실행 순서에 따른 최대 한 프레임의 차이를 고려해야 합니다.
- 화상은 `FirePatch`가 대상을 점화하고 지속시간을 갱신하는 별도 규칙을 사용합니다. 두 효과를 통합할지는 장판을 벗어난 뒤에도 피해가 남아야 하는지부터 결정해야 합니다.

코드: [PoisonGasCloud](../Assets/_Project/Scripts/Weapon/PoisonGasCloud.cs) · [EnemyHealth](../Assets/_Project/Scripts/Enemy/EnemyHealth.cs) · [FirePatch](../Assets/_Project/Scripts/Weapon/FirePatch.cs)

## 진화 전후 액티브 스킬 UI

`IActiveRoll`로 기본 대시와 맹독 대시의 쿨타임 표시·입력 처리를 공통화했습니다. 현재 `RollCooldownUI`의 활성 스킬 선택은 두 구현체를 직접 참조하므로, 새 액티브 스킬을 추가할 때는 선택 로직도 수정해야 합니다. 표시 계약의 공통화와 구현체 탐색의 확장성은 구분합니다.

코드: [IActiveRoll](../Assets/_Project/Scripts/Weapon/IActiveRoll.cs) · [RollCooldownUI](../Assets/_Project/Scripts/UI/RollCooldownUI.cs)

## 오브젝트 풀의 역할 분리

| 클래스 | 사용 상황 | 구조 |
| --- | --- | --- |
| `ObjectPool` | 무기·스포너가 자기 프리팹을 생성 | 인스턴스별 큐와 중복 반납 방지 집합 |
| `SharedObjectPool` | 여러 적이 같은 픽업·데미지 숫자를 생성 | 프리팹을 키로 `ObjectPool` 공유 |
| `RuntimeObjectPool` | 런타임에 조립하는 이펙트 생성 | 프리팹 없이 생성하는 경로 지원 |

소유자가 다른 풀을 분리하면서도 공유 풀은 기존 `ObjectPool`을 재사용합니다. 풀 사용 자체가 무할당을 보장하지는 않습니다. 초기 생성, 큐·집합의 용량 확장, 재사용 시의 상태 초기화를 별도로 점검해야 합니다.

풀에서 꺼낸 적의 좌표는 `Rigidbody2D.position`에도 반영합니다. 스폰마다 전체 물리 Transform을 동기화하던 경로를 줄이면서, 다음 물리 스텝 전에 실행되는 타겟 탐색이 이전 좌표를 읽는 문제에 대응했습니다. Rigidbody2D 없이 콜라이더만 있는 경우에는 전체 동기화 경로가 남아 있습니다.

코드: [ObjectPool](../Assets/_Project/Scripts/Weapon/ObjectPool.cs) · [SharedObjectPool](../Assets/_Project/Scripts/Weapon/SharedObjectPool.cs) · [RuntimeObjectPool](../Assets/_Project/Scripts/Weapon/RuntimeObjectPool.cs)

## 군중 물리와 보스 패턴

플레이어의 물리 바디와 피해 판정용 `PlayerHurtbox`를 분리했습니다. 적을 밀어내는 범위와 피해를 받는 범위를 각각 조정하기 위한 구성입니다. 적은 넉백 중 추적 조향을 멈춰 밀리는 힘과 추적이 서로 상쇄되는 현상을 줄입니다.

스턴은 기존 시간과 새 시간의 최댓값을 사용합니다. 둔화는 활성 상태에서 더 강한 배율을 택하고 남은 시간은 최댓값으로 갱신합니다. 따라서 약한 둔화가 나중에 들어와도 강한 배율의 지속시간이 늘어날 수 있으며, 각 효과의 수명을 독립적으로 관리하는 구조는 아닙니다.

보스는 `EnemyStatusImmunity`로 넉백·스턴을 제한합니다. 슬램과 운석은 독립적인 타이머를 가지되 발동 전 `BossPatternGate`를 통해 다른 패턴의 시전 여부를 확인합니다. 패턴 중첩을 제어하는 판단을 개별 공격 동작과 분리한 구성입니다.

코드: [EnemyChaser](../Assets/_Project/Scripts/Enemy/EnemyChaser.cs) · [BossPatternGate](../Assets/_Project/Scripts/Enemy/BossPatternGate.cs)

## 데미지 계산과 표시

무기 쪽에서 레벨·능력치·크리티컬 배율을 계산하고, 적 쪽에서 방어력과 관통력을 반영합니다. 적이 받는 `isCritical`은 피해 숫자의 색상·크기 표시에 사용합니다.

공격 단위의 크리티컬 결과를 여러 대상에 공유하며, 도트와 고정 피해 프록은 별도 경로를 사용합니다. 특히 다른 공격 도중 발동하는 프록이 `LastAttackWasCritical`을 덮어쓰지 않도록 기본 배율과 크리티컬 굴림 경로를 구분했습니다.

## 화면 밖 적의 재배치

플레이어보다 느린 적이 화면 밖에 남으면 전투에 참여하지 않는 개체가 누적됩니다. `EnemySpawner`는 멀어진 적을 주기적으로 찾아 플레이어 주변에 재배치하고, 일반 스폰에 활성 적 상한을 적용합니다. 웨이브 스폰은 이 상한을 넘어 생성될 수 있습니다.

반납 후 재스폰하는 방식도 상태 보존을 추가하면 사용할 수 있습니다. 여기서는 같은 인스턴스를 이동하는 방식으로 체력·드랍 관련 상태를 유지했습니다. 거리 기준과 재배치 위치는 순간이동이 시야에 보이지 않는지 플레이 조건별로 확인해야 합니다.

코드: [EnemySpawner](../Assets/_Project/Scripts/Spawner/EnemySpawner.cs)

## 스프라이트 정렬

렌더러 역할을 `Ground → Decal → Prop → Wall → Pickup → Character → Effect → Overlay`로 구분했습니다. 플레이어와 적은 같은 `Character` 레이어에서 Y축 정렬을 적용해 위치에 따라 앞뒤가 바뀌도록 했습니다.

플레이어를 항상 위에 그리는 방식은 식별하기 쉽지만, 앞에 선 적까지 뒤로 가려지는 문제가 있었습니다. 같은 레이어에서 정렬하고 `sortingOrder`는 레이어 내부의 예외에 사용합니다. 레이어 이름은 `SortingLayers`에 모았습니다.

코드: [SortingLayers](../Assets/_Project/Scripts/Rendering/SortingLayers.cs)

## 타격 이력의 수명 관리

`OrbitingBlade`와 `ChasingBlade`는 `Dictionary<Collider2D, float>`로 대상별 마지막 타격 시각을 기록합니다. 현재는 2초마다 무효 대상과 타격 쿨다운이 지난 항목을 정리합니다.

목적은 더 이상 판정에 필요하지 않은 참조를 제거하는 것입니다. 풀에서 동일한 콜라이더가 재사용되는 경우 키가 계속 증가하는 것은 아니므로, **제거 코드가 없다는 사실만으로 무제한 메모리 누수나 조회 성능 저하를 확정할 수는 없습니다.** 장시간 성능 개선을 정량 주장하려면 정리 전후의 항목 수·메모리·프레임 기록이 필요합니다.

코드: [OrbitingBlade](../Assets/_Project/Scripts/Weapon/OrbitingBlade.cs) · [ChasingBlade](../Assets/_Project/Scripts/Weapon/ChasingBlade.cs)

## 런타임 아레나 리소스

아레나 지형과 일부 UI는 `Resources`의 이름 기반 로드를 사용합니다. 런타임 설치 코드에서 공통 리소스를 찾게 해 `Game`과 `TestStage` 씬의 중복 참조 설정을 줄였습니다.

현재 규모에서는 이 구성을 유지합니다. 스테이지별 로딩이나 원격 배포가 필요해지면 로드·해제 수명과 배포 단위를 다시 설계할 수 있습니다. 에셋 파일 크기를 런타임 메모리 사용량으로 간주하지 않으며, 로딩 방식의 전환 비용도 호출부 개수만으로 판단하지 않습니다.
