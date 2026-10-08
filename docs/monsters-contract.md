# 몬스터·스킬 데이터 계약

서버(구글 시트와 서버)가 클라이언트에 주는 몬스터와 스킬 데이터의 규칙을 정리한다. 2026-10-07 기준이다. "현재 동작"은 코드를 실행해 확인한 것이고, **상태가 "제안"인 항목은 팀 합의 전이다.** 합의되면 상태를 "확정"으로 바꾼다. 필드 하나하나의 뜻은 [enemy.md](enemy.md)의 "Monsters.json 필드"와 "Passives"를, 스킬 필드는 [skills.md](skills.md)를 본다.

## 범위

| 데이터 | 누가 정하나 | 비고 |
|---|---|---|
| `Monsters` | 서버 | 수치, 등급, 능력(`Passives`, `Resists`, `CastResists`, `BlocksProjectile`) |
| `Stages`의 `Waves[].Spawns[]` (`MonsterId`, `Count`) | 서버 | 웨이브마다 나오는 몬스터와 마릿수. 엘리트·보스는 `Count` 1. 분열·소환 전용 몬스터는 넣지 않는다. 최상위 `MonsterIds`는 없고 웨이브 구성에서 모은다 |
| `Skills`의 `element` | 서버 | 스킬 속성 |
| `MonsterAssetTable`(프리팹) | **클라이언트** | `MonsterId` → 프리팹. 서버는 모른다 |
| `MonsterDisplayTable`(이름, 아이콘) | **클라이언트** | 이름은 아직 고정 텍스트. 로컬라이징이 들어오면 키로 바꾼다 |

서버는 `Id`와 수치와 능력만 관리하고, 어떤 아트를 쓰는지는 클라이언트가 `MonsterId`로 해결한다(`AssetKey` 같은 필드를 서버 계약에 두지 않는다).

## 클라이언트가 데이터를 읽는 방식 (현재 동작)

| 상황 | 동작 |
|---|---|
| JSON 키의 대소문자 | 구분하지 않는다(`id`와 `Id` 모두 읽힌다) |
| 모르는 필드가 들어옴 | 조용히 무시한다. 예전 클라이언트는 새 필드를 모르는 채로 동작한다 |
| 필드가 빠짐 | 기본값을 쓴다(`BurstCount` 1, `Chance` 1, `SpeedMultiplier` 1, 목록·표는 비어 있음) |
| `Passives`가 `null` | 비어 있는 것으로 본다 |
| `Passives[].Kind`가 모름 | **부팅 예외**로 멈춘다(`Monsters <Id>: 알 수 없는 패시브 Kind입니다`). `Kind`는 대소문자를 구분한다 |
| `Trigger`, `Statuses`, `Target` | 이름으로 읽고 대소문자를 구분하지 않는다. 숫자나 빈 값, `None`은 거부한다 |
| `Resists`, `CastResists`의 키, `HealOnHit.Element` | **정확한 영문 이름**이어야 한다(대소문자 구분). 숫자와 모르는 이름은 거부한다 |
| 값 범위를 벗어남(체력 0, 확률 0, 비율 -1 미만 등) | 부팅 예외 |
| 소환 대상이 없거나 자기 자신이거나 다시 소환함 | 부팅 예외(연쇄 소환 금지) |
| 웨이브 구성(`Spawns`)에 `Monsters`에 없는 몬스터, 엘리트·보스의 `Count`가 1이 아님, 빈 구성·0마리·웨이브 안 중복 | 부팅 예외 |
| `Skills.element` | 숫자(0~5)와 이름(`Fire` 등) 둘 다 읽는다. 모르는 이름이나 범위 밖 숫자는 로드 실패 |

## 필드 규칙

- **이름 규칙**: `Monsters`, `Stages`, `StageRewards`, `Items`, `Upgrades`, `GeneralCards`, `PlayerLevels`, `Energy`는 PascalCase이고 `Skills`만 camelCase다. 표마다 자기 스타일을 지킨다.
- **속성 이름**: `Neutral`, `Fire`, `Ice`, `Wind`, `Lightning`, `Earth`. 스킬의 `element`는 같은 순서의 숫자 0~5다.
- **시전 형태 이름**: `Projectile`, `Hitscan`, `Area`, `Beam`, `Chain`. 스킬의 `castType`은 같은 순서의 숫자 0~4다.
- **패시브 Kind**: `Spawn`, `Immunity`, `Modifier`, `Shield`, `Evade`, `LowHp`, `HealOnHit`, `SpeedBoost`.
- **면역 이름**: `Stun`, `Burn`, `Paralysis`, `Freeze`, `Slow`, `Frostbite`, `Vulnerability`, `AllDebuffs`.
- `Exp`, `GaugeValue`는 클라이언트가 읽지 않는다(보상은 `StageRewards`). 서버가 지워도 클라이언트는 영향이 없고, 있어도 무시된다.

