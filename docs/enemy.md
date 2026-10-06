# Enemy 구조와 추가 절차

이슈 #157 조사 결과다. 코드가 원본이고, 이 문서는 "새 Enemy를 하나 추가하려면 어디를 건드리나"를 한곳에 모은 것이다. 구현 변경은 없다.

## 원본 위치

| 무엇 | 어디 |
| --- | --- |
| 원작 요마 목록 | 나무위키 「냥냥 시노비 : 미소녀 닌자 디펜스」 문서의 "요마 목록"(일반, 상급, 보스 칠재) |
| 몬스터 수치 시트 | 팀 기획 스프레드시트의 `Monsters` 탭(gid 640001). 링크는 팀 문서를 본다 |
| 코드가 읽는 데이터 | `Assets/_Project/Resources/MockData/Monsters.json`(시트와 같은 10개 컬럼) |

위키에는 HP·공격력 같은 수치가 없고 속도 등급과 특수 능력 설명만 있다. 수치는 우리 게임 기준으로 정해야 한다.

## 현재 Enemy 5종

실제로 스폰되는 건 `Spawn.prefab`의 `EnemyFactory._enemyPrefabEntries` 5개다. 이름은 시트에 없고(Id만 있음) 사람이 보기 편하도록 붙인 표기다.

| Id | 등급(`EnemyType`) | 웨이브 프리팹 (`Prefabs/Stage`) | 리그 원본 (`Prefabs/Monsters`) | 공격 | 투사체 |
| --- | --- | --- | --- | --- | --- |
| 1 슬라임 | Normal | `Slime` | `Slime_Animated` | 원거리 | `Slime_Projectile` |
| 2 박쥐 | Normal | `Bat` | `Bat_Animated` | 근접 | - |
| 11 엘리트 슬라임 | Elite | `Elite_Slime` | `Slime_Animated` | 원거리 | `Slime_Projectile` |
| 12 엘리트 박쥐 | Elite | `Elite_Bat` | `Bat_Animated` | 근접 | - |
| 100 해골 | Boss | `Skeleton` | `Skeleton_Animated` | 근접 | - |

현재 데이터는 Normal 1~2, Elite 11~12, Boss 100으로 Id를 나눠 쓰고 있다. 코드나 시트가 정한 규칙은 아니다.

## 요마표 갱신안 (게임 콘셉트)

시트에는 이름 컬럼이 없다(결정). 이름은 이 문서에서만 쓰는 표기다. 속도는 원작 등급을 숫자로 옮긴 기준을 쓴다(최하 0.04, 하 0.06, 중 0.1, 상 0.15, 최상 0.2). 아래 5종은 현재 시트 값에서 `Speed`만 바꿨다. 쓰이지 않는 리그 프리팹에 임의 수치를 붙인 Id 3~9는 이 문서 맨 아래 부록에 따로 둔다.

| Id | 이름 | 리그 프리팹 | 등급 | 공격 | Hp | Damage | Speed | AttackInterval | AttackRange | ProjectileSpeed | IsBoss | 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 슬라임 | Slime | Normal | 원거리 | 999 | 1 | 0.06 | 3 | 2 | 8 | FALSE | 현재 |
| 2 | 박쥐 | Bat | Normal | 근접 | 999 | 1 | 0.2 | 2 | 0 | 0 | FALSE | 현재 |
| 11 | 엘리트 슬라임 | Slime | Elite | 원거리 | 25 | 2 | 0.06 | 2.5 | 2 | 8 | FALSE | 현재 |
| 12 | 엘리트 박쥐 | Bat | Elite | 근접 | 999 | 3 | 0.2 | 2.5 | 0 | 0 | FALSE | 현재 |
| 100 | 해골 | Skeleton | Boss | 근접 | 999 | 5 | 0.1 | 1.8 | 0 | 0 | TRUE | 현재 |

