# 인술 리팩토링 진행 상태

## 데이터와 성장

실행 인술 데이터는 Resources/MockData/Weapons.json에서 읽는다. DefaultWeaponDataProvider는 JSON을 파싱하고 중복 무기·강화 ID를 검사하며, StageLifetimeScope에서 IWeaponDataProvider로 주입한다. 현재 실행 인술은 쿠나이·화염구·벼락 3종이다. 기존 무기 숫자 ID, 프리팹 ID, 아이콘 키 및 기존 강화 ID는 유지한다.

기재 상한은 15·14·14이며, 사용자 확인에 따라 최초 습득을 제외한 성공한 전투 강화 횟수로 판정한다. UpgradeCount와 기존 표시 Level을 분리했다. 쿠나이 가속은 쿨타임이 아니라 투사체 이동 속도를 올린다. 연발은 동시 발사 수 대신 연속 시전 수를 올린다. 벼락 피해 증폭의 최대 등장 횟수는 최신 자료의 3으로 적용했다.

영구 인술 레벨 조건은 minPermanentLevel로, 25레벨 미만 배타 조건은 belowPermanentLevel로 표현한다. minBattleLevel은 전투 중 WeaponBase.Level을 참조하는 별도 필드다. ProfileWeaponProgression이 PlayerProfile에서 읽으며 전투 시작 시 영구 레벨 스냅샷을 보존한다.

progressionId는 기존 로비 카탈로그의 shuriken·fireball·lightning을 사용했다. 현재 Upgrades.json에는 atk만 있으므로 인술 성장 레코드가 없으면 영구 레벨은 0이다. 성장 비용·최대 영구 레벨을 임의로 만들지 않았다.

최대 등장 횟수(maxPickCount)는 선택 성공 시에만 사용한다. 제시·미선택·실패는 차감하지 않는다. 한도에 도달하면 후보에서 제외한다.

## 선택과 습득

UpgradeEligibility는 후보 생성과 실제 습득에서 동일하게 사용한다. 선행 인술·강화 횟수·교차 무기 강화·최소 레벨·단방향 제외를 지원한다. 상호 배타는 양쪽 선택지에 각각 설정한다.

ApplyUpgradeChoice는 보유하지 않은 무기와 카탈로그에 없는 강화 객체를 거부하고 성공 여부를 반환한다. WeaponBase.LevelUp은 효과를 모두 준비한 뒤 상태를 반영한다. StageManager는 성공한 선택만 기록하고, 실패하면 후보를 갱신한다. 후보가 없으면 기록 없이 전투를 재개한다.

## 공격

ProjectileCount는 한 번의 발사 수, CastCount는 연속 시전 수, PierceCount는 첫 명중 이후 추가 명중 수다. ProjectileSpeed는 이동 속도다. 기존 HitCount는 발사 수의 호환 별칭으로 남긴다. UpgradeType의 기존 0..3 숫자는 유지하며 새 효과는 뒤에 추가했다.

CastClock은 첫 시전에서 즉시 쿨타임을 시작하고 추가 시전을 순차 실행한다. 연발 간격은 Weapons.json의 castInterval이며 현재 프로토타입 값은 0.1초다. 이는 자료의 확정 수치가 아니라 조정 가능한 실행 설정이다. 기존 피해 강화의 순차 곱연산도 유지한다.

ProjectileHitLedger는 동일 대상의 반복 피격을 막고 관통 수 + 1만큼 서로 다른 대상에게 피해를 준다. 풀에서 재사용할 때 명중 이력과 잔여 횟수를 초기화한다.

쿠나이 관통 효과는 구현했지만 선행 인술 얼음창이 아직 실행 카탈로그에 없어 해당 강화는 enabled=false와 사유를 기록했다. 미지원 효과는 설명만 있는 활성 선택지로 만들지 않는다.

## 검증과 남은 작업

전체 Runtime 및 변경 테스트 소스의 C# 컴파일, 조건 판정 15건과 실행·카탈로그 검사 8건을 .NET에서 검증해 상한 경계 검증 3건을 추가해 총 26건 통과했다. Unity EditMode 배치 실행은 동일 워크트리를 이미 연 Unity 인스턴스 때문에 차단됐다. 실제 Unity Test Runner와 플레이 검증은 남아 있다.

Cards.json은 기존 데이터/API 호환성을 위해 유지했고 실제 무기 선택은 Weapons.json만 사용한다. 서버 데이터 계약을 포함한 두 테이블의 통합은 아직 수행하지 않았다.

나머지 인술과 강화 데이터, 쿠나이 폭발·분열·피뢰침, 상태이상·지상/공중 필터·지속 공격은 후속 구현 범위다. 동일 명칭의 서로 다른 강화, 새 자료에서 생략된 조건과 영구 성장 데이터도 확인이 필요하다. 별도로 발견된 URP GlobalSettings 변경은 리팩토링에서 수정하거나 되돌리지 않았다.

## 요구사항 기준

15종·168개 카드별 대응표와 구조화 요구사항은 [ninjutsu/README.md](ninjutsu/README.md)를 본다. (+)는 동일 카드의 변형이며 횟수를 공유한다. 제공 각주에 따라 카드별로 영구 5·9·17레벨에서 피해 보정이 바뀌며 연발 쿠나이는 +10%가 된다. 현재 실행의 조건부 피해 감소 제거와 표시명 변경은 아직 연결하지 않았다.
