# Swarm 아트 조달 계획

무료 에셋 스토어 대신 직접 생성하는 파이프라인을 기본 경로로 삼는다. 카테고리에 따라 두 갈래로 나뉜다:

- **플레이어 캐릭터 / 적**: PixelLab로 생성 → Aseprite 후가공 (2장 참고)
- **무기 이펙트**: GPT·Gemini로 레퍼런스 이미지 생성 → Aseprite에서 선 정리 후가공 (3장 참고)

파이프라인이 감당 안 되거나 시간이 급한 카테고리는 언제든 무료 에셋으로 되돌아갈 수 있게 열어둔다.

---

## 1. 카테고리별 적용 범위

| 분류 | 상태 | 비고 |
|------|------|------|
| 플레이어 — 전사 | ✅ 완료 | Idle/Walk + Animator + `CharacterDefinition` 연결 완료 (`Warrior.aseprite`, `Warrior.controller`) |
| 플레이어 — 궁수 | ✅ 완료 | Idle/Walk/Roll + Animator + `CharacterDefinition` 연결 완료 (`Archer.aseprite`, `Archer.controller`). Roll은 `ArcherRollWeapon.StartRoll()`에서 `Animator.SetTrigger("Roll")` 호출로 연동 |
| 플레이어 — 마법사 | ✅ 완료 | Idle/Walk + Animator + `CharacterDefinition` 연결 완료 (`Mage.aseprite`, `Mage.controller`). 로브가 발을 가려 다리 스트라이드 대신 옷자락 스윙으로 이동감 표현 |
| 적 3종 (기본/빠름/탱커) | 🟡 진행 중 | 기본형(`SwarmGrunt`) PixelLab 생성 + Aseprite 팔레트 스왑으로 빠름/탱커 파생 완료. Animator/`Enemy_*` 프리팹 연결 완료. 크기 차등은 기존 프리팹 Transform Scale 그대로 사용 |
| 무기 이펙트 (근접/투사체/범위) | 🟡 진행 중 | GPT·Gemini 레퍼런스 + Aseprite 선 정리(3장)로 전사/궁수/마법사 다수 무기 완료. 상세 내역은 6장 참고 |
| UI (HP 바, 경험치 바, 타이머, 카드 아이콘) | 미정 | |
| 타일맵/배경 | 미정 | |

미정 카테고리는 무료 에셋으로 임시 대체해도 되지만, 기본 방향은 "가능하면 직접 생성"으로 잡는다.

---

## 2. 캐릭터/적 파이프라인 (PixelLab + Aseprite)

1. PixelLab MCP `create_character`로 초안 생성 — **레퍼런스 이미지 없이 텍스트 설명만, `mode="standard"`, `view="low top-down"`** (레퍼런스 이미지를 쓰면 원본 앵글 편향이 그대로 옮아붙어 탑다운이 아닌 측면/카드아트 구도가 나옴)
2. Idle: `breathing-idle` 템플릿으로 우선 시도 → 무기/지팡이 같은 소품이 일부 프레임에서 사라지면 `mode="v3"` 커스텀으로 "계속 쥐고 있음"을 명시해 재생성
3. Walk: 템플릿 모드(`walking-4-frames` 등)는 방향이 뒤집혀 뒷모습이 섞이는 버그 위험이 있음 → 처음부터 `mode="v3"` 커스텀으로 "항상 정면 유지"를 명시해서 생성
4. (필요시) 액티브 스킬 애니메이션(예: 구르기)도 `mode="v3"` 커스텀으로 생성 — 동작 묘사를 구체적으로(무릎 굽힘, 상체 기울기 등) 써야 원하는 실루엣이 나옴
5. Aseprite에서 태그별(`Idle`, `Walk`, `Roll` 등)로 프레임 정리 → `.aseprite`로 저장 (Aseprite CLI(`-b --script`)로 스크립트 조립 가능)
6. Unity `com.unity.2d.aseprite` 임포터로 임포트, `Generate Animation Clips` 체크(기본값), **Pixels Per Unit을 50으로 수정** (Pivot Alignment는 최초 1회 Bottom Center로 설정해두면 이후 신규 임포트에 프로젝트 기본값으로 자동 상속됨)
7. 전용 에디터 툴(`Assets/_Project/Scripts/Editor/<Character>AnimatorSetup.cs` 패턴)로 AnimatorController 생성 + Player의 두 씬(Game.unity, TestStage.unity)에 임시 연결해서 눈으로 확인
8. `CharacterDefinition`(`defaultSprite`/`animatorController` 필드)에 실제로 연결 — `CharacterVisualLinker.cs`로 각 캐릭터 에셋(`Warrior.asset` 등)에 스프라이트+컨트롤러 배선. 이게 있어야 타이틀에서 고른 캐릭터가 실제로 표시됨 (그냥 씬에 박아두면 마지막으로 세팅한 캐릭터만 계속 보임)
9. Play 모드에서 캐릭터별로 선택→플레이해서 루프/전환 확인 → 어색한 프레임만 Aseprite에서 재수정 (전체 재작업 지양)

