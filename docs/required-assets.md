# 필요한 에셋 목록

기준일: 2026-10-08. 현재 저장된 Unity 에셋 테이블·프리팹·씬·런타임 코드 기준.

## 제작·연결 결과

후속 시트 요청으로 `2-Sheet`~`6-Sheet`의 117개 조각에 대응하는 레이어 구성을 추가했다. 원본 컨셉을 유지해 PNG 11장, 독립 Sprite 71개를 제작했다. 배경색·테두리·아이콘·하늘·잔디·조명을 분리했으며 저장된 프리팹 12개에 연결했다. 제작용 조합 프리팹 117개는 UI 개편 후 참조가 없어 `LayeredAssets` 폴더와 함께 삭제했다. 현재 화면·공통 요소 프리팹과 원본 Sprite를 사용한다. 전체 활성 PNG는 88장이다. [레이어 갤러리](asset-previews/sheet-layer-gallery.html)와 [제작 기록](asset-previews/README.md)에서 확인할 수 있다. PSD와 미저장 씬 변경은 보존했다.

추가 요청에 따라 PSD·UI 버튼/패널/배경을 제외하고 기존 스킬·아이템·전투 효과 컨셉을 유지한 26장을 다시 제작했다. 공용 몬스터 그림자와 스킬 원형 프레임 2장을 더해 이번 추가분은 28장, 활성 PNG는 전체 77장이다. 기본 스킬 3종, 재화/상자, 전투 파티클 참조를 교체했고, HUD에는 분리된 원형 배경을 연결했다. 공용 그림자는 재사용 프리팹까지 준비했다. 상세 범위·참조·씬 저장 상태는 [제작 기록](asset-previews/README.md)에 있다.

아래 목록은 작업 전 누락 조사 기록이다. 이번 브랜치에서 새 원화 38장과 기존 PSD의 원본 합성 픽셀을 추출한 PNG 11장, 총 49장을 준비했다. 초기 그림체 3장은 사용자 승인 완료. 최종 그림과 연결 정보는 [검토 갤러리](asset-previews/gallery.html), [제작 기록](asset-previews/README.md)에 있다.

- P1: 신규 스킬 6종의 HUD/카드 참조와 방벽 수리 카드, 아이템 17개 키, 코인·EXP·랜덤 재료, 몬스터 14 ID의 표시 아이콘을 연결했다.
- 플레이어 원화에 맞춘 장비 7종 및 장비 인챈트 주문서 7종을 제작했다. 기존 무기는 초록 구슬 지팡이, 사원증은 인장이다. 장비 주문서와 랜덤 장비 주문서는 공통 양피지 틀을 기준으로 내부 문양만 바꾼 완성 PNG다. 기존 `book.*`/`equipment.*` 저장 ID는 유지한다.
- 캐릭터 초상화, 캐릭터 화면 장비 7칸과 장비 강화 팝업의 장비/주문서, 확인된 코인 슬롯을 연결했다. 냉기 지대·전기 구름·에너지 빔·슬라임 투사체에는 전용 효과를 연결했다.
- 몬스터 루트의 동일 SpriteRenderer는 비활성 레거시 참조이며 실제 시각 요소는 PSD 기반 애니메이션 자식이다. 전투 몬스터를 새 그림으로 덮어쓰지 않고 기존 원화를 표시 아이콘으로 사용했다.
- 이벤트 배너·캐릭터 속성·티켓처럼 콘텐츠 정의가 확정되지 않은 표시는 이번 신규 제작 범위에서 제외했다.

Unity 실제 스프라이트 임포트·테이블·프리팹 참조 검사는 `asset-previews/unity-validation.json`, 파일·투명도·아이템 ID 호환 검사는 `asset-previews/static-validation.json`에 기록한다. 플레이 모드 전체 실행 검증은 포함하지 않는다.

`참조 없음`은 필요한 슬롯이 비어 있거나 테이블에 항목이 없는 경우로 판단했다. 서로 다른 콘텐츠가 같은 `GUID + fileID`를 쓰는 경우는 교체 후보로 기록했다. 같은 스프라이트 시트의 다른 그림, 공용 UI, 파티클 텍스처의 재사용은 누락으로 세지 않았다. 제작 수량은 파일 수가 아니라 표시 역할 수다. 기존 시트/PSD에서 가져올 수 있는 그림은 연결을 먼저 확인한다.

## 1. 우선 확보·연결할 에셋

