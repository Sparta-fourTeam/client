# Enemy 설계

Enemy 기능(패시브, 속성, 상태이상 면역 등)을 늘리기 위한 구조 설계다. 2026-10-07 기준이며 구현 전 합의용 문서다. 코드가 원본이고, 구현되면 이 문서의 "현재"와 "목표"를 맞춘다.

- 이름과 컨셉은 게임에 맞게 바꾼다. 코드는 컨셉과 무관한 이름(`Spawn`, `Immunity`, `Shield`)을 쓰고, 표시 이름과 설명, 아이콘은 표시용 데이터에만 둔다.
- 스킬 속성 용어는 [ninjutsu/README.md](ninjutsu/README.md)의 "원문 용어 대응"을 따른다(무/화/빙/풍/뇌/토속성).

## 1. 기준: 기본 규칙과 시스템

| 구분 | 기준 | 담는 것 |
|---|---|---|
| 기본 규칙 | 모든 적이 항상 따른다. 몬스터마다 달라지지 않고 클래스 구조가 바뀌지 않는다 | HP와 사망, 피해 파이프라인, 속도, 공격 타이머, 속성/시전 형태 저항 표 |
| 시스템 | 몬스터마다 켜지거나 꺼지고 종류가 계속 늘어난다. 자기 상태를 갖고 틱이나 훅으로 돈다 | 상태이상, 패시브, 스폰, 타깃팅, 연출 |

속성 약점/저항은 몬스터 대부분이 갖는 숫자 표라서 패시브가 아니라 기본 규칙의 한 단계로 둔다.

## 2. 클래스 구조

```
Enemy (MonoBehaviour)            위치, 이동 적용, 밀치기 적용, 연출, IEnemyTarget 구현
 └─ EnemyModel (일반 C#)         기본 규칙. 위치를 모른다
      ├─ EnemyStatus             상태이상 전부
      ├─ DamageProfile           속성/시전 형태 저항, 투사체 차단 (데이터에서 만든 불변 객체)
      └─ IPassive 목록           패시브 (Monsters.json의 Passives)
```

- `EnemyModel`은 일반 C# 클래스로 둔다. 엔진 독립이 목적이 아니라 (1) GameObject 없이 규칙을 빠르게 테스트하고 (2) 규칙이 늘어도 `Enemy`가 비대해지지 않고 (3) 사망 연출 중에도 로직은 이미 끝난 상태를 유지하기 위해서다.
- 클래스는 5종(`Enemy`, `EnemyModel`, `EnemyStatus`, `DamageProfile`, `IPassive`)에서 더 잘게 쪼개지 않는다.

### 위치와 이동

위치의 단일 출처는 `Enemy`(Transform)다. 물리, 충돌, 애니메이션 이동이 들어와도 구조가 바뀌지 않게 하기 위해서다. 구현됐다.

- 모델은 "얼마나 빨리 움직이는가"(`MoveSpeed`, 감속 반영이고 죽었거나 빙결·마비·기절이면 0)만 정하고 `Enemy`가 그대로 적용한다(`Enemy.Move`). 사거리 판정과 공격은 위치를 인자로 받는다(`EnemyModel.IsInAttackRange(position, wall)`, `Attack(dt, position, wall, projectiles)`).
- 밀치기: 모델이 거리를 정하고(`ResolveKnockback`) 적용은 `Enemy`가 한다. 저항 비율은 5단계에서 이 자리에 넣는다.
- 소환 요청: 모델은 부모 기준 오프셋(`RequestSpawn(monsterId, offset)`)을 알리고, `Enemy`가 자기 위치를 더한 `EnemySpawnRequest`를 `EnemySpawner`에 넘긴다.
- 점화 사망 폭발: 모델이 `DeathExplosionRequested`로 알리면 `Enemy`가 자기 위치로 콜백을 부른다.
- `Enemy`가 `IEnemyTarget`과 상태이상 대상 인터페이스(`IFreezableTarget` 등)를 구현해 모델에 위임한다. `EnemySpawner`, `IEnemyFactory`, 스킬 대상은 모두 `Enemy`를 직접 쓴다(`EnemyModel`을 직접 다루지 않는다).
- `IEnemyTarget.IsDead`는 기본 구현(항상 false)을 둔 인터페이스 멤버다. 스킬 코드는 구체 타입을 검사하지 않고 `target.IsDead`로 죽은 대상을 건너뛴다.
- 오브젝트가 파괴된 뒤에도 남은 참조(스킬 효과 등)가 위치를 읽을 수 있게 `Enemy.Position`은 마지막 위치를 돌려준다.
- [architecture.md](architecture.md)의 규칙과 맞다: 로직은 뷰를 모르고, 뷰는 로직을 직접 호출하며, 위치처럼 매 프레임 바뀌는 값은 뷰가 가진다.