### 캔버스/해상도
- 소스 해상도 48px 안팎(캐릭터 콘텐츠 기준) — 이 장르(Vampire Survivors, Brotato류) 기준 적정 사이즈. 16×16/24×24는 디테일이 뭉개져서 부적합
- 렌더링 방식: 정면(south) 스프라이트 1개 + `SpriteRenderer.flipX`로 좌우만 전환. `PlayerController.cs`가 이미 이 패턴이라 8방향 회전 스프라이트 불필요

### 알려진 함정
- 스프라이트 pivot을 bottom으로 바꾸면 `transform.position`이 "발밑"이 된다. 기존 Player의 `CircleCollider2D`는 `offset.y = 0.7`(가슴 높이)로 세팅돼 있어서, 무기 판정·픽업 자석처럼 플레이어 기준 좌표가 필요한 코드는 `transform.position`이 아니라 **`PlayerStats.AttackOrigin`(콜라이더 실제 중심)** 을 써야 한다. 새 캐릭터 붙일 때도 동일하게 확인할 것.
- **템플릿 애니메이션(`walking-4-frames` 등)은 방향 일관성이 깨질 수 있다** — 궁수 Walk에서 일부 프레임이 뒷모습으로 튀는 버그 발생. `mode="v3"` 커스텀 + "항상 정면 유지" 명시로 해결.
- **템플릿 애니메이션은 든 소품(무기/지팡이)을 프레임마다 유지 못할 수 있다** — 마법사 Idle에서 지팡이가 1프레임만 남고 나머지에서 사라짐. `mode="v3"` 커스텀 + "소품을 계속 쥐고 있음" 명시로 해결.
- **8방향 회전 생성 시 일부 대각선 방향에서 무기가 2개로 겹쳐 보이는 경우 있음** (예: 전사 검) — 어차피 south 1방향+flipX만 쓰므로 무시 가능.
- **씬에 Player 스프라이트/Animator를 직접 박아두는 것과 `CharacterDefinition` 연결은 별개다** — `<Character>AnimatorSetup.cs`의 `ApplyToScene()`은 에디터에서 눈으로 확인하기 위한 임시 수단일 뿐, 실제 게임에서 캐릭터별로 외형이 바뀌려면 `CharacterVisualLinker.cs`로 `CharacterDefinition` 에셋에 배선까지 해야 한다.

---

## 3. 이펙트 파이프라인 (GPT·Gemini 레퍼런스 → Aseprite 선 정리)

1. AI 이미지 생성 도구(GPT, Gemini 등)에게 이펙트 레퍼런스 시트를 요청 — 스타일/팔레트/프레임 구성(예: "몇 프레임, 어떤 동작 단계")을 구체적으로 명시해서 받기
2. Aseprite로 레퍼런스 이미지를 그대로 가져와서 **선/도트 정리** (처음부터 손으로 트레이싱하는 방식은 퀄리티가 안 나와서 포기 — 레퍼런스를 그대로 활용하고 지저분한 선과 색을 정리하는 방식으로 진행, 사용자 본인이 작업)
3. 프레임 스페이싱/타이밍 조절
4. Unity 연동 + 인게임 테스트 — Claude가 담당. `Swarm/Art Test/...` 메뉴 패턴의 Editor 링커 스크립트로 씬/프리팹에 배선