| 우선순위 | 필요한 에셋 | 수량/역할 | 현재 상태 | 연결 위치 |
|---|---|---|---|---|
| P1 | 서리 결정 아이콘 | HUD·신규 카드·강화 카드, 3역할 | 화살과 세 아이콘이 모두 동일 | `Data/SkillAssetTable.asset` → `weapon_frost_crystal` |
| P1 | 나무뿌리 아이콘 | 3역할 | 화살과 세 아이콘이 모두 동일 | 위 테이블 → `weapon_log` |
| P1 | 냉기 지대 아이콘 | 3역할 | 화살과 세 아이콘이 모두 동일 | 위 테이블 → `weapon_cold_zone` |
| P1 | 전기 구름 아이콘 | 3역할 | 벼락과 세 아이콘이 모두 동일 | 위 테이블 → `weapon_lightning_cloud` |
| P1 | 에너지 빔 아이콘 | 3역할 | 화염구와 세 아이콘이 모두 동일 | 위 테이블 → `weapon_energy_beam` |
| P1 | 연쇄 번개 아이콘 | 3역할 | 벼락과 세 아이콘이 모두 동일 | 위 테이블 → `weapon_chain_lightning` |
| P1 | 방벽 수리 카드 아이콘 | 강화 카드 1역할 | `upgradeCardIcon` 없음, 공용 fallback 사용 | 위 테이블 → `wall_repair` |
| P1 | 코인·경험치 아이콘 | 2종 | `_coin`, `_exp`가 모두 비어 있음 | `Data/ItemIconTable.asset` |
| P1 | 스킬 마법부 아이콘 | 9종 | `_items: []`, 모든 IconKey 미등록 | 아래 아이템 키 목록 |
| P1 | 장비 마법부 아이콘 | 7종 | `_items: []`, 모든 IconKey 미등록 | 아래 아이템 키 목록 |
| P1 | 보석상자 아이콘 | 1종 | `GemChest` 미등록 | `ItemIconTable` → `GemChest` |
| P1 | 랜덤 스킬·장비 재료 아이콘 | 2종 | 코드에는 필드가 있지만 저장 에셋에 값 없음 | `_randomSkillMaterial`, `_randomEquipmentMaterial` |
| P1 | 몬스터 표시 아이콘 | 14 ID 대응 | 기존 5항목은 icon 없음, 나머지 9항목은 행 자체 없음 | `Data/MonsterDisplayTable.asset` |

스킬 중복 교체는 **6개 스킬 × 3역할 = 18슬롯**이다. HUD·신규·강화를 한 원화에서 파생할지 각각 제작할지는 디자인 결정이다. 아이템·재화·랜덤 재료는 **21종**, 몬스터 표시는 **14 ID**에 대한 연결이 필요하다. 몬스터 미니형은 기존 그림을 재사용할 수 있으므로 14개의 신규 원화가 반드시 필요한 것은 아니다.

### 아이템 아이콘 키

| 구분 | 필요한 키 |
|---|---|
| 스킬 마법부 9종 | `ArrowBook`, `FireballBook`, `LightningBook`, `FrostCrystalBook`, `LogBook`, `ColdZoneBook`, `LightningCloudBook`, `EnergyBeamBook`, `ChainLightningBook` |
| 장비 마법부 7종 | `HatBook`, `TopBook`, `ShoesBook`, `WeaponBook`, `RingBook`, `TieBook`, `EmployeeIdBook` |
| 상자 1종 | `GemChest` |

정의 근거: `Resources/MockData/Items.json`. 누락 영향: 보상 목록·일시정지 획득 가능 목록·결과 화면에서 전용 아이콘을 받지 못하고 자리표시 그림이 남는다. 현재 UI에 이미 재화 그림이 있는 경우 그 그림을 테이블에 연결하면 된다.

### 몬스터 아이콘 대응

| ID | 연결된 전투 프리팹 | 표시 테이블 상태 |
|---|---|---|
| 1 | Slime | 행 있음, icon 없음 |
| 2 | Bat | 행 있음, icon 없음 |
| 3 | Slime_Mini | 행 없음 |
| 4 | Golem | 행 없음 |
| 5 | Spider | 행 없음 |
| 6 | Spider_Mini | 행 없음 |
| 11 | Elite_Slime | 행 있음, icon 없음 |
| 12 | Elite_Bat | 행 있음, icon 없음 |
| 21 | ArmoredCrab | 행 없음 |
| 22 | EyeJelly | 행 없음 |
| 23 | Ghost | 행 없음 |
| 24 | Skeleton | 행 없음 |
| 25 | Oni | 행 없음 |
| 100 | BrainSpider | 행 있음, icon 없음 |

ID 대응은 `MonsterAssetTable.asset` 기준. `MonsterList.prefab`의 5개 Icon도 현재 같은 그림을 사용하므로, 표시 테이블과 함께 연결해야 한다.

## 2. 기존 원화 연결 확인 / 교체 후보

아래는 저장된 프리팹에서 확인한 후보다. 씬 override, PSD의 자식 렌더러, 파티클, 실행 중 표시를 확인한 뒤 제작 여부를 결정한다.

