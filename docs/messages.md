# 메시지

시스템 사이의 약속이다. 메시지를 추가·변경하는 PR은 이 문서를 같이 고친다. 필드는 각 struct 파일을 본다.

## 규칙

- 값만 담는 `readonly struct`. 이름은 사건이면 과거형(`EnemyDied`), 상태면 `~Changed`
- Buffered 메시지는 반드시 `IBufferedPublisher`로 발행한다. 일반 `IPublisher`로 보내면 Buffered 구독자에게 가지 않는다
- 브로커는 발행자의 인스톨러에 등록한다

## 목록

발행처는 메시지의 주인이라 적어둔다. 구독처는 아래 Diagnostics 창으로 본다.

| 메시지 | 종류 | 발행 클래스 |
| --- | --- | --- |
| `StageStateChanged` | Buffered | StageManager |
| `WaveStarted` | 사건 | WaveManager |
| `SpawnCountdown` | Buffered | WaveManager |
| `EnemyDied` | 사건 | EnemySpawner |
| `WaveGaugeChanged` | Buffered | WaveProgress |
| `WaveGaugeFilled` | 사건 | WaveProgress |
| `WaveGaugeChanged` | Buffered | WaveProgress |
| `WaveGaugeFilled` | 사건 | WaveProgress |
| `AllEnemiesCleared` | 사건 | EnemySpawner |
| `CardPicked` | 사건 | StageManager |
| `SkillChanged` | Buffered | WeaponController |
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

`WalletChanged.HasValue`는 실제 발행된 잔액인지 구분한다. 생성자로 만든 메시지는 금액이 0이어도 true이며, 미발행 버퍼가 전달하는 `default(WalletChanged)`는 false다. 재화 UI는 false인 메시지를 무시하고 첫 실제 잔액은 카운트 효과 없이 표시한다.

`EnemyDied.IsSummoned`가 true이면 주기 소환으로 생긴 적이다(분열체는 false). 소환체는 게이지 총량에 포함되지 않으므로 `WaveProgress`는 세지 않고, `BattleStats`의 처치 수에는 센다.

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

## 실제 흐름 확인

재생 중 `Tools > Project Nova > Playtest`를 연다. 전투 입장, 일시정지·재개·포기, 방벽 파괴, 카드 선택, Local 백엔드의 결과 제출 실패·재시도를 실제 게임 서비스로 실행한다. 에너지 소모와 결과 보상도 실제 로컬 저장에 반영된다.

임의 메시지를 발행하던 Message Debugger는 삭제했다. 표시만 바꾸는 메시지를 직접 보내면 UI와 모델이 서로 다른 상태가 될 수 있다. 구독 현황은 MessagePipe의 Diagnostics 창에서, 메시지의 계약은 EditMode 테스트에서 확인한다. 새 메시지를 추가할 때 디버그 버튼을 함께 만들 필요는 없다.
