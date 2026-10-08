# 에셋 부족 조사 — 2026-10-07

현재 연결된 에셋을 기준으로 보면 가장 큰 부족은 **스킬을 구별하는 아이콘, 보상 종류를 구별하는 아이콘, 일부 전투 효과, 장비·캐릭터 화면의 그림**이다. 기존 PNG에 합쳐져 있는 문양·프레임·배경의 분리도 필요하다. 몬스터는 추가 제작보다 이미 있는 프리팹을 전투에 연결하는 작업이 먼저다.

조사 범위: `_Project`의 프리팹 74개, 게임·샌드박스 씬 5개, 에셋 표, 스킬·아이템·스테이지 데이터, 런타임 표시 코드, `Assets` 전체의 이미지·오디오 파일. Sprite GUID뿐 아니라 fileID를 비교해 같은 아틀라스 안의 서로 다른 그림을 중복으로 오인하지 않도록 했다. 씬에서 재정의한 스프라이트도 확인했다. `2-Sheet` 메타데이터의 옛 이름 표 대신 현재 spriteSheet의 internalID/name을 우선했다.

추가 조사 범위는 사용자의 지시에 따라 **PSD를 제외한 PNG와 해당 PNG의 SpriteImporter 메타데이터**다. 아래의 PNG 분리 판단은 시트 원본을 직접 보고 크기·알파·분할 영역을 확인한 결과다. 원본 아트나 프리팹은 수정하지 않았다.

파일 참조와 소스 코드 기준 조사다. Unity CLI는 최초 상태 확인에서 ready였으나 이후 연결 인스턴스를 찾지 못했고, 권한을 높여 재시도해도 동일했다. 따라서 실제 플레이 화면의 크기·가독성·애니메이션 품질은 이번 조사에서 검증하지 않았다.

| 우선순위 | 부족한 것 | 확인된 상태 | 필요한 작업 |
|---|---|---|---|
| 1 | 스킬 6종의 전용 아이콘 | 전투 스킬 9종이 아이콘 3세트를 공유 | 6종 × HUD/신규 카드/강화 카드 = 18개 연결 자리용 그림 |
| 1 | 보상 아이콘 | 아이템 표가 비어 있고 모든 보상 슬롯이 코인 그림을 사용 | 아이템 17키 + 코인/EXP/랜덤 재료 2종, 총 21자리 연결 |
| 1 | 냉기 지대·전기 구름·에너지 빔의 전투 그림 | Unity 기본 스프라이트에 색만 지정 | 각 스킬을 식별할 수 있는 전용 VFX 3세트 |
| 1 | 슬라임 탄 | 일반·엘리트 모두 같은 기본 스프라이트 탄 | 슬라임 공격용 탄 그림 1종, 필요하면 엘리트 변형 |
| 2 | 장비 그림 | 장비 7칸과 강화 팝업이 같은 기본 UI 스프라이트 | 장비 7종 그림과 데이터별 아이콘 연결 |
| 2 | 로비 스킬 목록·강화 팝업 그림 | 18칸의 기본 아이콘이 같고 Bind에 아이콘 교체가 없음 | 전투 스킬 아이콘 재사용 연결 + 로비 스킬 데이터 정합성 정리 |
| 2 | 캐릭터 초상화 | 메인 초상화와 캐릭터 카드 15칸이 기본 UI 스프라이트 | 우선 기본 캐릭터 초상화 1종; 추가 캐릭터는 데이터 확정 후 제작 |
| 2 | 방벽 수리 카드 그림 | 전용 아이콘이 비어 있어 기본 카드 아이콘으로 대체 | 수리 카드 아이콘 1종 |
| 2 | 결과 별점 그림 | Star1~3이 기본 스프라이트, 코드가 색만 변경 | 별점 그림 1종을 공용 사용 |
| 2 | 몬스터 다양성 | 10종 아트 중 3종만 전투 스폰에 연결 | 미연결 7종의 전투 프리팹·데이터 연결 |
| 3 | 연쇄 번개의 시각적 완성도 | LineRenderer와 텍스처 없는 ChainBolt 재질 | 번개 선·방전 표현을 보강할지 결정 |
| 3 | 스테이지별 배경 | 3개 스테이지가 같은 Stage 씬의 bg_1 사용 | 스테이지 차별화가 필요하면 추가 배경과 선택 연결 |
| 3 | 오디오 | Assets에 오디오 원본 0개, 런타임 AudioSource/재생 코드 없음 | 로비·전투 BGM 및 공격/피격/UI/클리어 효과음과 재생 연결 |

## 스킬 아이콘: 어떤 그림을 돌려 쓰는가

