# Swarm 아트 제작과 Unity 연동

[README](../README.md)에서 소개한 아트의 제작 방식과 게임 내 연결 과정을 정리한 문서입니다. 생성형 AI, Aseprite, MCP 기반 도구를 함께 사용했으며, 결과물의 선택·수정 판단과 게임 내 확인을 직접 진행했습니다.

캐릭터와 이펙트는 생성 이미지의 실루엣·프레임을 정리하고, UI와 타일은 크기·보더·반복 배치 규격에 맞춰 제작했습니다. 이미지 제작 이후의 Animator, 프리팹, 공격 효과 연결도 제작 범위에 포함했습니다.

## 1. 제작 방식과 적용 대상

| 분류 | 제작 방식 | 저장소의 대표 결과물 |
| --- | --- | --- |
| 플레이어 3종 | PixelLab 초안·애니메이션 생성 → Aseprite 프레임 정리 | `Warrior.aseprite`, `Archer.aseprite`, `Mage.aseprite`와 각 Animator Controller |
| 일반 적 3종 | 기본형 생성 → 팔레트 변형 → 애니메이션 연결 | `SwarmGrunt`, `SwarmGrunt_Fast`, `SwarmGrunt_Tank` |
| 보스 | PixelLab 생성 → Aseprite 후가공 → 이동 애니메이션 연결 | `Boss.aseprite`, `Boss.controller`, `Enemy_Boss.prefab` |
| 무기·보스 이펙트 | GPT·Gemini 이미지 레퍼런스 → Aseprite·MCP 기반 프레임 정리와 연동 | `Textures/Effects/`의 원본과 이펙트 프리팹 |
| UI·바닥·픽업 | Aseprite를 MCP로 조작해 도형·팔레트·좌표 기반 제작 | 버튼·패널·슬롯, 잔디·돌담·문양, 경험치·골드·자석·힐팩 |
| 항아리 | 레퍼런스 이미지 축소·팔레트 정리 → 파괴 애니메이션 연결 | `Pot.aseprite`, `Prop_Pot.prefab` |
| 타이틀 배경 | Aseprite 스크립트로 생성 후 씬 적용 | [gen_title.lua](../Art_src/Title/gen_title.lua), `Textures/Title/` |

플레이 화면은 [README](../README.md)에 정리했습니다. 드랍 확률과 회복량 등 게임 수치는 [게임 디자인 문서](game-design.md)에서 다룹니다.

## 2. 캐릭터·적 제작과 외형 연결

### 제작 과정

1. PixelLab에서 정면 중심의 캐릭터 초안을 생성했습니다.
2. Idle·Walk와 필요한 액티브 동작을 생성한 뒤, Aseprite에서 태그와 프레임 순서를 정리했습니다. 궁수의 대시는 `Roll` 태그와 Animator 트리거를 사용합니다.
3. 방향이 바뀌거나 소품이 사라지는 프레임을 확인하고 생성·수정을 반복했습니다.
4. `.aseprite`를 Unity에 임포트하고 애니메이션 클립과 Animator Controller를 연결했습니다.
5. 플레이어의 외형은 `CharacterDefinition`의 `defaultSprite`와 `animatorController`에 지정했습니다. 타이틀에서 각 캐릭터를 선택해 실제 전환을 확인했습니다.

당시 제작 기록에는 궁수 Walk의 일부 프레임이 뒷모습으로 바뀌고, 마법사 Idle에서 지팡이가 사라지는 문제가 남아 있습니다. 생성 요청에 정면 유지와 소품 유지를 명시하고 결과 프레임을 다시 확인했습니다. 특정 프롬프트만으로 모든 결과의 일관성이 보장되는 것은 아닙니다.

### 현재 연결 구조

