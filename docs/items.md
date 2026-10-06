# 아이템 계약

#148의 공통 계약이다. 골드·에너지는 재화, 스킬별·장비별 마법북은 아이템으로 관리한다. 콘텐츠 정의는 `ItemDefinition`, 마법북의 강화 비용·드랍·보상·소유량은 `ItemAmount`를 사용한다. Local 지급 API는 구현했으며 드랍 확률과 획득 방식은 후속 기능에서 정한다.

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

조회는 `PlayerProfile.ItemQuantity(itemId)`를 사용한다. 스냅샷 적용은 기존처럼 전체 교체다. 전투 중 획득 내역은 영구 소유량과 별도이며, #150에서 관리한다. 정의 존재 여부, 중복 지급 방지 및 저장 반영은 아래 Local 지급 API가 담당하고, #149에서 드랍 흐름과 연결한다.

## Local 지급 API

`IItemApi.Grant(items, idempotencyKey)`를 `LocalItemApi`로 구현하고 Root 스코프에 등록했다. 반환값은 지급 반영 후 최신 `PlayerSnapshot`이다. 호출자는 응답을 `PlayerProfile.Apply`에 전달해 캐시를 갱신한다.

```csharp
var snapshot = await itemApi.Grant(new[]
{
    new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 2 },
    new ItemAmount { itemId = ItemIds.WeaponBook, quantity = 1 }
}, "battle-123:drop-456");
profile.Apply(snapshot);
```

- Items 테이블에 있는 ID와 양수 수량만 받는다. 골드·에너지는 지급 목록에 포함할 수 없다.
- 한 요청 안의 같은 ID는 수량을 합산한다. 모든 검증과 소유량 계산이 성공한 뒤 한 번 저장하므로 거절된 요청의 일부만 지급하지 않는다.
- `LocalSave.itemGrants`에 지급 키와 정규화된 요청 내용을 저장한다. 재실행 후에도 같은 키·같은 내용은 재지급하지 않고 최신 스냅샷을 반환한다. 목록 순서나 같은 ID의 분할 방식은 중복 판단에 영향을 주지 않는다.
- 같은 키로 다른 지급 내역을 보내면 거절한다. 요청 키는 아이템 지급 작업 안에서 고유해야 하며, 골드 변동용 `ledger`와 분리한다.
- Local 거절 코드는 `INVALID_REQUEST`, `UNKNOWN_DATA_ID`, `IDEMPOTENCY_CONFLICT`, `ITEM_QUANTITY_OVERFLOW`다. 신규 지급 API와 오류 코드는 Local 구현 계약이며 서버 명세 확정을 뜻하지 않는다.
- 이 API는 드랍 시점이나 전투 결과를 결정하지 않는다. 서버 구현은 서버가 검증·확정한 지급 내역을 사용하도록 별도 계약으로 연결한다.

## 후속 구현 경계

- #149: 마법북 드랍 테이블은 정의 및 `ItemAmount`를 재사용하고 Local 지급에는 `IItemApi`를 사용한다. 골드 지급은 기존 재화 API를 사용한다. 지급 확정 시점과 실패·포기 정책은 드랍 연결 전에 결정한다.
- #150: 획득 가능 목록은 아이템 ID 목록, 실제 획득 내역은 `ItemAmount` 목록으로 구분한다. 집계 결과는 ID당 한 행이다.
- #151: 정의의 이름과 아이콘 키를 사용한다. 실제 아이콘 조회 테이블은 UI 연결 시 구성한다.
- Items 테이블은 `GameDataStore.TableNames`에 포함되어 버전·원문 조회에서도 제공한다. 정의의 필수 값은 `ItemDefinition.Validate()`로 확인한다. 강화 대상과 실제 아이콘 에셋 참조 검증은 해당 기능 연결 때 추가한다.
- 결과 제출 DTO의 확장은 지급 시점과 서버 계약이 결정된 뒤 별도 계약 변경으로 처리한다. 현재 골드 지급·강화 동작은 그대로다.