| 필요한 그림/검토 항목 | 현재 근거 | 우선 조치 |
|---|---|---|
| 몬스터별 대표 스프라이트 | ArmoredCrab, BrainSpider, EyeJelly, Ghost, Golem, Oni, Skeleton, Spider, Spider_Mini가 모두 `Monster_Normal.png`의 동일 fileID `6087901085610555463` 사용 | `PSD/`에 기존 원화가 있고 애니메이션도 있음. 자식 원화가 실제로 표시되는지 확인하고 공용 렌더러를 정리/교체 |
| 플레이어 대표 스프라이트 | `Player_Animated.prefab`의 렌더러가 `fx_soft.png` 사용 | `PSD/Player.psd`와 기존 Idle/Attack 애니메이션의 표시 상태 확인 |
| 슬라임 투사체 | `Slime_Projectile.prefab`이 Unity 내장 스프라이트 사용 | 전용 투사체 그림 연결 또는 제작. 일반/엘리트 공유는 의도 확인 |
| 냉기 지대 시각 요소 | `FrostPrison.prefab`이 Unity 내장 스프라이트 사용 | 기존 파티클과 함께 확인 후 바닥·빙결 영역 그림 검토 |
| 전기 구름 시각 요소 | `LightningCloud.prefab`이 Unity 내장 스프라이트 사용 | 기존 파티클과 함께 확인 후 구름 본체 그림 검토 |
| 에너지 빔 시각 요소 | `SunBeam.prefab`이 Unity 내장 스프라이트 사용 | 선형 도형을 의도한 표현인지 확인 후 빔 텍스처 검토 |
| 캐릭터 초상화·속성 아이콘 | `CharacterCard`의 Portrait, `CharacterScreen`의 Portrait/ElementIcon이 내장 UI 그림 | 기존 UI 시트 확인 후 캐릭터별 그림 연결 |
| 장비 아이콘 | `EquipSlot`의 Icon이 내장 UI 그림 | 모자·상의·신발·무기·반지·넥타이·사원증의 대표 그림 연결/제작 |
| 강화 재료·재화 그림 | `EquipUpgradePopup`의 MaterialIcon/CoinIcon, `SkillUpgradePopup`의 BookIcon/CoinIcon이 내장 UI 그림 | 위 아이템/재화 에셋을 공용으로 연결 |
| 이벤트 배너 | `LobbyEventBanner`의 Banner가 내장 UI 그림 | 노출할 이벤트가 확정되면 배너 제작 |
| 보상 상자 | `RewardNode`의 Chest가 내장 UI 그림 | 기존 상자 시트 재사용 확인. `MissionScreen`의 열림/닫힘 상자는 이미 서로 다른 그림 연결됨 |

## 3. 누락으로 세지 않은 참조

- `weapon_ice_shard`, `weapon_frost_crystal_aux`, `weapon_arrow_shard`, `weapon_fireball_shard`, `weapon_lightning_orb`, `weapon_energy_beam_aux`: `Skills.json`에서 `childOnly: true`. 아이콘 3칸이 비어 있지만 독립 신규/강화 카드용 제작 목록에는 넣지 않는다. 별도 UI 노출 요구가 생기면 추가한다.
- `wall_repair.prefab`의 빈 참조: 일반 수리 카드이므로 공격 프리팹 제작 대상이 아니다. 현재 카드 조회는 강화 아이콘 칸을 사용한다.
- HUD·CardSelect의 빈 Icon 슬롯: `SkillAssetTable`에서 런타임에 공급하는 자리다. 슬롯 수만큼 새 그림을 제작할 필요가 없다.
- `RewardList`의 동일 아이콘 5개: 항목별 그림은 `ItemIconTable`에서 공급한다. 원인은 보상 슬롯이 아니라 비어 있는 아이콘 테이블이다.
- HUD와 결과 피해 목록이 같은 스킬 아이콘을 쓰는 것: 같은 콘텐츠의 정상적인 공용 사용이다.
- 부모/보조 스킬의 공용 프리팹, 공용 파티클 텍스처, 공용 UI 프레임: 중복 참조만으로 신규 제작 대상으로 판단하지 않는다.
- Unity의 `m_Icon: {fileID: 0}`: 에디터용 오브젝트 아이콘이므로 게임 에셋 누락에서 제외한다.
- `AppIcon.png`: 프로젝트 내부 직렬화 파일만 보면 참조가 없지만 `ProjectSettings/ProjectSettings.asset`에서 사용 중이다. 미사용으로 분류하지 않는다.

## 확인 범위

에셋 테이블, MockData, UI/Stage/Skills 프리팹, 씬의 스프라이트 override, 아이콘 공급 코드를 정적으로 확인했다. Unity 플레이 모드에서 실제 표시를 검증한 목록은 아니다. P1은 저장 데이터에서 확정된 빈 참조/동일 아이콘이며, 2번 표는 기존 에셋 연결 및 화면 확인이 필요한 후보다. 해상도·최종 화풍·애니메이션 프레임 수는 현재 근거만으로 정하지 않았다.
