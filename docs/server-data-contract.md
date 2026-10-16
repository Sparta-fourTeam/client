# 서버 데이터 필드 계약

대상은 `GameDataStore.TableNames`의 11개 테이블이다. 각 테이블의 중첩 행, 도메인 로더·검증, 전투·보상·성장·튜토리얼 사용처와 `Core/Api/DTO` 및 API 메서드의 요청 인자를 함께 확인한다. 이 문서는 필드 계약을 정리하며 실행 기록은 포함하지 않는다.

## 줄인 전송 필드

| 대상 | 전송에서 제거·통합 | 클라이언트 처리 |
| --- | --- | --- |
| Skills | `requiredCardCounts[].skillId`, `exclusions[].skillId`, `mirrorCards[].skillId` | 전체에서 유일한 `cardId`로 소속 스킬을 찾는다. ID 값은 그대로다 |
| Skills 카드 81 | `requiredWeaponIds: [4]` | 카드 52의 선택 조건이 서리 결정 보유도 보장한다 |
| Items 장비 재료 | `TargetId` | `Upgrades.MaterialItemId`로 장비 대상을 역조회한다. 한 재료에 여러 장비를 연결하면 거절한다 |
| Monsters | `IsBoss`, `IsElite` → `Grade` | 필수 문자열 `Normal`, `Elite`, `Boss` 한 값에서 게임 모델의 등급을 정한다 |
| 버전 API | 전체 `revision` | 테이블별 `tables`만 사용한다. 이전 응답의 전체 `revision`이 있어도 버전 판단에 사용하지 않는다 |

스킬 재료의 `Items.TargetId`는 유지한다. 모든 스킬에 영구 강화 행이 있는 것은 아니므로 `Upgrades.MaterialItemId`만으로 스킬 재료의 대상을 찾을 수 없다. `Upgrades.MaterialItemId`는 실제 강화 비용으로 소모할 아이템을 지정하므로 유지한다.

Items의 장비 재료와 스킬 재료 예:

```json
[
  { "Id": "book.weapon" },
  { "Id": "book.energy_beam", "TargetId": "13" }
]
```

```json
{ "Id": 21, "Grade": "Elite", "Hp": 60, "AttackInterval": 2.5 }
```

`Grade` 누락을 일반 몬스터로 추측하지 않는다. 몬스터 표에는 항상 `Grade`를 보내고 `Monsters`의 스키마 개정을 올린다. 위 JSON은 필드 형식 예이며 출시 밸런스 값을 정하는 예가 아니다.

## JSON에서 제외하는 파생 상태

| 모델 | 제외할 속성 | 계산 근거 |
| --- | --- | --- |
| StageProgressRow | `IsCleared` | `clearRating != 0` |
| StageProgressRow | `HasUnclaimedReward` | `clearRating > claimedRating` |
| UpgradeDefinition | `UsesMaterial` | `MaterialItemId` 존재 여부 |
| StageRewardBalance | `UsesRandomSkillMaterial`, `UsesRandomEquipmentMaterial` | `Items`의 해당 보상 ID·수량 |
| TutorialDefinition | `IsBattle`, `IsGuide` | `Kind` |

클라이언트가 계산한 카드 소속 `OwnerSkillId`, 웨이브의 `Type`, `SplitDescendants`, `GaugeCount`, `EnemyCount`, 스테이지의 `MonsterIds`, 공유 카드의 `affectedWeaponIds`도 전송하지 않는다. 플레이어의 레벨은 누적 `exp`와 `PlayerLevels`로 계산하며 별도로 저장하지 않는다.

## 전체 테이블 검토