근거: [SkillAssetTable.asset](../Assets/_Project/Data/SkillAssetTable.asset), [Skills.json](../Assets/_Project/Resources/MockData/Skills.json).

| 현재 그림의 의미 | 같은 그림을 쓰는 스킬 | 교체 대상 |
|---|---|---|
| 화살 | 화살, 서리 결정, 나무뿌리, 냉기 지대 | 서리 결정·나무뿌리·냉기 지대 |
| 화염구 | 화염구, 에너지 빔 | 에너지 빔 |
| 벼락 | 벼락, 전기 구름, 연쇄 번개 | 전기 구름·연쇄 번개 |

각 그룹은 HUD 아이콘뿐 아니라 신규 카드와 강화 카드의 sprite fileID까지 같다. 부족한 6종은 작은 HUD 그림과 신규/강화 카드용 그림을 각각 연결할 자리가 있다. 18개는 출력·연결 자리 수이고, 원화는 스킬당 하나에서 크기와 상태를 파생할 수 있다. 기존 카드 배경은 이미 있으므로 배경까지 스킬마다 새로 만들 필요는 없다.

`wall_repair.upgradeCardIcon`도 null이다. `cardFallback`은 `4-Sheet`의 닫기 버튼에 쓰는 것과 같은 스프라이트라 수리 카드를 나타내는 그림이 아니다. 근거: [EnergyRecoverPopup.prefab](../Assets/_Project/Prefabs/UI/EnergyRecoverPopup.prefab), [CardSelectView.cs](../Assets/_Project/Scripts/Runtime/View/Card/CardSelectView.cs).

## 보상: 코인 그림이 모든 종류를 대신한다

[ItemIconTable.asset](../Assets/_Project/Data/ItemIconTable.asset)의 `_coin`, `_exp`는 null, `_items`는 빈 배열이다. 코드에 정의된 `_randomSkillMaterial`, `_randomEquipmentMaterial`도 에셋에 직렬화된 값이 없어 기본 null이다.

[RewardList.prefab](../Assets/_Project/Prefabs/UI/RewardList.prefab)의 슬롯 5개는 모두 `6-Sheet.png / 6-sheet_1` 코인 그림이다. [ResultRewardListView.cs](../Assets/_Project/Scripts/Runtime/View/Result/ResultRewardListView.cs)는 조회된 아이콘이 null이면 슬롯의 기존 그림을 유지한다. 결과·일시정지 화면 모두 같은 표와 표시 코드를 사용하므로 EXP, 마법북, 보석상자, 랜덤 재료가 코인으로 표시되는 경로가 확인된다.

필요한 연결 키는 [Items.json](../Assets/_Project/Resources/MockData/Items.json)에 있다.

- 스킬 마법북 9종: ArrowBook, FireballBook, LightningBook, FrostCrystalBook, LogBook, ColdZoneBook, LightningCloudBook, EnergyBeamBook, ChainLightningBook.
- 장비 마법북 7종: HatBook, TopBook, ShoesBook, WeaponBook, RingBook, TieBook, EmployeeIdBook.
- GemChest 1종.
- 별도 필드 4개: 코인, EXP, 랜덤 스킬 재료, 랜덤 장비 재료.

**21개 모두 새 그림을 제작해야 한다는 뜻은 아니다.** 코인 그림은 이미 있고, `2-Sheet`에는 책 그림, `3-Sheet`에는 상자 그림도 있다. 기존 그림을 연결하고, 서로 다른 종류를 구별할 수 없는 항목에 원화·문양·색 변형을 추가하는 순서가 효율적이다. 랜덤 재료는 확정된 재료와 구별할 수 있는 표현이 필요하다.

## 전투: 기능은 있지만 전용 그림이 없는 효과

| 기능 | 실제 연결 | 판단 |
|---|---|---|
| 냉기 지대 | [FrostPrison.prefab](../Assets/_Project/Prefabs/Skills/FrostPrison.prefab)의 Visual: 기본 스프라이트 fileID 10913, 청색 반투명 | 냉기 바닥·얼음 경계 표현 부족 |
| 전기 구름 | [LightningCloud.prefab](../Assets/_Project/Prefabs/Skills/LightningCloud.prefab)의 Visual: 동일한 기본 스프라이트 fileID 10913 | 구름·방전 표현 부족 |
| 에너지 빔 | [SunBeam.prefab](../Assets/_Project/Prefabs/Skills/SunBeam.prefab)의 Visual: 기본 스프라이트 fileID 10907 | 빔의 중심·끝·광선 표현 부족 |
| 슬라임 탄 | [Slime_Projectile.prefab](../Assets/_Project/Prefabs/Stage/Slime_Projectile.prefab): 기본 스프라이트 fileID 10913 | 적의 공격 종류를 나타내는 그림 부족 |
| 연쇄 번개 | [LightningChain.prefab](../Assets/_Project/Prefabs/Skills/LightningChain.prefab), [ChainBolt.mat](../Assets/_Project/Materials/ChainBolt.mat): MainTex null | 선 기반 효과는 존재; 추가 아트는 완성도 개선 항목 |