- 플레이어는 정면 스프라이트를 사용하고 `PlayerController`의 `SpriteRenderer.flipX`로 좌우를 전환합니다.
- `CharacterDefinition.ApplyToPlayer()`가 선택한 캐릭터의 스프라이트와 컨트롤러를 적용합니다. 씬의 Player 외형만 바꾸는 것으로 캐릭터별 설정을 대신하지 않습니다.
- `ArcherRollWeapon.StartRoll()`이 `Animator.SetTrigger("Roll")`을 호출합니다.
- 보스 이동 애니메이션 연결에는 현재 저장소의 `BossWalkSetup`을 사용합니다.

플레이어 외형을 추가·교체할 때는 `CharacterDefinition` 에셋과 `Animation` 폴더의 컨트롤러를 함께 확인합니다. 타이틀의 캐릭터 선택부터 게임 진입까지 실행해 외형과 동작이 일치하는지 검증합니다.

코드·에셋: [CharacterDefinition](../Assets/_Project/Scripts/Game/CharacterDefinition.cs) · [PlayerController](../Assets/_Project/Scripts/Player/PlayerController.cs) · [ArcherRollWeapon](../Assets/_Project/Scripts/Weapon/ArcherRollWeapon.cs) · [Animation 폴더](../Assets/_Project/Animation)

## 3. 이펙트 제작과 보스 공격 연결

이펙트는 AI 레퍼런스로 형태와 동작 단계를 잡고, Aseprite·MCP 기반 작업으로 프레임·색상·타이밍을 정리했습니다. 완성 이미지는 게임에 적용해 크기, 위치, 겹침, 공격 판정과의 관계를 확인했습니다.

- 캐릭터가 이미 들고 있는 활·지팡이 등은 이펙트에서 중복되지 않도록 확인했습니다.
- 투사체의 비행 이미지와 명중 효과를 분리했습니다. 발사 빈도가 높은 무기는 명중 효과의 크기와 빈도를 조절했습니다.
- 타격 플래시 등에는 Additive 머티리얼을, 독 장판 등에는 알파 블렌딩을 사용했습니다. 밝기는 배경과 여러 효과가 겹치는 상황에서 확인했습니다.
- 독 장판은 `PoisonGasCloud`가 도입 프레임 → 정점 프레임 유지 → 종료 프레임 순서로 재생합니다. 지속형 효과를 프레임 전체의 반복 재생과 구분했습니다.

### 보스 슬램: 이미지에서 공격 효과까지

`BossSlamSetup`은 `bossslam.aseprite`의 3프레임을 읽어 `Effect_BossSlam_Impact.prefab`을 만들고 `Enemy_Boss`의 `BossSlamAttack`에 연결합니다.

원본을 다시 제작해 스프라이트 참조가 달라진 경우에 대비해 프레임, 연결 상태, 크기, 불투명도를 확인합니다. 이를 통해 이미지 교체 뒤 프리팹 설정과 보스 연결을 반복하는 작업을 줄였습니다.

슬램의 `perspectiveSquash`는 경고 도형과 타격 판정 양쪽에 전달합니다. 경고만 세로로 줄이고 피해 범위를 원형으로 남기지 않도록, 이펙트의 모양과 함께 확인하는 구조입니다.

코드·에셋: [BossSlamSetup](../Assets/_Project/Scripts/Editor/BossSlamSetup.cs) · [BossSlamAttack](../Assets/_Project/Scripts/Enemy/BossSlamAttack.cs) · [BossAttackHits](../Assets/_Project/Scripts/Enemy/BossAttackHits.cs) · [슬램 이펙트 프리팹](../Assets/_Project/Prefabs/Effect_BossSlam_Impact.prefab)

### 현재 저장소의 연결 도구

| Unity 메뉴 | 역할 | 코드 |
| --- | --- | --- |
| `Swarm/Setup/Attach Boss Walk Animation` | 보스 이동 애니메이션 연결 | [BossWalkSetup](../Assets/_Project/Scripts/Editor/BossWalkSetup.cs) |
| `Swarm/Setup/Build Boss Slam Effect` | 슬램 이펙트 생성·보스 연결 | [BossSlamSetup](../Assets/_Project/Scripts/Editor/BossSlamSetup.cs) |
| `Swarm/Setup/Build Boss Rockfall Effects` | 운석 낙하·착탄 효과 생성·연결 | [BossRockSetup](../Assets/_Project/Scripts/Editor/BossRockSetup.cs) |
| `Swarm/Setup/Build Breakable Pot` | 항아리 프리팹과 관련 설정 구성 | [PotSetup](../Assets/_Project/Scripts/Editor/PotSetup.cs) |

