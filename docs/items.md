# 아이템 계약

#148의 공통 계약이다. 골드·에너지는 재화, 스킬별·장비별 마법북은 아이템으로 관리한다. 콘텐츠 정의는 `ItemDefinition`, 마법북의 강화 비용·드랍·보상·소유량은 `ItemAmount`를 사용한다. Local 보상 API의 아이템 지급을 구현했으며 몬스터 드랍 확률과 획득 방식은 후속 기능에서 정한다.

## 식별과 수량

- 아이템은 문자열 ID 하나로 식별한다. 화살북과 화염구북, 모자북과 반지북은 각각 다른 ID와 소유량을 가진다. 코드에서는 `ItemIds.ArrowBook` 같은 문자열 상수로 참조해 오타를 줄인다. 상수는 해당 ID의 이름이며 별도 식별자가 아니다. enum은 두지 않는다.
- 스킬북은 현재 `Skills.json`의 독립 스킬 9종 기준이다: 화살, 화염구, 벼락, 서리 결정, 나무뿌리, 냉기 지대, 전기 구름, 에너지 빔, 연쇄 번개. `childOnly` 보조 효과에는 별도 마법북을 만들지 않는다.
- 장비북은 모자, 상의, 신발, 무기, 반지, 넥타이, 사원증으로 구분한다. 장비의 단검 슬롯은 무기로 표현한다. Local 테이블의 장비 `TargetId`는 `equipment.weapon` 같은 슬롯 키다. 실제 장비 강화 연결은 장비 계약에서 확정한다.
- `ItemDefinition.Id`는 저장·API에서 사용하는 콘텐츠 고유 ID다. 각 종류는 서로 다른 ID와 소유량을 가진다.
- `TargetId`는 마법북으로 강화할 스킬 또는 장비의 ID다. 아이템 자체의 식별자와 강화 대상의 식별자는 구분한다.
- `Name`은 표시 이름, `IconKey`는 아이콘 조회 키다. Unity 오브젝트를 API나 저장 데이터에 넣지 않는다.
- `ItemAmount.itemId`는 정의 ID, `quantity`는 0 이상의 절대 수량이다. 소비는 음수 수량 대신 소비 동작으로 표현한다. 실제 드랍·지급 행은 양수만 사용한다.

`Resources/MockData/Items.json`에 정의를 등록하고 `GameDataStore.Items`에서 조회한다. 아이콘 키와 실제 스프라이트의 연결은 UI 구현에서 구성한다. 정의 예시:

```json
[
  { "Id": "book.arrow", "Name": "화살 마법북", "IconKey": "ArrowBook", "TargetId": "1" },
  { "Id": "book.fireball", "Name": "화염구 마법북", "IconKey": "FireballBook", "TargetId": "2" },
  { "Id": "book.weapon", "Name": "무기 마법북", "IconKey": "WeaponBook", "TargetId": "equipment.weapon" }
]
```

수량 예시:

```json
[
  { "itemId": "book.arrow", "quantity": 2 },
  { "itemId": "book.fireball", "quantity": 5 }
]
```

## 소유량 저장

스냅샷은 기존 최상위 `gold`, `energyStored`, `energyUpdatedAt` 필드를 유지한다. 로컬 저장 내부에서는 기존 `LocalSave.wallet`(`WalletRow`)을 유지하고 `LocalPlayerApi`가 스냅샷으로 변환한다. 두 모델의 구조를 같게 만들 필요는 없다. 골드는 아이템 ID를 갖지 않으며 아이템 정의·소유량·비용 목록에 넣지 않는다. 강화 비용과 보상에서 골드는 재화 필드, 마법북은 `ItemAmount`로 각각 표현한다.

`PlayerSnapshot.items`와 `LocalSave.items`는 마법북 소유량이다. `itemId`당 한 행만 저장하고, 행이 없으면 소유량 0으로 읽는다. 기존 세이브는 이미 `wallet`을 사용하므로 재화 이관은 필요 없다. 누락된 `items`는 빈 목록으로 시작한다. null 아이템 목록도 로컬 스냅샷 변환과 `PlayerProfile.Apply`에서 빈 목록으로 처리한다.

스냅샷 예시:

```json
{
  "gold": 100,
  "energyStored": 5,
  "energyUpdatedAt": "2026-10-06T00:00:00Z",
  "items": [ { "itemId": "book.arrow", "quantity": 2 } ],
  "stageProgress": [],
  "upgrades": []
}
```

새 재화는 필요해질 때 저장과 스냅샷에 필드를 추가한다. `PlayerProfile.Gold`, `EnergyStored`, `EnergyUpdatedAt` 조회와 기존 응답 필드 위치는 유지한다.

조회는 `PlayerProfile.ItemQuantity(itemId)`를 사용한다. 스냅샷 적용은 기존처럼 전체 교체다. 전투 중 획득 내역은 영구 소유량과 별도이며, #150에서 관리한다. 소유량 반영은 기존 보상 API 내부에서 처리하고, #149에서 몬스터 드랍 흐름과 연결한다.

## 보상 API의 아이템 지급