### 그릴 때 참고할 것
- **캐릭터가 이미 들고 있는 무기/도구(활, 지팡이 등)는 이펙트 자체에 포함하지 않는다** — 캐릭터 스프라이트가 이미 그 무기를 들고 렌더링되므로, 이펙트에도 그리면 두 개로 겹쳐 보임. 이펙트(궤적/충격/빛)만 그릴 것
- 레퍼런스가 에어브러시/블러 느낌이어도 우리 기존 스타일(1px 외곽선 + 디더링 없는 면 분할)과 다를 수 있음 — 어느 쪽으로 재해석할지는 선 정리하면서 감각적으로 판단하면 됨. 이펙트끼리 스타일이 완전히 통일될 필요는 없음
- **발사체(투사체)류는 "비행 중 스프라이트"와 "명중 시 히트 이펙트"를 분리해서 그리는 게 실전 연동에 유리하다** — 비행 중 프레임은 1~4장, 명중 이펙트는 짧은 버스트(1~2프레임)로 별도 제작. 발사 빈도가 높은 무기는 히트 이펙트를 생략하거나 짧고 작게 유지할 것

### 프로젝트 기술 표준 (그림체와 무관하게 항상 적용)
- 캔버스 64×64px, PPU 50
- Sprite Mesh Type: Full Rect (Tight 아님) — 뾰족한 실루엣이 확대 시 깨지는 문제 방지
- Pivot Space: Canvas + Pivot Alignment: Center
- 스프라이트 원본은 정면 방향을 **오른쪽(+X)** 으로 통일해서 그릴 것 — 코드의 회전 공식(`Atan2(facing) → Euler`)이 기본적으로 "오른쪽이 정면"이라고 가정하므로, 다른 방향으로 그리면 무기별 회전 보정 상수를 추가해야 함
- 이펙트 크기/위치는 실제 무기 판정 반경(`radius`)이나 스케일 필드에 비례해서 코드로 조정 — 레벨업/범위 증가 패시브로 반경이 커져도 이펙트가 따라가야 함(오프셋도 같은 배율로 스케일해야 캐릭터와 안 겹침)
- 순간적으로 터지는 원샷 이펙트(타격 플래시, 슬래시 등)는 Additive 블렌드(`SpriteAdditive.mat`)가 잘 어울리고, 지속되는 장판형 이펙트(독 구름 등)는 알파 블렌드를 유지하는 편이 여러 개 겹쳤을 때 과다노출되지 않아서 낫다

### 알려진 함정 (이펙트)
- **스프라이트시트 프레임마다 트림 영역이 다르면 기본 상속 피벗(Bottom Center, 프레임별 trim 기준)이 프레임마다 완전히 다른 지점을 가리켜 회전 시 위치가 튐** — 이펙트처럼 프레임마다 실루엣이 크게 변하는 애니메이션은 **Pivot Space: Canvas + Pivot Alignment: Center**로 명시적으로 바꿔야 함 (캐릭터의 Bottom Center 상속값을 그대로 두면 안 됨)
- **Pivot Alignment를 "Center"로 바꿔도 실제 프레임별 피벗이 재계산 안 되는 경우가 있다** — 적용 후에는 항상 `.meta`의 `animatedSpriteImportData[].pivot` 값을 직접 확인해서 Y가 0.5 근처인지 검증할 것 (x=1.8, y=-4 같이 0~1 범위를 크게 벗어나면 깨진 것). 안 고쳐지면 Inspector에서 Pivot Alignment를 다른 값으로 바꿨다 다시 Center로 바꾸고 Apply(강제로 변경 이벤트를 트리거)
- 색상 톤 선택 주의 — 짙은 남색 계열은 "물속성" 느낌으로 오해되기 쉬움. 밝은 파스텔톤 위주가 검기/광속성처럼 더 무난하게 읽힘
- 스프라이트 서브에셋은 내용이 바뀌면 내부 식별자가 갱신되므로, `.aseprite`를 덮어쓸 때마다 **연결 스크립트(`Swarm/Art Test/Link ...` 패턴)를 다시 실행**해야 함
- 방향성 있는 판정(부채꼴/원뿔 등)은 `AoeWeaponData.attackAngleDegrees`로, 캐릭터 앞쪽으로 오프셋된 판정(예: 넓은 범위 슬램)은 `forwardOffset`으로 표현 가능 — 360도 각도 + 오프셋 조합이면 "캐릭터 주변 원형"이 아니라 "캐릭터 앞쪽에 위치한 꽉 찬 원"이 됨
- 스크립트로 동적 생성한 렌더러에 Additive 머티리얼을 처음 쓰면, 해당 셰이더 변종이 첫 실사용 시점에 컴파일되면서 한 프레임 색이 깨지는 플래시가 보일 수 있음 — 씬 로드 시 알파 0으로 한 프레임만 그려서 미리 컴파일시키는 워밍업 코루틴으로 방지 (기존 무기 스크립트의 `WarmUpEffectShader` 패턴 참고)

