# 클라이언트 규칙

공통 작업 규칙(브랜치, 커밋, PR)은 조직 `.github` 레포의 `CONTRIBUTING.md`를 본다.

## 네임스페이스

**어셈블리 이름 그대로** 쓴다. 폴더 경로는 네임스페이스에 반영하지 않는다. 예) `Core/Api`, `Core/Data`, `Core/Growth` 모두 `Game.Core`, `View/Hud`도 `Game.View`, `Tests/EditMode`는 `Game.Tests`

- 시스템 폴더를 더 잘게 나눠도 네임스페이스는 늘어나지 않는다. 폴더가 너무 잘게 쪼개지면 오히려 참조가 지저분해지기 때문
- 모든 코드는 네임스페이스 안에 둔다. 전역 클래스 금지
- 네임스페이스와 같은 이름의 클래스를 만들지 않는다. `Game.Core.Wave` 안에 `Wave` 클래스를 만들면 컴파일 에러가 난다
- asmdef의 Root Namespace가 설정되어 있어 새 스크립트에 루트는 자동으로 들어간다. 하위 부분은 IDE 제안대로 맞춘다

## 인게임

- `Time.timeScale`은 StageManager만 바꾼다
- 팝업 연출과 Local 백엔드 지연은 unscaled time을 쓴다 (`UniTask.Delay(..., ignoreTimeScale: true)`)
- 클리어·실패 판정은 StageJudge 한 곳에서, 먼저 도착한 신호로 판당 한 번만 한다. 매 프레임 확인하지 않고 메시지를 받을 때 판정한다
- 엔트리포인트끼리 실행 순서에 기대지 않는다. 순서가 필요하면 메시지나 명시적 호출로 연결한다
- Stage 씬은 로비에서 `BattleLauncher`가 전투를 발급받은 뒤에 들어와야 `StageContext`가 채워진다. 실제 흐름을 확인할 때는 **Tools → Project Nova → Play From Boot**를 켜서 재생을 Boot 씬부터 시작한다. 끄면 열어 둔 씬에서 재생한다. 켠 선택은 에디터를 다시 켜도 유지되고, 프로젝트 폴더마다 따로 저장된다 (Unity에는 이 값을 바꾸는 기본 메뉴가 없고 값 자체도 재시작하면 풀려서 직접 만들었다)

## 메시지

[messages.md](messages.md)를 본다.

## 구독 해제

- 순수 C# 클래스는 `IDisposable`을 구현하고 `Dispose`에서 해제한다
- MonoBehaviour 뷰는 `HudView`를 상속하고 구독을 `Track()`으로 감싼다

## 인스톨러

- 다른 시스템이 쓰는 인터페이스는 구현 전에 스텁을 먼저 등록한다
- 씬 오브젝트 등록(`RegisterComponentInHierarchy`)은 인스톨러에 넣지 않는다. 샌드박스 씬과 테스트에서 인스톨러를 재사용하기 위해서다

## 데이터와 API

- 밸런스 수치·해금·재화·보상은 서버, 판 안의 진행 상태·쿨타임·에셋은 클라이언트
- 게임 테이블은 도메인 모델로 변환해 쓴다. 전투 발급·제출 요청과 응답만 예외로 `Core/Api`의 타입을 그대로 쓴다
- 프리팹·아이콘은 테이블의 `PrefabKey`/`IconKey`로 찾는다
- 1주차는 저장되는 Local 구현(`Network/Local`)을 쓴다. `save.json`에 저장되어 재실행해도 값이 유지된다. `MockData` 폴더는 Local이 읽는 초기 테이블 JSON을 두는 곳이다
- Local도 ApiDog 명세와 같은 DTO(`Network/Dto`)로 변환하고, 서버와 같은 오류 코드(`INSUFFICIENT_GOLD` 등)로 `ApiException`을 던진다
- 서버 전환은 `BackendSettings`(ScriptableObject)의 스위치로 그룹 단위로 한다: Account(`IAuthApi`, `IDataApi`)를 먼저, Player(`IPlayerApi`, `IBattleApi`, `IUpgradeApi`, `IEnergyApi`, `IStageApi`) 5개는 한 번에 전환한다. 다섯 다 지갑을 건드리므로 따로 바꾸면 로컬과 서버 잔액이 어긋난다
- 로컬 데이터는 서버로 이관하지 않는다. 전환 후에는 새 계정으로 시작하고, Local에서 만든 미전송 결과는 서버로 보내지 않는다