## 3. 피해 파이프라인

모든 피해(점화와 동상의 틱 피해 포함)는 `DamageInfo`(양, 속성, 시전 형태, 충격 여부)로 아래 순서를 지난다.

1. 패시브 `OnBeforeDamage`: 무적, 회피, 방어막 (피해를 0으로 만들거나 줄인다). 목록 순서대로 이어서 줄이고, 앞에서 막히면 뒤는 부르지 않는다
2. `DamageProfile`: 속성 저항, 시전 형태 감쇠, 투사체 충격 면역
3. `EnemyStatus`: 취약 배율
4. HP 차감
5. 패시브 `OnDamaged`: 회복, 이속 증가 (살아남았을 때만. 막힌 피해와 죽은 피해는 부르지 않는다)
6. 사망이면 패시브 `OnDied`

기존 `TakeDamage(int)`는 무속성 오버로드로 남겨 호출부와 테스트를 보존한다.

## 4. 시스템별 요구사항

### 기본 규칙
- 모든 적은 HP, 속도, 근접 또는 원거리 공격을 가진다.
- 소환체의 사망은 웨이브 게이지에 세지 않고(`EnemyDied.IsSummoned`) 전투 통계에는 센다.
- 연속 공격(한 번의 공격에 여러 발)은 `Monsters.json`의 `BurstCount`(생략하면 1)와 `BurstInterval`(초)이다. 첫 타격은 공격 간격이 되면 나가고 나머지는 `BurstInterval`마다 나간다. 연속 공격은 한 번의 공격 간격 안에 끝나야 한다(`AttackInterval > BurstInterval * (BurstCount - 1)`). 기절·빙결·마비 중에는 멈춘다. 근접은 타격마다 벽을 때리고 원거리는 타격마다 투사체를 쏜다.

### 상태이상 (`EnemyStatus`)
- 있는 것: 빙결, 마비, 기절, 감속, 범위 감속, 취약, 점화, 동상. 기절 면역은 구현됨.
- 면역 종류는 `Stun`, `Burn`, `Paralysis`, `Freeze`, `Slow`, `Frostbite`, `Vulnerability`이고 `AllDebuffs`는 이 전부다. `Passives`의 `Immunity`의 `Statuses`에 이름으로 적는다(대소문자 구분 없음, 숫자는 거부). `EnemyStatus`의 `Apply*` 입구에서 막는다.
- 효과의 크기는 `Passives`의 `Modifier`로 바꾼다. `Target`은 `KnockbackDistance`(밀치기 거리)와 `BurnDuration`(점화 지속시간), `Multiplier`는 받는 효과에 곱하는 배수다(0.5면 절반, 0이면 무효, 4면 4배). 같은 대상이 여러 개면 곱한다. 예: `{"Kind": "Modifier", "Target": "BurnDuration", "Multiplier": 4}`. 밀치기 저항에 별도의 면역 단계는 없고 `Multiplier` 0이 면역이다. `Modifier`는 패시브 객체가 아니라 `EffectModifiers` 값으로 모델에 들어간다.
- **동적 면역**(구현됨): 방어막 보유 중, 체력 조건 발동 중처럼 조건부이거나 일시적인 면역은 `EnemyStatus.GrantImmunity(source, flags, duration)`로 출처별로 걸었다 푼다(`duration`이 0이면 직접 풀 때까지, 아니면 시간이 지나면 자동으로 풀린다). 처음부터 있는 면역(`Immunity` 패시브)과 합쳐서 본다. 확률 면역(카라카사)은 `OnStatusApply` 훅 대신 회피가 성공할 때 0.1초짜리 `AllDebuffs` 면역을 거는 방식으로 풀어서 그 훅은 만들지 않았다.
- 이동 속도 배율도 출처별이다(`AddSpeedBoost(source, multiplier, duration)`). 감속(`MovementMultiplier`)과 따로 곱해서 `MoveSpeed`가 된다.