시트에 붙일 때는 이름·리그 프리팹·등급·공격·상태 컬럼을 빼고 아래 8개 컬럼만 쓴다(시트 스키마와 같은 순서).

```
Id	Hp	Damage	Speed	AttackInterval	AttackRange	ProjectileSpeed	IsBoss
1	999	1	0.06	3	2	8	FALSE
2	999	1	0.2	2	0	0	FALSE
11	25	2	0.06	2.5	2	8	FALSE
12	999	3	0.2	2.5	0	0	FALSE
100	999	5	0.1	1.8	0	0	TRUE
```

보스(Id 100)는 아직 원작 칠재와 연결하지 않았다.

> **TODO**: 위 요마표(`Speed` 변경, `Exp`·`GaugeValue` 제거)와 부록의 Id 3~9는 아직 `Monsters.json`에 반영하지 않았다. 시트 갱신이 확정된 뒤 별도 PR에서 `Monsters.json`과 `MonsterDefinition`을 맞춘다. 이 PR에서는 `Monsters.json`과 프리팹을 바꾸지 않는다.

`Exp`와 `GaugeValue`는 코드에서 읽는 곳이 없어 갱신안에서 뺐다. 현재 `Monsters.json`과 `MonsterDefinition`에는 아직 남아 있고, 제거는 데이터·코드 변경이라 별도 PR에서 한다. 경험치는 #153에서 몬스터별로 둘지 정해지면 다시 넣는다.

## 데이터가 흐르는 길

```
Monsters.json ─▶ GameDataStore.Monsters (MonsterDefinition, Id로 조회)
                         │
Spawn.prefab ─ EnemyFactory._enemyPrefabEntries (EnemyPrefabEntry: Type, Prefab, ProjectilePrefab, MonsterId, AttackType)
                         │
EnemySpawner.PickEnemyType()  ─ 보스 → 엘리트(확률) → 일반 순으로 등급만 고름
                         │
EnemyFactory.Create(위치, 등급) ─ 같은 등급 엔트리 중 무작위 1개 선택(EnemyPrefabTable.GetRandomEntry)
                         ├─ EnemyModel 생성 (Hp·Speed·공격 값은 Monsters 행에서)
                         └─ 웨이브 프리팹 Instantiate → Enemy.Bind(모델, 투사체 프리팹, 메시지)
```