화살·화염구·서리 결정·나무뿌리·벼락·전기 구체에는 이미 그림이나 파티클 표현이 연결돼 있다. 화염구 변형, 얼음 3조각 변형, 큰/불타는 나무 변형, 심판 벼락도 visualRoot 또는 전용 visual 참조가 존재한다. 기존 문서의 “임시 연결” 문구만 보고 전부 누락으로 세지 않았다.

## 로비: 아트와 바인딩이 함께 부족하다

- [EquipSlot.prefab](../Assets/_Project/Prefabs/UI/EquipSlot.prefab): Icon이 기본 UI 스프라이트다. [CharacterScreen.prefab](../Assets/_Project/Prefabs/UI/CharacterScreen.prefab)에 7칸이 있고 모자·상의·신발·무기·반지·넥타이·사원증에 해당한다. [EquipSlotView.cs](../Assets/_Project/Scripts/Runtime/View/Lobby/EquipSlotView.cs)의 Bind는 레벨·잠금만 변경하므로 그림을 추가한 뒤 선택한 장비에 맞춰 아이콘을 전달해야 한다.
- [SkillListSlot.prefab](../Assets/_Project/Prefabs/UI/SkillListSlot.prefab): Icon이 같은 기본 UI 스프라이트다. [SkillScreen.prefab](../Assets/_Project/Prefabs/UI/SkillScreen.prefab)에 18칸이 있고 [SkillListSlotView.cs](../Assets/_Project/Scripts/Runtime/View/Lobby/SkillListSlotView.cs)는 아이콘을 교체하지 않는다. [SkillUpgradePopup.prefab](../Assets/_Project/Prefabs/UI/SkillUpgradePopup.prefab)의 Info/Icon/Art도 기본 스프라이트이고 팝업 표시 코드에 아이콘 바인딩이 없다.
- [DummyGrowthCatalog.cs](../Assets/_Project/Scripts/Runtime/Core/Lobby/DummyGrowthCatalog.cs)의 공개 스킬 6종은 단검·화염탄·빙결·낙뢰·가시·섬광이다. 실제 전투 스킬 9종과 ID·이름이 다르다. 로비 18칸을 보고 곧바로 새 스킬 그림 18종을 주문할 근거는 없다. 스킬 데이터 연결을 먼저 확정하고 실제 전투 아이콘을 공용으로 사용해야 한다.
- CharacterScreen의 메인 Portrait, [CharacterCard.prefab](../Assets/_Project/Prefabs/UI/CharacterCard.prefab)의 Portrait, 카드 15칸이 기본 그림이다. [CharacterScreenView.cs](../Assets/_Project/Scripts/Runtime/View/Lobby/CharacterScreenView.cs)는 카드 목록 데이터가 없어 그대로 둔다고 명시한다. 현재 캐릭터 초상화 1종을 먼저 마련하고 나머지는 캐릭터 명세 확정 뒤 제작한다. 전투용 Player_Animated 프리팹은 이미 있다.
- 장비/스킬 강화 팝업의 BookIcon·CoinIcon, SkillScreen의 Gold/Gem/Ticket 아이콘도 기본 UI 그림이다. 코인·보석·책은 기존 아틀라스에서 연결할 후보가 있어 재제작보다 연결이 먼저다. 티켓은 전용 그림을 확인하지 못했다.
- 결과 Star1~3은 기본 스프라이트이고 [ResultStarsView.cs](../Assets/_Project/Scripts/Runtime/View/Result/ResultStarsView.cs)가 색만 바꾼다. 별점 그림 하나를 세 위치에 공용으로 연결하면 된다.

## 몬스터: 미연결 재고를 활용할 수 있다

[Spawn.prefab](../Assets/_Project/Prefabs/Stage/Spawn.prefab)의 실제 스폰은 일반 Slime/Bat, 엘리트 Slime/Bat, 보스 Skeleton 5항목이다. 시각적으로는 3종을 사용한다.

