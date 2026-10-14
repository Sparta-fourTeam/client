# 서버 스킬 데이터 계약

클라이언트의 `Resources/MockData/Skills.json`, `Resources/ClientData/DisplayTexts.json`, `Data/SkillAssetTable.asset`은 서버가 제공한 스킬·카드 ID를 그대로 사용한다. 별도의 ID 변환 코드나 대응표는 두지 않는다. 이름·설명은 클라이언트 표시 표에서 같은 ID로 찾는다. 서버가 이름·설명을 보내도 클라이언트 표가 화면 문구를 결정한다.

`castType`은 `Projectile`, `Instant`, `Area`, `Beam`, `Chain`이고, `projectilePath`는 `Aimed`, `Cloud`, `Radial`이다. `Instant`는 벼락의 즉시 타격, `Cloud`는 나무뿌리의 방벽 앞 굴러가는 경로다. 몬스터의 `CastResists` 키도 같은 `castType` 이름을 사용한다.

서버 JSON의 카드 ID는 숫자로 보낸다. 클라이언트는 일반 카드와 공유하는 문자열 식별 타입에 그 숫자의 문자열 표현을 그대로 담는다. 예를 들어 서버 카드 `100`은 게임에서도 `"100"`이며 다른 ID로 바뀌지 않는다.

## 선행 조건 생략 규칙

- `requiredWeaponIds`: 스킬 보유 조건. 조건이 없으면 필드 자체를 생략한다.
- `requiredCardCounts`: 카드 선택 조건. 조건이 없으면 필드 자체를 생략한다.
- `requiredCardCounts[].cardId`: 필요한 카드 ID라서 생략하지 않는다.
- `requiredCardCounts[].skillId`, `exclusions[].skillId`, `mirrorCards[].skillId`는 제거한다. `cardId`는 전체 스킬에서 유일하며, 클라이언트가 로드 때 소속 스킬을 찾는다.
- `requiredCardCounts[].count`: 1회 조건이면 생략한다(기본 1). 2회 이상일 때만 지정한다.

```json
{
  "requiredWeaponIds": [4],
  "requiredCardCounts": [
    { "cardId": 2 },
    { "cardId": 51, "count": 2 }
  ]
}
```

위 두 배열은 의미가 다르므로 합치지 않는다. 배타 조건은 `{ "cardId": 11, "belowPermanentLevel": 0 }`, 카드 효과 복제는 `[{ "cardId": 1 }]`로 표현한다. `belowPermanentLevel`의 해제 기준은 배타 조건을 가진 스킬의 영구 레벨이다. 클라이언트가 계산한 `OwnerSkillId`는 JSON에 싣지 않는다.

카드 81의 `requiredWeaponIds: [4]`는 제거한다. `requiredCardCounts: [{"cardId":52}]`가 서리 결정의 선행 카드 선택을 요구하므로 스킬 보유 조건도 충족한다. 다른 카드의 스킬 보유 조건은 유지한다.

`effects[].skillId`는 시전할 자식 스킬을 지정하는 별도 정보이므로 유지한다.

## 서버에 전달할 확인 요청

> 스킬·카드 ID, 프리팹·아이콘·표시 문구 연결과 enum 표기는 클라이언트를 서버 계약에 직접 맞췄습니다. ID·표기 수정은 서버에 요청하지 않습니다.
>
> `requiredCardCounts`, `exclusions`, `mirrorCards`의 `skillId`는 삭제해주세요. 유일한 `cardId`로 소속 스킬을 찾도록 클라이언트를 수정했습니다. `count`는 1회면 생략 가능합니다. 카드 81의 `requiredWeaponIds: [4]`도 삭제하고 선행 카드 52 조건은 유지해주세요. `effects[].skillId`와 다른 카드의 `requiredWeaponIds`는 유지합니다.
>
> 다만 아래 전투 규칙이 제공 데이터에서 빠져 있습니다. 의도적인 변경인지 누락인지 확인 부탁드립니다. 누락이면 현재 클라이언트 `Skills.json`의 필드를 서버 데이터에 포함해주세요. 클라이언트에서 누락분을 임의로 보충하지 않습니다.
>
> 1. 모든 스킬의 `element`: 무·화·빙·뇌·토 속성과 적의 약점·저항 계산에 필요합니다.
> 2. 스킬 보유 조건 `requiredWeaponIds`: 아래 카드에 필요합니다.
> 3. 자식 효과의 `inherit`: 부모의 공격력·속도·상태이상 등을 시전 시점에 상속하는 규칙입니다. `damageScale`로 대체할 수 없습니다.
> 4. 집중 화살(스킬 17)의 `mirrorCards`: `[{"cardId":1}]`이 필요합니다.
> 5. 나무뿌리(스킬 5)의 `baseStats.projectile.collisionRadius: 0.45`, 화살 조각(스킬 10)의 `baseStats.projectile.sizeMultiplier: 0.5`, 집중 광선(스킬 16)의 `baseStats.beam.focusAim: 1`이 필요합니다.
>
> 벼락(스킬 3)의 `baseStats.cast.impactRadius: 0.7`도 명시해주세요. 현재 기본값과 같아 당장 동작 차이는 없지만 밸런스의 주인은 서버입니다. 테이블 구조가 바뀌면 `Skills` 개정 번호도 올려주세요.

### 누락된 스킬 보유 조건

| 카드 ID | requiredWeaponIds |
| --- | --- |
| 7 | `[4]` |
| 8 | `[2]` |
| 11 | `[3]` |
| 22 | `[13]` |
| 23 | `[16]` |
| 48, 49 | `[15]` |
| 62 | `[6]` |
| 71 | `[2]` |
| 79, 80 | `[4]` |
| 90 | `[3]` |
| 91 | `[15]` |
| 103 | `[1]` |
| 104 | `[4]` |
| 105 | `[16]` |
| 115, 116 | `[3]` |
| 123, 125 | `[1]` |
| 126 | `[13]` |

### 누락된 상속 규칙

`effects`의 인덱스는 0부터 시작한다. 값은 현재 `Skills.json`의 전투 규칙이다.

| 카드 ID | 효과 인덱스 | inherit |
| --- | --- | --- |
| 12 | 0 | `{"damage":0.5,"projectileSpeed":1}` |
| 20 | 0 | `{"explosionRadius":1,"explosionDamage":0.5}` |
| 33 | 0 | `{"damage":0.5,"projectileSpeed":1}` |
| 41 | 0 | `{"damage":0.5,"paralysisChance":1}` |
| 56 | 0, 1 | `{"damage":0.5,"projectileSpeed":1,"frostbiteChance":1}` |
| 61 | 1 | `{"damage":0.5,"projectileSpeed":1,"pierceCount":1,"freezeDuration":1,"freezeChance":1,"knockbackDistance":1,"frostbiteRatio":1,"frostbiteChance":1}` |
| 100 | 0 | `{"damage":0.5}` |

스킬 목록과 이름·속성·수치의 기준 파일은 `Assets/_Project/Resources/MockData/Skills.json`과 `Assets/_Project/Resources/ClientData/DisplayTexts.json`이다. 서버의 테이블 본문 조회 API 연결은 별도의 엔드포인트 계약이 필요하다. 이 문서는 스킬 테이블의 ID·필드 계약을 정의한다.
