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

## 에셋

- 에셋은 `Assets/_Project/` 안에만 둔다. `Assets/` 루트에 프리팹을 두지 않는다.
- 폴더는 역할별로 나눈다.

```
Assets/_Project/
  Prefabs/
    Skills/<스킬>/   플레이어 스킬. 본체와 명중 이펙트를 스킬 폴더 하나에 둔다 (Arrow, Fireball, Lightning, IceSpear, Log)
    Enemies/         Monsters, Wave, Projectiles(적 투사체), Attacks(몬스터 공격 연출), Spawn
    Player/ UI/ Scopes/
  Art/
    Characters/      Monsters, Player (리그 PSD와 애니메이션 포함)
    VFX/             Textures, Materials, Shaders, Doodle, Enemy(몬스터 공격 연출용)
    UI/              Sheets(N-Sheet), Backgrounds
  Resources/         코드가 경로 문자열로 불러오는 것만 (MockData, Materials, VFX)
  Data/ Settings/ Scenes/ Scripts/
```

- 새 스킬은 `Prefabs/Skills/<스킬 이름>/` 폴더를 만들어 본체와 명중 이펙트를 둔다. `Resources`에 넣은 에셋은 참조와 무관하게 빌드에 포함되므로 코드가 경로로 불러오는 것만 둔다.
- 에디터 도구(`Editor/MonsterRig`, `WaveMonsterVisualLinker` 등)와 테스트는 에셋 경로를 문자열로 갖고 있다. 폴더를 옮기거나 이름을 바꾸면 이 경로도 함께 고친다. 이동은 Unity 안에서 하거나 `.meta`를 같이 옮겨 GUID를 유지한다.
- 이름은 영문 PascalCase로 짓는다. 한글, 공백, 대문자 확장자(`.PNG`)를 쓰지 않고 새 파일은 숫자로 시작하지 않는다. 버전은 `_v2`처럼 접미로 붙인다.
- 실험용 씬이나 테스트용 복제 프리팹은 커밋하지 않는다. 빌드에 연결되지 않은 에셋은 쓰임을 확인하고 지운다. 지우기 전에는 Unity의 Find References로 확인한다.
- 임포트 설정은 에셋마다 쓰임에 묶여 있다. Pixels Per Unit은 프리팹 크기가 그 값에 맞춰져 있으므로 임의로 통일하지 않는다(`Art/VFX/Enemy` 64, `Arrow_v1` 1000 등). Max Size는 화면에서 보이는 크기에 맞춘다(`VFX/Doodle`은 256). 스프라이트는 밉맵을 끈다. 다만 파티클 셰이더가 쓰는 `VFX/Textures`는 켠 상태를 유지한다.
- 아이콘과 프리팹은 테이블의 `IconKey`/`PrefabKey`로 찾는다(위 "데이터와 API").
- 기존 예외: `Art/UI/Sheets`의 `N-Sheet.png`(1~6)는 UI 시트라 역할이 확정될 때 이름을 바꾼다.