- 로직은 `EnemyModel`(순수 C#), 화면은 `Enemy`(MonoBehaviour)가 맡는다. `Enemy.Update`가 모델의 `Position`을 `transform`에 복사한다.
- 이동·공격·피격·사망은 모두 `EnemyModel`이 하고, 결과를 `EnemyHpChanged`·`EnemyDied` 메시지와 `Attacked`·`ProjectileFired` 이벤트로 알린다.
- 스테이지의 웨이브 정의(`Stages.json`)에는 **몇 마리, 엘리트·보스 최대 몇 마리**만 있고 어떤 종류인지는 없다. 종류는 등급별 무작위다.
- `MonsterDefinition`의 `IsBoss`는 정의만 있고 런타임 코드에서 읽는 곳이 없다. `EnemyFactory.Create`는 `Hp`, `Speed`, 공격 값(`Damage`, `AttackInterval`, `AttackRange`, `ProjectileSpeed`)만 쓴다.

## 기존 동작

리팩터링 때 유지해야 할 기준선이다. 모두 `EnemyModel`이 정하고 `Enemy`는 결과를 화면에 옮긴다. 별도 표기가 없으면 `EnemySpawner.Advance`가 매 프레임 `TickCombat`으로 호출한다.

| 동작 | 현재 구현 |
| --- | --- |
| 생성 | `EnemyFactory.Create`가 `Instantiate`로 웨이브 프리팹을 만들고 `Bind`한다. Id는 `++_nextEnemyId`다. 위치는 `SpawnArea`의 현재 범위에서 x, y를 무작위로 뽑는다. 풀링은 없다 |
| 이동 | 항상 아래(`Vector2.down`)로 `Speed × MovementMultiplier × deltaTime`만큼 간다. 사거리 안이면 이동하지 않는다 |
| 사거리 판정 | `Position.y - wall.AttackLineY <= AttackRange`. 근접은 `AttackRange`가 0이라 방벽 공격선에 닿아야 공격한다 |
| 공격 | 사거리 안에서 `AttackInterval`마다 한 번. 타이머는 0에서 시작하고 사거리 안에서만 흐르므로 도착 즉시 공격하지 않고 한 주기를 기다린다. 근접은 `wall.TakeDamage(Damage)`, 원거리는 투사체를 발사한다. 공격이 나가면 `Attacked` 이벤트가 발생해 `Attack` 애니메이션 트리거가 켜진다 |
| 원거리 투사체 | `EnemyProjectileSystem`이 모델을 관리한다. 아래로 직진하다가 `y <= AttackLineY`가 되면 방벽에 `Damage`를 주고 사라진다. 플레이어 스킬이 가로막는 동작은 없다. 화면 쪽 `EnemyProjectile`은 `Instantiate`로 만들고 `IsDone`이면 `Destroy`한다 |
| 피격 | `TakeDamage(amount)`: 이미 죽었거나 `amount <= 0`이면 무시한다. 취약 상태면 받는 피해를 `floor(amount × (1 + 비율))`로 늘린다. `Hp`를 깎고 `EnemyHpChanged`를 발행한다. 화면은 `HitFlash`와 `Hit` 트리거를 재생한다(죽지 않았을 때만 `Hit`) |
| 피격 경로 | 투사체 스킬은 `IEnemyTargetProvider.GetNearest` 좌표로 `IEnemyTarget.TakeDamage`를 부른다. 낙뢰는 `Physics2D` 오버랩으로 `Enemy` 컴포넌트를 찾아 `Enemy.TakeDamage`를 부른다. 둘 다 결국 `EnemyModel.TakeDamage`로 모인다 |
| 사망 | `Hp`가 0이 되면 상태이상을 모두 지우고 `EnemyDied(Id)`를 발행한다. 화상 상태였다면 사망 폭발 콜백도 실행한다. 화면은 `Die` 파라미터가 없으면 즉시 `Destroy`, 있으면 `Die` 트리거 후 `_Die` 클립 길이(없으면 0.6초) 뒤에 `Destroy`한다. 모델은 다음 `TickCombat`에서 목록에서 빠지고 그 전에도 `GetNearest`는 죽은 적을 건너뛴다 |
| 클리어 판정 | 마지막 웨이브 스폰이 끝나고 필드의 적이 모두 죽으면 `EnemySpawner`가 `AllEnemiesCleared`를 한 번 발행한다 |

### 상태이상

플레이어 스킬이 `ReactionCompiler`의 확률 판정으로 적에게 효과를 건다. 적은 효과마다 `IFreezableTarget` 같은 인터페이스를 구현하고, 스킬은 대상이 그 인터페이스를 구현할 때만 효과를 적용한다. 효과 상태는 `EnemyModel`이 들고 있고, `TickStatus`가 `TickCombat`에서 매 프레임 시간을 줄이거나 피해를 준다.

| 효과 | 걸리면 바뀌는 것 | 지속·중첩 | 화면 변화 |
| --- | --- | --- | --- |
| 빙결 | 이동·공격 정지(공격 타이머도 멈춤) | 지속 시간이 겹치면 긴 쪽으로 갱신 | `Freeze` 파티클. 이때 동상 파티클은 숨김 |
| 마비 | 이동·공격 정지 | 빙결과 같음 | `Paralysis` 파티클 |
| 기절 | 이동·공격 정지 | 빙결과 같음 | `Stun` 파티클 |
| 둔화 | 이동 속도 배율 감소 | 비율은 큰 쪽, 시간은 긴 쪽으로 갱신. 비율은 0 초과 1 미만 | `Slow` 파티클 |
| 범위 둔화 | 이동 속도 배율 감소 | 거는 쪽(`source`)마다 따로 두고, 둔화와 합치지 않고 가장 강한 하나만 적용 | `Slow` 파티클 |
| 화상 | 1초마다 피해 `max(1, 초당 피해 + 최대 체력 × 비율)`. 걸린 채 죽으면 사망 폭발 콜백 실행 | 새로 걸 때 피해·비율·시간은 큰 쪽으로 갱신 | `Burn` 파티클 |
| 동상 | 스택마다 1초에 `max(1, 스택 피해)` | 스택당 10초, 최대 5개(넘으면 가장 오래된 것부터 제거) | `Frostbite` 파티클(빙결 중에는 숨김) |
| 취약 | 이후 받는 모든 피해(화상·동상 포함)를 `× (1 + 비율)`로 늘림(버림) | 비율은 큰 쪽, 시간은 긴 쪽으로 갱신 | `Vulnerability` 파티클 |
| 밀치기 | `Position`을 방향 × 거리만큼 즉시 이동 | 지속 없음 | 없음(위치가 바뀌어 `Enemy`가 따라감) |

효과가 풀리면 값이 원래대로 돌아간다. 죽는 순간 화상·둔화·기절·취약·범위 둔화 상태는 모두 초기화되고, `EnemyStatusEffects`는 파티클을 끈다.

알아둘 점은 이렇다.

- **면역·저항이 없다.** 효과를 거는 코드는 몬스터 등급이나 종류를 보지 않는다. 원작의 "기절 면역"(오니), "점화·빙결 면역"(불도마뱀)처럼 몬스터별로 막으려면 해당 인터페이스를 구현하지 않는 모델을 쓰거나 새 판정 규칙을 넣어야 한다. 지금 `EnemyModel`은 모든 효과 인터페이스를 구현한다.
- **애니메이션은 멈추지 않는다.** 빙결·마비·기절 중에 `Animator`를 멈추는 코드는 없다. 이동이 멈추면 `Enemy.Update`가 위치 변화가 없다고 보고 `Moving`만 끈다. 별도의 얼음·기절 포즈도 없고 파티클만 보인다.
- **`Enemy`(MonoBehaviour)가 구현하는 상태 인터페이스는 `IParalyzableTarget`뿐이다.** 낙뢰의 `HitscanEffect`처럼 콜라이더로 `Enemy`를 찾는 코드는 지금 피해만 주고, 다른 효과를 걸려면 `Enemy.Target`(`EnemyModel`)을 써야 한다.
- **효과 표시는 프리팹 크기에 맞춰진다.** `EnemyStatusEffects`가 자식 `SpriteRenderer`의 범위를 합쳐 파티클 크기와 위치를 정하므로, 새 몬스터도 별도 설정 없이 붙는다. `Resources/VFX/EnemyStatuses` 프리팹이 없으면 효과 표시가 조용히 생략된다.

이 컴포넌트와 `HitFlash`는 웨이브 프리팹에 없으면 `Enemy.Bind`가 자동으로 붙인다.

### 등급별 차이

`Normal`, `Elite`, `Boss`는 스폰 때 등급을 고르는 데만 쓰이고(`EnemySpawner.PickEnemyType`) 이동·공격·피격 동작은 모두 같다. 등급별 차이는 `Monsters` 행의 수치와 엘리트의 시각 변형(1.2배, 원색)뿐이다.

## 웨이브 프리팹의 구조

웨이브 프리팹(`Prefabs/Stage/Slime` 등)은 리그 프리팹의 베리언트가 **아니다**. 구조는 이렇다.

- 루트: `Enemy`, `Rigidbody2D`(Kinematic, 이동·회전 고정), `CircleCollider2D`, 레이어 `Enemy`, 그리고 크기 기준용 `SpriteRenderer`(꺼짐)
- 자식 `Visual`: `Monsters/*_Animated` 리그 프리팹의 중첩 인스턴스

`Visual`은 수동 작업이 아니라 에디터 도구 `Tools > Monster > Link Visuals To Wave Prefabs`(`WaveMonsterVisualLinker`)가 붙인다. 일반 몬스터는 탈색 머티리얼, 엘리트는 같은 리그를 원래 색으로 1.2배 키운다. 즉 **엘리트는 별도 아트가 아니라 일반 몬스터의 색·크기 변형**이다.

## 새 Enemy 추가 절차 (현재 기준)

1. **리그 프리팹**: `Prefabs/Monsters/<이름>_Animated.prefab`을 만든다. 이 폴더에는 몬스터 리그만 둔다. `MonsterAnimationBaker`가 폴더 안 프리팹을 전부 몬스터로 보고 애니메이션을 만들기 때문이다(`Tools > Monster > Bake Animations`).
2. **웨이브 프리팹**: `Prefabs/Stage/<이름>.prefab`에 루트 컴포넌트를 구성한다. `Enemy` + `Rigidbody2D`(Kinematic) + `CircleCollider2D` + 레이어 `Enemy`. 낙뢰(`HitscanEffect`)가 `Physics2D` 오버랩과 레이어 마스크로 판정하므로 콜라이더와 레이어가 빠지면 낙뢰만 안 맞는다. 투사체 스킬은 좌표 기준이라 영향이 없다.
3. **비주얼 연결**: `WaveMonsterVisualLinker`의 `Links` 배열에 `(웨이브 이름, 리그 이름, 엘리트 여부)` 한 줄을 **코드로** 추가하고 메뉴를 실행한다. 배열이 하드코딩이라 이 단계는 코드 수정이 필요하다.
4. **데이터**: `Resources/MockData/Monsters.json`에 새 `Id` 행을 추가한다. 검증은 `Hp >= 1`, `AttackInterval > 0`, 음수 금지다.
5. **스폰 연결**: `Spawn.prefab`의 `EnemyFactory._enemyPrefabEntries`에 항목을 추가한다(`Type`, `Prefab`, `MonsterId`, `AttackType`). 원거리면 `ProjectilePrefab`이 필요하고 `ProjectileSpeed > 0`이어야 한다.
6. **검증**: `EnemyPrefabMonsterLinkTests`가 `Spawn.prefab`의 모든 엔트리가 `Monsters` 행을 가리키는지, 원거리의 투사체 속도가 0이 아닌지 확인한다. 플레이 시작 때 `EnemyFactory`도 같은 검사를 로그로 낸다.

### 알아둘 점

- 같은 `EnemyType` 엔트리가 여러 개면 **무작위로 하나씩** 뽑힌다. 새 Normal 몬스터를 추가하면 모든 스테이지·웨이브에 섞여 나오고, 특정 스테이지에만 등장시키는 방법이 지금은 없다.
- 새 Enemy 하나에 건드리는 곳은 프리팹 2개(웨이브, `Spawn`), 데이터 1개(`Monsters.json`), 코드 1줄(`Links`)이다.
- 몬스터 능력(분열, 비행, 면역, 회복 등)을 담는 필드가 없다. 지금 있는 상태이상 계열은 플레이어 스킬이 거는 쪽(`IFreezableTarget` 등)이다.

## 사용되지 않는 리그 프리팹

`Prefabs/Monsters/`에는 `ArmoredCrab`, `BrainSpider`, `EyeJelly`, `Ghost`, `Golem`, `Horned`, `Spider`가 있다. 어디에서도 웨이브 프리팹이 쓰지 않고(`EyeJelly`는 `BrainSpider`가 참조할 뿐), `Links`에도 없다. 위 절차 2~5단계를 하면 쓸 수 있다.

## 원작 요마와의 차이

원작(나무위키 「냥냥 시노비 : 미소녀 닌자 디펜스」)의 요마 목록과 비교한다. 위키에는 수치가 없고 속도 등급(최하·하·중·상·최상)과 특수 능력 설명만 있다.

| 원작 일반 요마 | 속도 | 특수 능력 | 현재 대응 |
| --- | --- | --- | --- |
| 고슴도치 | 중 | 없음 | 없음 |
| 슬라임 | 하 | 사망 시 미니 슬라임 2마리 분열 | Id 1 (원거리, 분열 없음) |
| 독살무사 | 상 | 이동속도 대폭 상승 | 없음 |
| 오니 | 하 | 밀치기 저항, 기절 면역 | 없음 (`Horned` 리그 후보) |
| 불도마뱀 | 중 | 화둔 -50%, 점화·빙결 면역 | 없음 |
| 나무 정령 | 최하 | HP 30% 이하 시 5초간 50% 회복 | 없음 |
| 박쥐 | 최상 | 비행(지상 공격 면역) | Id 2 (면역 없음) |

상급(엘리트)은 "일반 요마가 보라색 기운을 가진 것"이고 보스는 칠재 7종(후메츠, 미즈치, 카제하, 하쿠코, 카구츠치, 라이 히메, 아마츠누바타마)이다. 현재 보스(Id 100)는 해골이라 원작 칠재와 연결이 없다.

속도 등급을 숫자로 옮기는 기준은 요마표 갱신안이 제안한 값이고 확정이 아니다. 현재 `Monsters.json`은 5종이 모두 0.1이다.

## 부록: 임시 제안 (Id 3~9)

`Prefabs/Monsters/`에 있지만 쓰이지 않는 리그 프리팹 7종에 임의 수치를 붙인 **임시 제안**이다. 확정된 값이 아니며 시트와 `Monsters.json`에는 없다. 실제로 스폰하려면 위 "새 Enemy 추가 절차"를 따라야 한다.

| Id | 이름 | 리그 프리팹 | 등급 | 공격 | Hp | Damage | Speed | AttackInterval | AttackRange | ProjectileSpeed | IsBoss | 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 3 | 거미 | Spider | Normal | 근접 | 20 | 1 | 0.15 | 2 | 0 | 0 | FALSE | 제안 |
| 4 | 눈알 젤리 | EyeJelly | Normal | 원거리 | 25 | 1 | 0.06 | 3 | 2 | 8 | FALSE | 제안 |
| 5 | 유령 | Ghost | Normal | 근접 | 15 | 1 | 0.1 | 2.5 | 0 | 0 | FALSE | 제안 |
| 6 | 갑옷 게 | ArmoredCrab | Normal | 근접 | 60 | 1 | 0.06 | 3 | 0 | 0 | FALSE | 제안 |
| 7 | 뿔 요마 | Horned | Normal | 근접 | 50 | 3 | 0.06 | 2.5 | 0 | 0 | FALSE | 제안 |
| 8 | 골렘 | Golem | Normal | 근접 | 100 | 2 | 0.04 | 3.5 | 0 | 0 | FALSE | 제안 |
| 9 | 뇌 거미 | BrainSpider | Normal | 근접 | 30 | 2 | 0.1 | 2 | 0 | 0 | FALSE | 제안 |

시트에 붙일 때는 위와 같은 8개 컬럼만 쓴다.

```
Id	Hp	Damage	Speed	AttackInterval	AttackRange	ProjectileSpeed	IsBoss
3	20	1	0.15	2	0	0	FALSE
4	25	1	0.06	3	2	8	FALSE
5	15	1	0.1	2.5	0	0	FALSE
6	60	1	0.06	3	0	0	FALSE
7	50	3	0.06	2.5	0	0	FALSE
8	100	2	0.04	3.5	0	0	FALSE
9	30	2	0.1	2	0	0	FALSE
```
