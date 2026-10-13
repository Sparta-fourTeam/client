# 제작 에셋 검토

브랜치: `feat/missing-game-assets`. 초기 3장 그림체는 사용자 승인 완료.

현재 상태 (2026-10-11): `LayeredAssets`의 제작용 조합 프리팹 117개는 씬·에셋·코드에서 참조하지 않아 폴더째 삭제했다. 원화와 Sprite, 실제 화면에 연결된 `Art_*` 레이어는 유지한다. 아래의 프리팹 경로·개수와 JSON·갤러리는 당시 제작 기록이며 현재 런타임 에셋 목록이 아니다. 현재 구조는 [UI 시스템](../ui-system.md)을 참고한다.

## 시트 재제작: 독립 레이어 구성

후속 요청에 따라 기존 `2-Sheet`~`6-Sheet`도 재제작했다. 원본의 컨셉을 유지하면서 배경색, 테두리, 아이콘, 상태 표시를 분리했다. 하늘·잔디·결과 조명도 각각 별도 레이어다. **PNG 11장 안에 독립 Sprite 71개**, 기존 시트 조각 117개에 대응하는 조합 프리팹 117개를 준비했다. 활성 원화 PNG는 전체 88장이다. PSD는 제외했다.

[레이어·조합 갤러리](sheet-layer-gallery.html)에서 레이어를 하나씩 보거나 테두리·아이콘·전경을 켜고 끌 수 있다. 색 면과 아이콘이 포함된 조합은 Unity 프리팹의 `Art_Border`, `Art_Glyph`, `Art_Overlay`/`Art_Lighting` 자식으로 각각 교체한다. 이미지 픽셀은 생성 출력 그대로이며 Unity Sprite Editor API로 슬라이싱했다.

| PNG | 독립 조각 | 역할 |
|---|---:|---|
| UIFrames | 6 | 사각형·카드·버튼·패널·게이지 테두리 |
| UIFills | 16 | 공용 배경 색 면 |
| UIMenuGlyphs | 12 | 메뉴·책·집·문서·우편·나비 등 |
| UIControlGlyphs | 12 | 방향·닫기·일시정지·속도·영상·비활성 표시 |
| UICircleLayers | 6 | 원형 색 면·테두리·보상 노드 |
| WallStates | 3 | 정상·파손·잔해 상태 |
| UIStrokes | 9 | 제목·상태 띠 |
| UIExtraGlyphs | 4 | 메뉴 기호·허수아비·진행선·청록 표시 |
| LobbySky, LobbyGrass, ResultSpotlight | 3 | 하늘·잔디·조명 |

원화: `Assets/_Project/Sprites/Generated/LayeredSheets/`. 조합 프리팹: `Assets/_Project/Prefabs/UI/LayeredAssets/`. 현재 저장된 프리팹 12개에서 Image 78곳과 Sprite/아이콘 연결 11곳을 교체했다. 실행 중 속도 기호가 바뀌는 `buttonImage`는 분리한 기호 Image로 연결했고, 카드 3칸에는 별도 테두리를 추가했다. 파손 방벽은 기존 월드 폭을 유지했다. 테두리·색 면·띠에는 9-slice border를 설정했고, UI 자식 레이어는 LayoutGroup에서 제외하고 raycast를 끄도록 구성했다. 신규 시트 Max Size는 1024, 하늘·잔디는 2048이며 원본 파일 크기는 유지했다.

제작은 built-in `image_gen`을 사용했다. [최종 파일·프롬프트·슬라이싱 기록](sheet-layer-manifest.json), [기존 조각 대응표](sheet-layer-recipes.json), [연결 기록](sheet-reference-changes.txt), [Unity 검사](sheet-unity-validation.json), [파일 검사](sheet-static-validation.json)에 상세 결과가 있다. 임포트·슬라이스·조합 프리팹·속도 기호 연결·카드 테두리·방벽 상태·하늘/잔디 분리 검사를 통과했다. 전체 기존 에셋 검사도 통과했다.