### 패시브 (`IPassive`)
- 훅: `OnSpawn`(생성 직후 한 번), `OnTick`, `OnBeforeDamage`, `OnDamaged`, `OnDied`. 피해 훅은 `DamageInfo`를 받는다. 상태이상 지속 피해(스킬 밖)도 `OnBeforeDamage`로 오니 스킬 피해만 보려면 `info.IsFromSkill`로 거른다. 한 번의 공격에서 "적중"은 `IsImpact`(직접 피해) 하나다. 폭발이나 장판 같은 부가 피해는 적중으로 세지 않는다.
- `Spawn`(`Trigger` = `OnDeath` 분열, `Interval` 주기 소환)
- `Shield`: `Trigger`(`Start` 처음부터, `OnHit` 맞은 뒤 `Chance` 확률로), `Hits`(막는 적중 횟수), `Duration`(0이면 횟수를 다 막을 때까지, 아니면 이 시간 뒤에도 사라짐), `Statuses`(켜져 있는 동안 걸리지 않는 상태이상, 선택). 켜져 있는 동안 스킬 피해는 부가 피해까지 막고 적중만 횟수에 센다. 상태이상 지속 피해는 막지 않는다. 한 번만 켜진다.
- `Evade`: `Chance`. 적중(`IsImpact`)을 확률로 피한다. 피하면 같은 공격의 부가 피해와 상태이상도 0.1초간 막는다. 지속 피해는 피하지 않는다.
- `LowHp`: `HpRatio`(이하가 되면 발동), `Duration`(효과가 켜져 있는 시간), 효과 `HealRatio`(그 시간 동안 최대 체력의 비율만큼 나눠서 회복), `Invulnerable`(지속 피해까지 모두 막음), `SpeedMultiplier`, `Statuses`. 효과가 하나도 없으면 거부한다. 한 번만 발동한다. 기절·빙결·마비 중에는 시간이 흐르지 않는다.
- `HealOnHit`: 적중에 맞을 때마다 `Chance` 확률로 `HealRatio`(최대 체력의 비율, 최소 1)만큼 회복하고 `SpeedMultiplier`배 이속이 `Duration`초 동안 된다(다시 맞으면 갱신). `Element`를 정하면 그 속성에만 반응한다.
- `SpeedBoost`: `Interval`마다 `Duration`초 동안 `SpeedMultiplier`배 이속(주기 가속).
- 확률을 쓰는 패시브(`Chance`가 1보다 작음)는 `IRandomProvider`가 필요하다. `PassiveBuilder.BuildPassives(monster, random)`가 받고, `EnemyFactory`가 주입받은 랜덤을 넘긴다.
- **방어막과 체력 조건 패시브는 1회성이다.** 한 번 켜지면 다시 켜지지 않는다.
- 회복은 `EnemyModel.Heal`로 한다. `EnemyHpChanged`를 발행하지만 `Enemy`는 체력이 줄 때만 피격 플래시와 피격 모션을 낸다.
- 방어막이 켜져 있는 동안의 밀치기 저항 상승(누리카베)은 아직 없다. 밀치기 배수(`Modifier`)는 처음부터 고정이라 켜짐/꺼짐에 따라 바꿀 수 없다.
- 데이터는 `Monsters.json`의 `Passives` 목록(`Kind` + 값)이고, `PassiveBuilder`가 `Kind`로 객체를 만든다. 상태를 가진 패시브라 호출마다 새 객체를 만든다.

### 속성과 시전 형태 (`DamageProfile`)
- **스킬이 속성을 가진다.** `SkillData.element`(0 Neutral, 1 Fire, 2 Ice, 3 Wind, 4 Lightning, 5 Earth)가 `Skills.json`에 있다. 생략하면 무속성이다. 속성과 시전 형태는 `DamageAttribution` 범위의 `DamageSource`로 피해까지 따라가므로, 가장 바깥 스킬이 이겨서 자식 스킬(`childOnly`)의 피해는 부모의 속성과 `castType`을 이어받는다.
- 약점/저항은 `Monsters.json`의 `Resists`(속성 이름 → 비율)다. 약점은 양수(받는 피해 +50% = 0.5), 저항은 음수(-70% = -0.7), -1은 무효다. 키는 `Neutral`, `Fire`, `Ice`, `Wind`, `Lightning`, `Earth`이고 대소문자를 구분한다.
- **지상 공격 면역은 토속성 저항 100%(`"Earth": -1`)로 표현한다.** 별도의 지상/공중 태그를 만들지 않는다.
- **투사체 감쇠는 시전 형태별 비율로 표현한다.** `Monsters.json`의 `CastResists`(`Projectile`, `Hitscan`, `Area`, `Beam`, `Chain` → 비율)다. 예: `"CastResists": {"Projectile": -0.7}`.
- 속성과 시전 형태의 배율은 곱하고, 줄어도 무효(-1)가 아니면 최소 1은 들어간다. 약점 키는 속성만 쓴다. 특정 스킬 단위 약점은 두지 않는다.
- `DamageProfile`은 스킬에서 나온 피해(`DamageInfo.SkillId != 0`)에만 적용한다. 점화·동상 같은 상태이상 지속 피해는 속성 계산을 받지 않는다.