---

## 4. 스타일 통일 원칙

- 플레이어 3종은 같은 팔레트/디테일 수준으로 통일 (PixelLab 생성 파라미터 — outline, shading, detail 옵션을 동일하게 유지)
- 적/이펙트/UI는 별도 스타일이어도 무방 (직접 비교되는 영역이 아님)

---

## 5. 라이선스 체크리스트

- PixelLab 생성물은 제3자 에셋 라이선스 이슈가 없지만, [PixelLab ToS](https://pixellab.ai/termsofservice) 상 상업적 이용(포트폴리오 공개 포함) 조건은 확인해둘 것
- 무료 에셋을 보조로 쓰게 되는 카테고리에 한해서만 아래 적용:
  - [ ] 상업적 이용(포트폴리오 공개, 취업 지원용 시연) 허용 여부
  - [ ] 출처 표기(attribution) 의무 여부 → 있다면 크레딧 화면/README에 기재
  - [ ] 재배포·수정 제한 여부
  - [ ] 라이선스 원문 또는 링크를 프로젝트 내 기록으로 남겨두기

---

## 6. 진행 상황

| 시점 | 상태 |
|------|------|
| 전사 Idle/Walk + Animator + CharacterDefinition 연결 | ✅ 완료 |
| 궁수 Idle/Walk/Roll + Animator + CharacterDefinition 연결 | ✅ 완료 |
| 마법사 Idle/Walk + Animator + CharacterDefinition 연결 | ✅ 완료 |
| 플레이어 캐릭터 3종 (외형 전환 포함 실사용 가능) | ✅ 완료 |
| 적 기본형(SwarmGrunt) + 팔레트 스왑 빠름/탱커 + Animator 연결 | ✅ 완료 |
| 전사 — 기본무기(전방 베기)/콤보어택(베기·찌르기·내려찍기)/흡혈·레이지(패시브)/공전검·소드체이싱 | ✅ 완료 — 전부 3장 파이프라인으로 제작. 원샷 이펙트에 Additive 블렌드(`SpriteAdditive.mat`) 적용 |
| 궁수 — 화살(활쏘기)/화살비(볼리)/집중(포커스)/난사(스프레이)/맹독 구르기(독 장판) | ✅ 완료 — 화살·화살비·집중은 명중 히트 이펙트(`arrow_hit`) 공유 + Additive 적용, 난사는 발사 빈도가 높아 히트 이펙트 의도적으로 생략. 맹독 구르기 독 장판은 유일한 지속형(looping) 이펙트라 알파 블렌드 유지, `PoisonGasCloud.cs`가 인트로→Peak 유지→아웃트로 구조로 재생 |
| 마법사 — 낙뢰(전체 즉발 스트라이크)/체인 라이트닝(낙뢰 진화, 연쇄 투사체)/화염구/치유/축복(치유 진화) | ✅ 완료 — 낙뢰·화염구는 Additive 적용, 치유·축복은 알파 블렌드 유지 |
| 나머지 무기(마법사 잔여분) / 보스 / UI / 타일맵 | 미정 — 카테고리별로 파이프라인 vs 무료 에셋 결정하며 진행 |

시간이 급해지면 미정 카테고리는 무료 에셋으로 전환해 전체 루프(이동→전투→레벨업→웨이브→결과 화면) 완성을 우선한다.