일반·엘리트는 각각 Slime_Animated와 Bat_Animated를 공유한다. [WaveMonsterVisualLinker.cs](../Assets/_Project/Scripts/Editor/WaveMonsterVisualLinker.cs)에 일반은 탈색, 엘리트는 원색과 1.2배 크기로 구별하는 의도가 명시돼 있다. 따라서 이 공유만으로 엘리트 원화 2종이 필수로 부족하다고 판단하지 않았다. 추가 실루엣·장식은 가독성을 더 높이기 위한 후보다.

`Prefabs/Monsters`에는 다음 7종도 있으나 현재 전투 스폰까지 연결되지 않는다: Ghost, BrainSpider, EyeJelly, Spider, ArmoredCrab, Horned, Golem. 애니메이션 파일도 있다. EyeJelly는 BrainSpider 비주얼 내부에 참조가 있지만 해당 계열이 전투 스폰에는 연결되지 않는다.

보스는 Skeleton 그림을 사용한다. 보스 전용 외형을 원한다면 기존 Golem/Horned 등으로 전투 프리팹과 행동을 구성할 수 있다. “보스 그림이 없다”보다 “보스 역할에 어떤 기존 아트를 쓸지와 연결이 부족하다”가 정확하다.

## 부족으로 세지 않은 정상 공유

- 같은 아틀라스 PNG의 서로 다른 sprite fileID.
- 여러 슬롯의 공통 프레임·카드 배경·닫기/뒤로 버튼.
- 일반 서리 결정과 기본 서리 결정의 동일 프리팹, 보조 에너지 빔과 기본 빔의 동일 프리팹.
- 부모/자식 투사체가 같은 계열의 그림을 쓰는 것.
- HUD/CardSelect의 초기 null 아이콘: 실행 중 SkillAssetTable에서 채우는 구조다.
- 몬스터 프리팹의 비활성 루트 SpriteRenderer: 별도의 Visual 자식이 실제 몬스터 그림을 표시한다.
- 파티클·LineRenderer가 실제 그림을 담당하는 객체의 null SpriteRenderer.
- 실제 Lobby 씬에서 전용 그림을 재정의한 상단 재화·메뉴·하단 탭. 원본 NavTab/CurrencySlot의 null만으로 누락 판정하지 않았다.

## PNG에는 있지만 합쳐져 있어 재사용이 제한되는 그림

PNG 시트는 그림마다 사각형으로 잘라져 있어도, **잘라진 한 스프라이트 내부의 문양·바탕·테두리·표시는 별도 레이어가 아니다.** 같은 버튼 전체를 여러 위치에 쓰는 것은 가능하지만, 문양만 추출하거나 배경색·잠금 표시·조명 등을 따로 제어하려면 분리가 필요하다. 합쳐진 픽셀에 가려진 원래 그림은 사각형 분할만으로 복구되지 않는다.

| 우선순위 | PNG와 스프라이트 | 합쳐진 요소 | 재사용하려면 필요한 분리 | 가능한 활용 |
|---|---|---|---|---|
| 1 | [2-Sheet.png](../Assets/_Project/Sprites/2-Sheet.png)의 `weapon_0`~`weapon_5` | 화살/불/번개 문양 + 회색/청색 원형 바탕 + 테두리 | 큰 문양 3종, 공통 원형 프레임, 상태별 바탕 | HUD·로비·카드·마법북에 문양 공용 사용 |
| 1 | 같은 시트의 `2-Sheet_8`, `_9`, `_13`, `_14`, `_26` 등 메뉴 그림 | 책·인물·우편·종이 문양 + 사각 버튼 바탕·테두리 | 문양, 공통 사각 프레임, 버튼 바탕 | 책 그림을 보상·강화 비용으로 사용하고 버튼 프레임은 다른 메뉴에도 사용 |
| 1 | 같은 시트의 `_24`, `_25`, `_47`, `_48` 잠금 그림과 `_45`, `_46`, `_54`, `_55`, `_59`, `_61`, `_62` 등 탭 그림 | 문양 + 버튼 바탕 + 일부 그림의 빨간 X; 활성/비활성 그림도 통째로 존재 | 정상 문양과 프레임을 기반으로 잠금 X·선택 바탕을 별도 이미지로 구성 | 한 아이콘에서 정상·선택·잠금 상태 파생 |
| 2 | [4-Sheet.png](../Assets/_Project/Sprites/4-Sheet.png)의 `4-sheet_2`, `_4`, `_5`, `_14` | 닫기 X + 회색 바탕; 카드 바탕 + 테두리; 큰 창의 질감 + 노란 테두리 + 좌상단 장식 | X/장식/프레임/질감 | 공용 닫기 아이콘·팝업 프레임·색을 바꿀 수 있는 카드 바탕 |
| 2 | [5-Sheet.png](../Assets/_Project/Sprites/5-Sheet.png)의 `5-Sheet_14` | 코인 + 청색 사각 바탕 + 테두리 | 코인/바탕/프레임; 다른 시트의 독립 코인을 쓰면 코인 추출 생략 가능 | 재화 슬롯·보상 슬롯·가격 표시 공용 구성 |
| 2 | [3-Sheet.png](../Assets/_Project/Sprites/3-Sheet.png)의 `3-Sheet_15` 등 상자 | 상자 몸통 + 뚜껑 + 보석 + 광선 | 몸통/뚜껑/내용물/빛 | 상자 열기·광선 애니메이션과 내용물별 보상 상자. 정적 상자는 현재 그림 그대로 사용 가능 |
| 3 | 같은 시트의 `bg_0` | 하늘 + 구름 + 풀밭 | 하늘/구름/전경 풀, 가려진 배경 복원 | 구름 이동·시차 효과·배경 조합 변경 |
| 3 | 5-Sheet의 `5-Sheet_1`, `_3` | 배경 질감과 명암·조명; `_1`은 밝은 조명까지 합쳐짐 | 공통 질감과 조명/어둡게 하는 오버레이 | 성공·실패·일시정지 화면에 공통 배경과 상태별 조명 사용 |
| 3 | [bg_1.png](../Assets/_Project/Sprites/bg_1.png) | 회색 바탕 + 노트 줄 + 양옆 질감 + 명암 | 바탕/줄/가장자리/명암 | 스테이지별 배경 변형·줄 간격·가장자리 효과 독립 제어 |