## 결정이 필요한 항목

| # | 항목 | 현재 동작 | 제안 | 상태 |
|---|---|---|---|---|
| 1 | 클라이언트가 모르는 `Kind`를 받으면 | 부팅 예외로 멈춤 | **유지(빠른 실패).** 모르는 능력을 조용히 빼면 몬스터가 약해진 채로 나가서 눈치채기 어렵다. 대신 **클라이언트 먼저, 데이터 나중** 순서로 배포한다(새 `Kind`는 그것을 아는 클라이언트가 나간 뒤에 서버 데이터에 쓴다) | 제안 |
| 2 | 필드 이름 규칙 | 표마다 다름(`Skills`만 camelCase) | **표별 기존 스타일 유지, 새 표는 PascalCase.** 클라이언트는 대소문자를 구분하지 않고 읽으니 어긋나도 깨지지는 않지만, 서버는 표 스타일을 지킨다 | 제안 |
| 3 | 참조 검증(소환 대상, 스테이지 몬스터 등)을 누가 하나 | 클라이언트가 부팅 때 검증 | **둘 다 한다.** 서버가 시트를 반영할 때 같은 규칙으로 막고, 클라이언트 검증은 마지막 방어선으로 둔다. 규칙 목록은 위 표와 [enemy.md](enemy.md)의 "검증과 테스트" | 제안 |
| 4 | `element`를 숫자로 줄지 이름으로 줄지 | 둘 다 읽음, 지금 데이터는 숫자 | **숫자 유지**(`castType`과 같은 방식). 대신 숫자의 의미를 **바꾸지 않는다**(항목은 뒤에만 더한다) | 제안 |
| 5 | `Exp`, `GaugeValue` | 읽지 않음 | 서버 표에서 **제거**(클라이언트 영향 없음). 클라이언트 쪽 필드 정리는 별도 PR | 제안 |
| 6 | 테이블 개정 번호(`DataVersions.revisions`) | 클라이언트가 전부 1로 고정(TODO) | 서버가 표별 개정 번호를 실제로 주고, **스키마가 바뀌는 변경은 개정 번호를 올린다.** 클라이언트가 지원하지 않는 개정이면 로드하기 전에 거부할 수 있다 | 제안(서버 구현 필요) |

## 시트 컬럼 (Monsters 탭)

`Monsters` 탭은 아래 컬럼을 가진다(시트는 담당자가 직접 수정한다). 중첩 값은 **JSON 문자열 셀**로 두는 것을 제안한다(서버가 파싱하고 검증). 시트에서 서버, 서버에서 클라이언트로 가는 변환 방식은 서버가 정한다.

| 컬럼 | 타입 | 필수 | 예 |
|---|---|---|---|
| `Id` | int | 필수 | `21` |
| `Hp` | int | 필수 | `60` |
| `Damage` | int | | `3` |
| `Speed` | float | | `0.06` |
| `AttackInterval` | float | 필수 | `2.5` |
| `AttackRange` | float | | `0` |
| `ProjectileSpeed` | float | | `0` (0보다 크면 원거리) |
| `IsBoss` | bool | | `FALSE` |
| `IsElite` | bool | | `FALSE` |
| `BurstCount` | int | | 비우면 1 |
| `BurstInterval` | float | | 비움 |
| `Resists` | JSON 문자열 | | `{"Lightning": -1}` |
| `CastResists` | JSON 문자열 | | `{"Projectile": -0.7}` |
| `BlocksProjectile` | bool | | `FALSE` |
| `Passives` | JSON 문자열 | | `[{"Kind": "Shield", "Trigger": "Start", "Hits": 6}]` |

`Exp`, `GaugeValue` 컬럼은 제거 대상이다(위 결정 5).

## 필드를 더하거나 바꿀 때

1. [enemy.md](enemy.md)의 필드 표와 이 문서의 읽는 방식, 시트 컬럼 표를 같이 고친다.
2. 클라이언트는 모르는 필드를 무시하므로, **새 필드는 서버보다 클라이언트를 먼저 내보낸다.** 새 필드를 서버가 먼저 내보내면 예전 클라이언트가 조용히 무시해서 능력이 빠진 채로 나간다.
3. 새 패시브 `Kind`는 클라이언트가 모르면 부팅이 멈추므로 결정 1의 배포 순서를 지킨다.
4. 값 범위 검증을 코드(`MonsterDefinition.Validate`, `PassiveDefinition.Validate`)와 테스트에 같이 더한다.