아이템 지급을 위한 별도 공개 API는 두지 않는다. `IPlayerApi.GetMe()`는 저장된 소유량을 조회하고, 실제 지급은 기존 보상 처리 안에서 수행한다.

- `IStageApi.ClaimRatingReward(stageId)`: 아직 받지 않은 별 등급의 골드·마법북을 함께 지급하고 최신 `PlayerSnapshot`을 반환한다. `claimedRating`으로 중복 수령을 막으며 이미 받은 보상을 다시 요청하면 기존처럼 `NO_REWARD`다.
- `IBattleApi.SubmitResult(request)`: 클리어 골드와 마법북을 확정·저장하고 `SubmitResultResponse.rewardGold`, `rewardItems`로 반환한다. 전투 기록에 처음 확정된 아이템 목록도 보관하므로 재실행·재제출·테이블 변경 후에도 같은 `battleId`에는 같은 결과를 반환하며 재지급하지 않는다.
- 골드는 기존 지갑, 마법북은 `LocalSave.items`에 저장한다. 전체 검증·계산이 성공한 뒤 보상과 수령 상태를 같은 세이브에 저장한다.
- `LocalItemRewards`는 두 Local API가 공유하는 내부 검증·합산 함수다. 별도 DI 등록이나 아이템 지급 원장은 없다.
- 알려진 Items ID와 양수 수량만 받으며 같은 ID는 합산한다. 잘못된 보상은 `INVALID_ITEM_REWARD`, 없는 ID는 `UNKNOWN_DATA_ID`, 수량 초과는 `ITEM_QUANTITY_OVERFLOW`로 거절한다.

Stage 테이블의 `ClearItems`는 클리어 마법북, `RatingItemRewards`는 등급별 1회성 마법북 목록이다. 누락되거나 빈 목록이면 아이템 보상 없이 기존 골드 동작을 유지한다. 설정 형식 예시:

```json
{
  "ClearItems": [ { "itemId": "book.arrow", "quantity": 2 } ],
  "RatingItemRewards": [
    [ { "itemId": "book.weapon", "quantity": 1 } ],
    [],
    []
  ]
}
```

위 수량은 설정 예시이며 현재 Stages 콘텐츠에 임의 보상 수량을 추가하지 않았다. 실제 보상량은 콘텐츠 정의에서 결정한다. 결과 제출 응답의 아이템 필드는 Local 구현 계약이며 서버 명세 확정을 뜻하지 않는다. 몬스터별 드랍의 검증·전투 결과 연동은 #149에서 추가한다.

## 스테이지 조회·집계

`StageItemCache`는 `IStageItemInfo`로 현재 스테이지 ID, 획득 가능 ID 목록, 실제 획득량을 제공한다. UI는 조회 계약만 의존하므로 모의 구현으로도 작업할 수 있다.

- 전투 시작·재시작·전환 시 `Begin(stageId, obtainableItemIds)`을 호출한다. 같은 스테이지라도 누적 내역을 비우고 가능 목록을 교체한다.
- 가능 목록은 중복 ID를 제거하고 호출자가 가진 목록과 분리해서 보관한다. 실제 획득 내역과는 독립적이다.
- 획득 처리에서 확정한 뒤 `RecordAcquired(itemId, quantity)`로 양수 수량을 기록한다. 같은 아이템은 합산한다. 사망·획득 이벤트 중복 방지는 #149의 획득 처리에서 담당한다.
- 획득 내역 조회는 복사본을 반환한다. UI나 결과 요청이 DTO를 수정해도 캐시의 집계 값은 변하지 않는다.
- 영구 소유량, 골드, 지급 API는 캐시에서 변경하지 않는다.

현재 조회·집계 모듈과 테스트까지 구현했다. 드랍 테이블에서 가능 목록을 계산하는 공급자, Stage 스코프의 `Begin` 연결, 실제 획득 콜백 연결은 #149의 계약 확정 후 연결한다. 이 연결 전에는 #150의 실제 스테이지 연동 완료로 간주하지 않는다.

## 후속 구현 경계

- #149: 마법북 드랍 테이블은 정의 및 `ItemAmount`를 재사용한다. 지급은 기존 보상 처리 안에서 확정하며 몬스터별 드랍 검증과 실패·포기 정책은 드랍 연결에서 정의한다.
- #150: 획득 가능 목록은 아이템 ID 목록, 실제 획득 내역은 `ItemAmount` 목록으로 구분한다. 집계 결과는 ID당 한 행이다.
- #151: 정의의 이름과 아이콘 키를 사용한다. 실제 아이콘 조회 테이블은 UI 연결 시 구성한다.
- Items 테이블은 `GameDataStore.TableNames`에 포함되어 버전·원문 조회에서도 제공한다. 정의의 필수 값은 `ItemDefinition.Validate()`로 확인한다. 강화 대상과 실제 아이콘 에셋 참조 검증은 해당 기능 연결 때 추가한다.
- 전투 제출 응답에 `rewardItems`를 추가했다. 기존 골드 지급·강화 동작은 유지한다. 서버 연결과 몬스터 드랍 요청의 검증 계약은 별도 확정한다.
