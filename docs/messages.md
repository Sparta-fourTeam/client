# 메시지

시스템 사이의 약속이다. 메시지를 추가·변경하는 PR은 이 문서를 같이 고친다. 필드는 각 struct 파일을 본다.

## 규칙

- 값만 담는 `readonly struct`. 이름은 사건이면 과거형(`EnemyDied`), 상태면 `~Changed`
- Buffered 메시지는 반드시 `IBufferedPublisher`로 발행한다. 일반 `IPublisher`로 보내면 Buffered 구독자에게 가지 않는다
- 브로커는 발행자의 인스톨러에 등록한다

## 목록

발행처는 메시지의 주인이라 적어둔다. 담당자는 발행 클래스가 있는 폴더로 [ownership.md](ownership.md)에서 찾는다. 구독처는 아래 Diagnostics 창으로 본다.

| 메시지 | 종류 | 발행 클래스 |
| --- | --- | --- |
| `StageStateChanged` | Buffered | StageManager |
| `WaveStarted` | 사건 | WaveManager |
| `SpawnCountdown` | Buffered | WaveManager |
| `EnemyDied` | 사건 | EnemySpawner |
| `WaveGaugeChanged` | Buffered | WaveProgress |
| `WaveGaugeFilled` | 사건 | WaveProgress |
| `CardPicked` | 사건 | StageManager |
| `SkillChanged` | 사건 | SkillInventory |
| `WallHpChanged` | Buffered | Wall |
| `WallDestroyed` | 사건 | Wall |
| `StageEnded` | 사건 | StageJudge |
| `StartFailed` | 사건 | BattleLauncher |
| `StageResult` | 사건 | StageManager |
| `SubmitFailed` | 사건 | StageManager |
| `SubmitRejected` | 사건 | StageManager |
| `WalletChanged` | Buffered | LobbyModel |
| `EnergyChanged` | Buffered | EnergyClock |
| `UpgradeChanged` | 사건 | UpgradeService |
| `ProgressChanged` | Buffered | LobbyModel |
| `LobbyRequestFailed` | 사건 | UpgradeService, EnergyRecovery |

로비 메시지(`WalletChanged`, `EnergyChanged`, `UpgradeChanged`, `ProgressChanged`, `LobbyRequestFailed`)는 새 파일 `Core/Messages/LobbyMessages.cs`에 둔다.

## 구독처 확인: MessagePipe Diagnostics

플레이 중에 **Window → MessagePipe Diagnostics**를 열면 지금 살아 있는 구독이 구독한 위치(클래스와 줄)별로 묶여서 보인다.

- 메시지를 바꾸기 전: 누가 구독 중인지 확인하고 해당 담당에게 알린다
- 스테이지를 나갔다 들어온 뒤: 구독 수가 늘어나 있으면 해제를 빠뜨린 것이다

설정은 1단계에 E가 Stage 스코프에 한 번 넣어둔다. 스택 트레이스 수집은 느려서 에디터와 개발 빌드에서만 켠다.

```csharp
var o = builder.RegisterMessagePipe(options => {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    options.EnableCaptureStackTrace = true; // 구독 위치 표시
#endif
});
builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider())); // 창 사용에 필요
```

언제 메시지를 쓰고 언제 직접 읽는지는 [architecture.md](architecture.md)의 소통 규칙을 본다. 상태를 나타내는 메시지(`~Changed`)는 Buffered로 만들어서, 늦게 구독한 뷰도 현재 값을 바로 받게 한다.

## 발행 확인: Message Debugger

Diagnostics 창은 구독만 보여주고 발행은 못 한다. 내 UI나 로직이 메시지를 제대로 받는지 보려면 플레이 중에 **Tools → Project Nova → Message Debugger**를 연다. 버튼으로 메시지를 직접 발행한다.

- 사용법: Boot 씬에서 재생해 Stage 씬까지 들어간 뒤 연다. `EnemyDied x5`를 누르면 웨이브 1이 끝나 카드 선택까지 이어진다
- 판정(`StageEnded`)은 StageJudge를 거치지 않고 바로 발행된다. 판정 로직 자체를 확인할 때는 `WallDestroyed`나 `WaveGaugeFilled`를 쓴다
- `StageResult`(결과 팝업이 구독)는 서버 제출을 거치지 않고 표시만 확인할 때 쓴다. 제출까지 실제로 확인하려면 로비에서 **전투 발급 후 Stage 진입** 버튼으로 들어간 뒤 `WaveGaugeFilled (마지막 웨이브)`(클리어)나 `Pause` → `Forfeit`(포기)를 쓴다. 이 버튼은 로비가 `BattleLauncher`에 연결되기 전에도 실제 전투 발급으로 Stage에 들어간다. 에너지 소모와 골드 지급이 로컬 저장에 실제로 기록된다
- Buffered 메시지(`WallHpChanged`)는 `IBufferedPublisher`로 발행한다. 새 메시지를 추가하면 `Scripts/Editor/MessageDebugWindow.cs`에 버튼을 같이 넣는다
- 에디터 전용 어셈블리(`Game.Editor`)라 빌드에는 포함되지 않는다