`weapon_*`는 각각 83×88 픽셀이다. 별도로 존재하는 `skillIcon_1`~`3`은 23×19, 19×22, 18×23 픽셀이라 큰 카드·로비 그림으로 확대하는 대체품으로는 해상도가 부족하다. 즉 작은 독립 문양이 있다고 해서 큰 문양 분리 작업이 불필요해지는 것은 아니다.

잠금 X가 덮은 부분을 해당 PNG에서 지우면 그 아래 원래 픽셀이 자동으로 살아나지 않는다. 같은 시트에 있는 정상 상태 문양을 활용하고 X만 별도로 얹는 방식이 우선이다. 새로운 스킬 문양 6종은 기존 3종의 프레임을 분리해도 생기지 않으므로 여전히 신규 제작 대상이다.

## 분리 없이 이미 재조합 가능한 PNG 재고

- **코인:** `6-Sheet / 6-sheet_1`은 독립 코인이다. `5-Sheet_14`에서 코인을 다시 떼어낼 필요 없이 이 그림을 사용하고 바탕을 따로 얹을 수 있다. 보상 종류별 그림이 없는 문제와는 별개다.
- **하트:** `4-Sheet / 4-sheet_8`의 단일 하트가 있다. `_9`의 여러 하트 묶음을 꼭 분리하기보다 단일 하트를 여러 위치에 배치해 재구성할 수 있다.
- **캐릭터와 나비:** `3-Sheet / bg_1`, `bg_2`는 각각 캐릭터 그림과 나비로 이미 따로 잘려 있다. 같은 시트의 `bg_0` 배경에서 떼어낼 필요가 없다. 여기의 `bg_1`은 별도 파일 `bg_1.png`와 다른 에셋이다.
- **기본 프레임·게이지:** `2-Sheet`에는 문양 없는 `card_0`, `card_1`, 게이지와 슬롯 그림이 있다. 공용 바탕 후보로 활용 가능하다. 일부 벽·게이지는 9-slice border도 이미 설정돼 있다.
- **닫힌/열린 상자:** 정적 표시용 그림은 `3-Sheet`에 이미 있으므로 ItemIconTable을 채우는 작업부터 가능하다. 뚜껑·내용물 분리는 애니메이션·다른 보상 구성으로 확장할 때 필요하다.

많은 버튼·팝업 PNG는 프레임과 안쪽 질감이 합쳐져 있지만, 크기 조절만 목적이라면 9-slice 설정으로 해결할 수 있다. 이는 프레임과 바탕의 색·움직임을 독립적으로 바꾸는 레이어 분리와 구별해야 한다.

제작 순서는 **기존 그림 연결 → 큰 문양·공통 프레임 분리 → 새로운 문양·전투 VFX 제작**으로 조정하는 것이 적절하다. 우선 분리할 것은 `weapon_*` 6장의 문양/바탕과 메뉴·탭의 문양/프레임/잠금 표시다. 배경·상자 분리는 관련 연출이 필요할 때 진행한다.