열린 Lobby 씬의 기존 미저장 변경과 개별 Sprite override를 보존했으며 씬 파일은 저장하지 않았다. 새 연결은 저장된 프리팹 기준이다. Stage 씬에 직접 설정된 시트 참조도 유지되며 대응 조합 프리팹을 제공한다. 실제 게임 화면의 전체 플레이 검증은 포함하지 않는다.

## 기존 PNG 재제작 및 공용 에셋 추가

이전 작업의 사용자 지정 범위는 **스킬·아이템·전투 효과**였으며, PSD와 UI 버튼·패널·배경을 제외했다. 기존 색·형태·역할을 유지한 26장과 공용 2장을 built-in `image_gen`으로 추가 제작했다. 이전 49장을 포함한 당시 활성 PNG는 총 77장이었다. [갤러리](gallery.html)의 `추가 제작` 필터에서 그 28장을 확인할 수 있다.

| 종류 | 이번 파일 수 | 저장 폴더 / 주요 에셋 |
|---|---:|---|
| 기본 스킬 | 3 | `Generated/Skills`: Arrow, Fireball, Lightning |
| 재화·아이템 | 6 | `Generated/Items`: Coin, Gem, Heart, HeartBundle, ChestClosed, ChestOpen |
| 전투 그림 | 8 | `Generated/Effects`: ArrowProjectile, LightningStroke, ChalkStroke, FlameDab, IceChip, WoodCrumb, DustPuff, EyeJellyCross |
| 파티클 텍스처 | 9 | `Generated/EffectTextures`: LightningFrames, ElectricFrames3x3, FxSoft, FxHole, FxRing, FxSparkle, FxSpiral, FxStreak, FxBeam |
| 공용 그림 | 2 | `Generated/Common`: MonsterShadow, SkillCircleFrame |

위 경로의 접두사는 `Assets/_Project/Sprites/`이다. 원본 PNG/시트는 다른 UI가 사용하므로 보존했다. 기본 스킬 테이블의 아이콘 9칸과 HUD 5칸의 배경을 새 그림으로 연결하고, 아이템·투사체·파티클의 테이블/프리팹/머티리얼 참조 102곳을 교체했다. 스킬 그림에는 원형 프레임이 붙어 있지 않으며, 회색 원형 배경은 HUD에서 별도 이미지로 사용한다.

몬스터 그림자는 `Assets/_Project/Prefabs/Effects/MonsterShadow.prefab`으로 재사용할 수 있다. 기본 불투명도 32%, 정렬 순서 -1, 캔버스 폭 1월드 단위이며 몬스터 크기에 맞춰 Transform Scale을 조절한다. 몬스터별 배치는 이번 작업에 포함하지 않았다. 파티클 6종은 기존 방식을 따라 투명 여백을 제외한 별도 Sprite 에셋도 만들었으며, 기존 월드 폭에 맞춘 실제 검사를 마쳤다.

새 아이콘·공용 그림·작은 파티클은 Max Size 256, 번개 시트 2종은 1024다. 생성 원본 픽셀은 그대로 보존했다. 이번 프롬프트 전체는 [existing-refresh-prompts.json](existing-refresh-prompts.json), 생성 원본/최종 파일 대응은 [existing-refresh-manifest.json](existing-refresh-manifest.json), Unity 검사는 [refresh-unity-validation.json](refresh-unity-validation.json)에 기록한다. 갤러리의 64/128px 표시로 작은 크기에서 검토할 수 있다.

열린 Lobby 씬이 이미 저장되지 않은 변경을 포함하므로 씬 파일은 저장하거나 교체하지 않았다. 그 씬의 기존 개별 sprite override는 보존되며, 새 참조 연결은 저장된 테이블·프리팹·머티리얼 기준이다. 플레이 모드 전체 전투 검증은 포함하지 않는다.

