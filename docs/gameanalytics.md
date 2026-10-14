# GameAnalytics

SDK는 `Packages/manifest.json`의 OpenUPM 의존성 `com.gameanalytics.sdk`로 설치한다. 현재 버전은 8.3.0으로 고정한다. 설치 방법은 [공식 Unity 안내](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/)를 참고한다.

## 키 설정

`Assets/_Project/Resources/GameAnalytics/Settings.asset`의 Setup에서 대상 플랫폼의 Game Key와 Secret Key를 설정한다. 현재 Android 플랫폼을 구성했다. 다른 플랫폼을 배포할 때는 SDK Inspector의 Add Platform으로 추가한 뒤 해당 플랫폼의 키를 입력한다. 버전은 Player Settings에서 자동으로 가져온다.

Google 계정으로 가입했다면 웹 대시보드에 Continue with Google로 로그인한 뒤 게임 SDK 설정의 키를 직접 입력한다. Unity 에디터의 계정 로그인은 필수가 아니다.

Root 스코프가 `GameAnalyticsService`를 초기화한다. 현재 플랫폼의 키가 없으면 경고를 남기고 분석 전송을 생략한다. 에디터에서는 SDK를 초기화하지 않는다. 테스트·샌드박스가 운영 통계에 들어가지 않게 하기 위해서다. SDK 예외는 경고로 처리해 카드 적용과 결과 제출을 계속 진행한다.

SDK 설정은 `Resources/GameAnalytics/Settings`라는 고정 경로로 읽으므로 이 에셋은 이름과 Resources 아래 경로를 유지한다. 씬에 GameAnalytics 프리팹을 추가하거나 별도로 `Initialize()`를 호출하지 않는다.

## 이벤트

| 시점 | 종류 | 식별자 | 값·필드 |
| --- | --- | --- | --- |
| 전투 시작 | Progression Start | `stage / stage_{stageId}` | 없음 |
| 클리어 | Progression Complete | 위와 동일 | score: 처치 수, `outcome=clear`, `reached_wave`, `play_time_seconds` |
| 실패·포기 | Progression Fail | 위와 동일 | score: 처치 수, `outcome=fail` 또는 `forfeit`, `reached_wave`, `play_time_seconds` |
| 전투 종료 시 스킬별 피해량 | Design | `battle:skill_damage:stage_{stageId}:skill_{skillId}` | value: 누적 실제 피해량, `outcome`: clear / fail / forfeit |
| 카드 적용 성공 | Design | `card:picked:stage_{stageId}:{source}:{cardId}` | `pick_index`: 이번 전투의 1부터 시작하는 선택 순서 |

`source`는 웨이브 카드 `wave`, 엘리트 룰렛 `elite_roulette`다. 카드 ID와 순서는 `BattleStats.BuildLog`에 실제로 기록된 값을 쓴다. 적용에 실패하거나 오래된 후보를 갱신한 경우에는 보내지 않는다. ID는 카탈로그의 안정적인 ID를 쓰며 사용자 입력이나 전투 ID를 이벤트 이름에 넣지 않는다.

스킬별 피해량은 결과 화면과 같은 `BattleStats.SkillDamageRanking()`의 누적 실제 피해량을 사용한다. 피해가 없는 스킬은 보내지 않으며 공격마다 전송하지 않는다. 자식 스킬 피해는 기존 집계 규칙대로 부모 스킬에 포함한다.

전투 종료와 스킬별 피해량 이벤트는 결과 제출을 시작할 때 한 번 기록한다. 서버 제출 성공 여부와 독립적이며, 자동·수동 제출 재시도나 중복 종료 신호로 다시 보내지 않는다. 전투 중 앱을 종료하면 Start만 남을 수 있다. 계정 ID나 로그인 토큰은 이 연동에서 전송하지 않는다.

## 실제 수신 확인

키를 입력한 기기 빌드에서 Boot → Lobby → 전투로 진입해 카드 선택, 클리어·실패·포기를 수행하고 대시보드의 실시간 이벤트를 확인한다. 로컬 테스트 통과만으로 대시보드 수신을 확인했다고 간주하지 않는다.