연결 도구를 실행한 뒤에는 프리팹 변경 내역과 게임 내 결과를 함께 확인합니다.

## 4. 에셋별 규격과 임포트 기준

아래 값은 **2026-09-18 기준 원본 파일과 `.meta`에서 확인한 현재 설정**입니다. 새 에셋에 일괄 적용하는 공통 규격은 아닙니다. 캔버스 크기에는 투명 여백이 포함되며 실제 실루엣 크기와 다를 수 있습니다.

| 대상 | 소스 캔버스 | PPU | 비고 |
| --- | --- | ---: | --- |
| 플레이어·일반 적 | 92×92 | 50 | 투명 여백 포함 |
| 보스 본체 | 434×462 | 50 | 일반 적과 별도 캔버스 |
| 일반 무기 이펙트 | 64×64 | 대부분 100 | `SlashEffect.aseprite`, `lifesteel.aseprite`는 50 |
| 보스 슬램 | 512×512 | 100 | 3프레임, 코드에서 표시 크기 조정 |
| 보스 운석 | 256×256 | 100 | 5프레임, 낙하·착탄 분리 |
| 항아리 | 48×48 | 64 | 6프레임, Idle 1 + Break 5 |
| 패널 프레임 예시 (`Panel_Frame`) | 48×48 | 100 | Border 6px |
| 체력 채움 예시 (`Fill_HP`) | 76×60 | 100 | Border 4px |
| 바닥 타일 예시 (`Grass_01`) | 32×32 | 32 | 스케일 1에서 1유닛 |
| 중앙 문양 (`Center_Sigil`) | 256×256 | 32 | 스케일 1에서 8유닛 |
| 보스 체력바 프레임 / 채움 | 1400×180 / 1094×69 | 100 | UI 크기는 RectTransform에서 배치 |

### 위치와 크기

- 피벗은 에셋의 용도에 맞게 지정합니다. 캐릭터의 발 기준과 이펙트의 중심 기준을 구분하고, 프레임 전환·회전 시 이미지가 흔들리지 않는지 확인합니다.
- 프레임을 잘라내는 영역과 캔버스 기준이 다를 수 있으므로, `.meta`의 피벗 값이 0~1을 벗어났다는 이유만으로 오류라고 판단하지 않습니다. 임포터 설정과 실제 렌더링 위치를 함께 확인합니다.
- 공격의 기준점이 필요할 때는 발 위치와 피해 판정 중심을 구분합니다. `PlayerStats.AttackOrigin`은 콜라이더 중심을 반환하도록 되어 있습니다.
- 방향을 회전시키는 이펙트는 해당 무기 코드가 가정하는 원본 방향과 맞춥니다. 모든 이펙트에 같은 피벗·방향을 강제하지 않습니다.
- PPU를 바꾸면 월드 표시 크기가 달라집니다. 기존 스케일, 공격 반경, 오프셋과 함께 확인합니다.

### 재임포트와 렌더링

- Aseprite 원본을 다시 제작하거나 프레임 구성을 바꾼 뒤에는 참조와 재생 순서를 확인합니다. 모든 수정이 서브에셋 ID 변경을 일으킨다고 가정하지 않습니다.
- 픽셀 경계가 필요한 에셋은 Point 필터와 압축 설정을 확인합니다. 프레임의 여백·트림·메시 설정도 실제 효과에 맞춰 확인합니다.
- 바닥 경고 도형은 `SortingLayers.DECAL`, 공격 효과는 해당 렌더링 레이어를 사용합니다. 기본 레이어에 남아 바닥에 가려지지 않도록 확인합니다.
- `MageBoltWeapon.WarmUpStrikeFlashShader()`와 `MageFireballWeapon.WarmUpFireballShader()`에는 투명한 이펙트를 한 프레임 준비하는 경로가 있습니다. 초기 표시 문제에 대응한 구현이며, 모든 플랫폼에서 셰이더 준비가 완료된다는 보장으로 해석하지 않습니다.