| 테이블 | 확인한 필드·중첩 구조 | 유지·정리 이유 |
| --- | --- | --- |
| Skills | 스킬 ID·공격 형태·속성·경로·기본 스탯·영구 성장 ID·강화 상한·자식 전용 여부, 카드 효과·변형·공유 ID·선행·배타·복제 조건 | 카드 소속 ID만 파생한다. `effects[].skillId`는 시전할 자식 스킬이라 유지한다. 속성·발동 확률·상속 배율·선택 상한은 독립 게임 규칙이다 |
| Items | `Id`, `TargetId`, 클라이언트 이름 | 장비의 역참조만 제거한다. 스킬 대상과 아이템 자체 ID는 다른 정보다. 이름은 표시 표에서 찾는다 |
| Upgrades | `UpgradeId`, `MaxLevel`, `UnlockLevel`, 코인·재료 비용의 Base·Step 또는 배열, `MaterialItemId`, `Effects[].Kind`와 `ValuePerLevel` 또는 `LevelValues`(레벨마다 늘어나는 양, MaxLevel개) | 비용·강화 상한·재료 선택·성장 효과는 독립 규칙이다. 스킬 해금은 Skills에서 정하고 장비 해금은 여기서 정한다. 선형 비용은 Base·Step, 비선형은 배열을 보내며 같은 비용을 양쪽 형식으로 중복 전송하지 않는다 |
| Monsters | ID·체력·피해·속도·공격 간격·사거리·투사체 속도·등급·연속 타격·속성/시전 저항·투사체 차단·패시브 | 등급을 하나로 합친다. 공격 방식은 투사체 속도로 이미 계산한다. 사거리와 투사체 속도는 도달 범위와 이동 시간이라 별개다 |
| Monsters.Passives | `Kind`, `Trigger`, `MonsterId`, `Count`, `Interval`, `MaxTotal`, `Statuses`, `Target`, `Multiplier`, `Hits`, `Duration`, `Chance`, `HpRatio`, `HealRatio`, `Invulnerable`, `SpeedMultiplier`, `Element` | Kind가 다르면 쓰는 필드도 다르다. 생성 대상·발동 시점·빈도·총량, 방어 횟수·시간, 발동 체력·회복량은 서로 다른 조건이다. 해당 Kind가 사용하지 않는 필드는 보내지 않는다 |
| Stages | `Id`, 룰렛 최소·최대 횟수, 최초 등급별 코인·아이템 보상, `MonsterBonuses[].Stat/Percent`, 방벽 HP, 스폰 최소·최대 간격·휴식, `Waves[].Spawns[].MonsterId/Count` | 웨이브 ID로 몬스터를 찾지만 마릿수·강화율·스폰 시간은 계산할 수 없다. 최초 등급 보상은 반복 전투 보상과 별개다. 등장 몬스터·게이지 수는 이미 파생한다 |
| StageRewards | `StageId`, `BaseExp`, `ThreeStarBonusExp`, `FinalCoin`, `Items[].itemId/finalAmount` | 반복 전투 보상의 확정 밸런스다. 등급 추가 경험치와 기본 경험치는 독립이다. 중간 지급량은 최종 수량에서 이미 계산한다. StageId는 다른 테이블의 행 연결에 필요하다 |
| Energy | `Max`, `Start`, `RegenSeconds`, `Cost`, `AdRecoverAmount`, `PurchaseRecoverAmount` | 최대치·새 계정 시작량·시간당 회복·입장 비용·광고/구매 회복은 서로 다르다. 시작량을 최대치로 추측하지 않는다 |
| GeneralCards | `Id`, `MaxPicks`, `Forced`, `Priority`, `Condition.Kind/Value`, `Effect.Kind/Value` | 등장 조건과 선택 효과, 강제 등장과 강제 카드 간 우선순위는 별개다. 같은 현재 값을 갖더라도 계산 가능한 중복이 아니다. 이름·설명은 클라이언트 표에서 찾는다 |
| PlayerLevels | `BaseExp`, `StepExp`, `MaxLevel` | 시작 요구 경험치·증가량·진행 상한은 독립이다. 레벨별 배열은 이미 로드 때 만든다 |
| CodexRewards | `Grade`, `Gold` | 몬스터별이 아닌 등급별 지급 정책이다. 등급 키와 지급량 모두 필요하다 |
| Tutorials | `Id`, `Kind`, `After`, `MinLevel`, `StageId`, `Start.Kind/StageId/UpgradeId/Level/Feature`(선택, Kind `featureUnlocked`는 클라이언트 FeatureUnlocks의 Feature가 열렸는지), `Complete.Kind/StageId/UpgradeId/Level`, `Rewards.Kind/Level/UpgradeId/ItemId/Quantity` | 시작 전투와 완료 목표, 시작 레벨과 목표 레벨, 앞 단계 의존과 배열 순서는 별개다. 시작은 `After` 완료·`MinLevel`·`Start`를 모두 만족해야 하며, 강화 가이드는 선행 보상 지급을 보장하는 `After`를 기능 해금 `Start`와 함께 유지한다. `Kind`는 `battle`(자동 전투), `guide`(화면 안내), `milestone`(완료 조건 달성 시 보상 지급)을 지원한다. `milestone`은 자동 입장이나 가이드를 시작하지 않는다. 현재 한 행에서 같은 StageId라고 공통 필드를 제거하지 않는다. Kind의 파생 불리언은 전송하지 않는다 |