## 첫 제작 기록

플레이어 PSD의 검은 후드·해진 로브·해골 장식·초록 구슬 지팡이에 맞춰 장비를 제작했다. 기존 무기 슬롯은 지팡이 무기, 사원증 슬롯은 인장이다. 장비 강화 재료는 인챈트 주문서, 스킬 강화 재료는 마법북이다. 주문서는 하나의 공통 틀을 참조해 내부 아이콘만 바꾼 완성 PNG이며, 런타임에서 레이어를 나누지 않는다.

[전체 갤러리](gallery.html)에서 종류, 표시 크기(64/128/256px), 밝은/어두운 배경을 바꾸며 볼 수 있다. 선택된 에셋의 경로·GUID·해시는 `production-manifest.json`에 기록한다.

| 기존 저장 슬롯 | 현재 표시 | 장비 원화 | 강화 주문서 키 |
|---|---|---|---|
| `equipment.hat` | 후드 | Hood | HoodScroll |
| `equipment.top` | 로브 | Robe | RobeScroll |
| `equipment.shoes` | 장화 | Boots | BootsScroll |
| `equipment.weapon` | 무기(지팡이) | Staff | StaffScroll |
| `equipment.ring` | 뼈 반지 | BoneRing | BoneRingScroll |
| `equipment.tie` | 해골 목걸이 | SkullNecklace | SkullNecklaceScroll |
| `equipment.employee_id` | 인장 | Seal | SealScroll |

`book.*` 아이템 ID와 `equipment.*` 강화 ID는 기존 저장 데이터와 호환되도록 유지한다. 표시 이름과 `IconKey`만 현재 콘셉트에 맞춘다.

첫 제작은 새 원화 38장과 기존 PSD를 픽셀 변경 없이 PNG로 추출한 11장이다. 초기 승인 시안 3장은 `Generated/Review`에 별도로 보관한다. 다른 콘텐츠의 자리표시 아이콘을 교체하고, 같은 콘텐츠의 HUD/카드 아이콘은 같은 원화를 공유한다. 기본 스킬 3종은 추가 제작에서 프레임 없는 새 그림으로 교체했다.

PNG는 생성기의 투명 알파를 보존한다. UI 스프라이트는 Single, 중앙 pivot, 100 PPU, mipmap 없음, bilinear, 최대 512px 임포트 설정이다. 전투 효과는 원래 내장 스프라이트의 월드 크기를 유지하도록 PPU를 맞추고 최대 2048px로 임포트한다. 충돌·공격 범위와 몬스터 애니메이션은 변경하지 않는다.

프롬프트 기록은 `prompts.json`(초기 승인), `production-prompts.json`(초기 제작), `equipment-revision-prompts.json`·`seal-prompts.json`(장비 수정), `equipment-scroll-prompts.json`(초기 주문서), `shared-scroll-prompts.json`(공통 틀), `consistent-scroll-prompts.json`(최종 완성 주문서)이다. 교체된 장비 책/주문서 시안과 공통 틀 작업 파일은 `superseded-equipment`에 보관하며 게임 Assets에는 포함하지 않는다.

Unity 실제 임포트·테이블·프리팹 검사 결과는 `unity-validation.json`에 저장한다. 당시의 고정 목록 검사 메뉴는 삭제했다. 현재 UI 연결은 `Tools > Project Nova > UI Workspace`의 문제 검사와 Test Runner의 에셋 바인딩 테스트로 확인한다. 정적 파일 검사 결과는 `static-validation.json`에 저장한다. 플레이 모드에서 전체 화면을 실행한 검증은 별도다.

이 폴더의 최초 생성·복사·연결·출력용 Python 스크립트 7개는 완료된 작업을 덮어쓰는 일회성 도구여서 삭제했다. 원화·프리팹·갤러리·매니페스트와 검증 기록은 유지한다.