## 5. UI·배경·픽업

UI·타일·픽업은 도형·팔레트·좌표를 지정하는 방식으로 제작했습니다. 버튼의 보더, 타일 이음매, 픽업 실루엣처럼 반복되는 규격을 다루기 위한 선택입니다. 생성 후에는 실제 해상도와 게임 화면에서 결과를 확인했습니다.

- **UI:** 늘어나는 프레임에는 9-slice용 Border와 Sliced 설정을 함께 사용합니다. 전체 화면을 덮는 레벨업·결과 패널은 배경 가림 정도를 따로 확인합니다. 기존 틴트 색이 남아 원본 색과 곱해지는지도 확인합니다.
- **타일:** 잔디 4종과 장식을 섞어 반복감을 줄였습니다. `ArenaBuilder`가 바닥과 돌담 등을 런타임에 배치합니다. 카메라가 경계에 가까워졌을 때도 바닥이 이어지는지 확인합니다.
- **픽업:** 경험치는 마름모, 골드는 원형 중심의 실루엣으로 구분했습니다. 색·외곽선·크기를 함께 사용해 배경이나 다른 픽업과 겹칠 때의 식별성을 확인했습니다.
- **픽셀 밀도:** 바닥과 캐릭터의 PPU가 다릅니다. 이를 맞추려면 PPU뿐 아니라 Transform 스케일과 배치 계산도 검토해야 합니다.

### 보스 체력바

현재 체력바는 `BossBar_Frame`과 `BossBar_Fill`을 분리하고, `BossHealthBarUI`가 `Filled / Horizontal / Left` 방식으로 체력 비율을 연속 표시합니다.

채움 텍스처는 1094×69로 분리되어 있고, 기본 보정값 `fillStart = 0`, `fillEnd = 1`을 사용합니다. 이미지를 교체할 때는 텍스처의 여백, RectTransform 배치, 보정값을 함께 확인합니다.

코드: [BossHealthBarUI](../Assets/_Project/Scripts/UI/BossHealthBarUI.cs) · [BuffIconBarUI](../Assets/_Project/Scripts/UI/BuffIconBarUI.cs) · [ArenaBuilder](../Assets/_Project/Scripts/Arena/ArenaBuilder.cs)

## 6. 제작 도구와 외부 리소스

- **생성형 AI:** PixelLab 및 GPT·Gemini 기반 이미지 생성·레퍼런스를 활용했습니다. ‘전량 수작업’으로 표현하지 않습니다.
- **편집·생성 도구:** Aseprite, MCP 기반 조작, Lua 스크립트를 활용했습니다. 제작 방식은 위 카테고리별 기록과 함께 설명합니다.
- **폰트:** 외부 폰트인 Galmuri의 프로젝트용 파일 `Galmuri11-Swarm.ttf`를 포함합니다. 저장소의 [Galmuri 라이선스 원문](../Assets/_Project/Fonts/Galmuri-OFL.txt)은 SIL Open Font License 1.1을 명시합니다.
- **WebGL 한글 표시:** 씬·프리팹의 폰트 지정과 런타임 UI의 `UiFont.Current`를 연결했습니다. 코드에서 생성하는 설정·버프 UI도 같은 폰트를 사용하도록 구성했습니다([UiFont](../Assets/_Project/Scripts/UI/UiFont.cs), [WebGL 문서](webgl-build.md)).

제작 도구와 외부 리소스의 출처를 구분해 기록합니다. 폰트 등 함께 배포하는 외부 리소스의 라이선스 원문도 저장소에 보관합니다.