## API·플레이어 상태 검토

| 대상 | 유지 이유 |
| --- | --- |
| LoginResponse | 액세스 토큰은 인증에 필요하다. 테이블 개정은 별도 버전 API에서 받는다 |
| DataVersions·버전 HTTP 응답 | 테이블별 개정 딕셔너리는 각 테이블의 변경 여부에 필요하다. 전체 revision은 클라이언트에서 제거한다 |
| TableData | `json`은 테이블 본문이며 다른 필드에서 계산할 수 없다. 본문 endpoint 계약은 별도로 정한다 |
| StartBattleResponse | `battleId`는 결과 제출과 재제출의 같은 전투를 식별한다 |
| SubmitResultRequest | `battleId`, `cleared`, `completedWaves`, `reachedWave`, `wallHpPercent`, `encounteredMonsterIds`는 전투 식별·판정·완료 진척·현재 진척·방벽 상태·도감 등록의 서로 다른 입력이다 |
| SubmitResultResponse | `rewardGold`, `rewardExp`, `clearRating`, `rewardItems`는 서버가 확정한 처리 결과다. 랜덤 재료·재제출 시 확정 결과를 클라이언트 계산으로 대체하지 않는다 |
| PlayerSnapshot | `playerId`·`nickname`은 현재 로컬 표시 정보이며 서버 저장·기기 간 복원은 후속 계약이다. `nickname`, `gold`, `exp`, `energyStored`, `energyUpdatedAt`, `stageProgress`, `upgrades`, `items`, `codex`, `tutorials`는 최신 상태 전체다. 에너지 잔량과 갱신 시각은 둘 다 자연 회복에 필요하다 |
| StageProgressRow | `stageId`, `clearRating`, `claimedRating`은 유지한다. 완료 등급과 이미 수령한 등급이 달라야 미수령 보상을 알 수 있다. 파생 불리언 두 개는 보내지 않는다 |
| UpgradeRow·ItemAmount·CodexRow | 강화 ID·레벨, 아이템 ID·수량, 몬스터 ID·수령 여부는 각 대상과 상태의 쌍이다. 어느 한쪽으로 다른 쪽을 계산할 수 없다 |
| TutorialCompleteResponse | `snapshot`은 최신 전체 상태, `rewardItems`는 이번 요청에서 실제 지급한 내역이다. 이미 완료한 단계 재요청은 지급 내역이 비므로 인벤토리 전체로 대체할 수 없다. 도입 대사를 봤다는 기록은 완료 요청의 `tutorialId`를 `<튜토리얼Id>.dialogue`로 보내며, 조건·보상 없이 `tutorials`에 저장한다(해당 튜토리얼이 없으면 UNKNOWN_TUTORIAL) |
| API 요청 인자 | 계정 로그인 방식·외부 계정, 스테이지·강화·몬스터·튜토리얼 ID, 에너지 회복 출처, 중복 방지 키는 각각 대상·동작·요청 동일성을 식별한다. ID로 중복 방지 키를 계산하지 않는다 |

Local 전용 `BattleRow`의 시드·확정 보상, `LedgerRow`의 거래·출처 기록은 서버 테이블 전송 필드가 아니다. 재제출과 중복 지급 방지에 사용하는 확정 기록이므로 삭제하지 않는다.

## 서버에 전달할 내용

> 클라이언트에서 카드 참조 3곳의 skillId를 제거했고 카드 81의 중복 보유 조건도 제거했습니다. 장비 재료의 Items.TargetId는 Upgrades.MaterialItemId로 찾으므로 빼주시고, 스킬 재료의 TargetId는 유지해주세요. Monsters는 IsBoss·IsElite 대신 필수 Grade(Normal/Elite/Boss) 한 값으로 보내주세요. 버전은 테이블별 tables만 있으면 됩니다. 파생 상태 불리언은 보내지 않아도 됩니다. Skills·Items·Monsters의 스키마 개정을 올려주세요.

기존 스킬 데이터의 누락 필드 보완 요청은 [server-skills-contract.md](server-skills-contract.md)에 있다. ID를 다른 값으로 바꾸는 변환 코드나 정적 대응표는 사용하지 않는다.