### 투사체 차단
- 투사체의 **직접 충격 피해만 면역**이다. 폭발, 상태이상, 자식 스킬 등 나머지 리액션은 모두 실행되고 그 피해는 면역이 아니다.
- **관통을 막는다.** 투사체는 이 적에서 소멸한다.
- 직접 피해는 `DamageReaction`(빌더의 `Damage`)과 Hitscan의 직접 타격이 내며 `DamageInfo.IsImpact`로 표시한다. 폭발, 추가 번개, 장판 피해는 충격이 아니다. 관통은 `Projectile`이 적중 뒤 `IEnemyTarget.BlocksPierce`를 확인해 `ProjectileHitLedger.Exhaust()`로 남은 관통을 없앤다.
- 몬스터 데이터는 `Monsters.json`의 `BlocksProjectile: true`다. 막는 것은 `CastType == Projectile`이고 `IsImpact`인 피해뿐이다(Hitscan의 직접 타격은 막지 않는다).

### 스폰/소환
- 소환 요청은 큐로 받아 다음 `Advance`에서 만든다(`TickCombat` 중 목록 변경 방지). 대기 중인 요청이 있으면 클리어를 발행하지 않는다.
- 소환체 종류는 `MonsterId`로 지정하고 총 소환 수 상한(`MaxTotal`)이 있다.
- **소환체는 부모와 다른 몬스터다.** `Spawn`의 `MonsterId`가 자기 자신이면 거부한다.
- **연쇄 소환은 허용하지 않는다.** `Spawn`이 가리키는 몬스터는 `Spawn` 패시브를 가질 수 없다. 둘 다 `GameDataStore`의 패시브 참조 검증에서 거부한다(구현됨, 서로 소환하는 순환도 같은 규칙으로 막힌다).

### 연출 (View)
- 위치, 이동, 밀치기, 애니메이션, 피격 플래시, 상태이상 이펙트는 `Enemy`가 맡는다.
- 방어막, 회복, 이속 증가 표시는 이벤트를 구독할 자리만 만들고 내용은 연출 단계에서 채운다.

## 5. 작업 순서

| 단계 | 내용 | 비고 |
|---|---|---|
| 1 | 이 문서 확정 | |
| 2a | `EnemyStatus` 분리 | 완료. 동작 불변 |
| 2b | 위치와 이동을 `Enemy`로 이전 | 완료. 테스트는 `TestEnemy.Create(model, position)`로 `Enemy`를 만든다 |
| 2c | `Enemy`가 모델을 직접 소유, 이벤트 정리 | 완료(2b와 함께). `EnemySpawner`와 팩토리, 샌드박스, 스킬 대상이 `Enemy`를 쓴다 |
| 3 | 스킬 계약: `SkillData.element`, `DamageSource`, `DamageInfo`, `TakeDamage(DamageInfo)` 오버로드, 충격 구분, 관통 차단 확인점 | 완료. 스킬 시스템을 건드리는 공유 계약이라 별도 작은 PR로 올린다 |
| 4 | 피해 파이프라인과 `DamageProfile` | 완료. 패시브 훅(`OnBeforeDamage`, `OnDamaged`)은 5단계에서 이 파이프라인에 붙는다 |
| 5a | 상태이상 확장(면역 종류, 밀치기 저항, 점화 배율), 연속 공격, 연쇄 소환 금지 검증 | 완료(#219). 피해 훅이 필요 없어 3·4단계와 독립 |
| 5b | 패시브 훅(`OnBeforeDamage`, `OnDamaged`, `OnSpawn`)과 방어막, 회피, 체력 조건 발동, 피격 시 회복·가속, 주기 가속, 동적 면역 | 완료(#220). 몬스터 데이터에는 아직 쓰지 않는다(6단계) |
| 6 | 몬스터 21종 데이터, 프리팹, `WaveMonsterVisualLinker`, 표시 테이블 | 5 이후 |
| 7 | 문서 갱신, 시트 컬럼 반영, 서버 계약 | 서버 계약: 알 수 없는 `Kind`의 처리 정책, 필드 이름 규칙(`Monsters`는 PascalCase, `Skills`는 camelCase) |

각 단계는 EditMode 전체 테스트가 통과한 상태로 끝낸다.

## 6. 결정 사항

- 나무뿌리는 토속성이다. 나머지 스킬의 속성은 3단계에서 `Skills.json`에 채울 때 확정한다.
- 소환체는 부모와 다른 몬스터이고 연쇄 소환은 허용하지 않는다(4절 스폰/소환).
- 밀치기 저항은 비율만 둔다(4절 상태이상).
- 몬스터 수치(약점 비율, 속도 상/중/하)는 임시 추천값으로 채우고 밸런스는 나중에 조정한다.
