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
| `EnemyDied` | 사건 | EnemyModel |
| `EnemyHpChanged` | 사건 | EnemyModel |
| `SpecialEnemyWarning` | 사건 | EnemySpawner |
| `WaveGaugeChanged` | Buffered | WaveProgress |
| `WaveGaugeFilled` | 사건 | WaveProgress |
| `WaveGaugeChanged` | Buffered | WaveProgress |
| `WaveGaugeFilled` | 사건 | WaveProgress |
| `AllEnemiesCleared` | 사건 | EnemySpawner |
| `CardPicked` | 사건 | StageManager |
| `SkillChanged` | Buffered | WeaponController |
| `SkillFired` | 사건 | SkillController |
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

`EnemyDied.Type`은 사망한 모델의 등급이다. `StageManager`는 Elite 사망을 한 번씩 룰렛 보상에 넣는다. 룰렛과 웨이브 카드 보상이 겹치면 대기열로 차례로 처리하고 모두 끝난 뒤 전투를 재개한다.

`SpecialEnemyWarning`은 엘리트·보스가 나오기 `Seconds` 전에 발행한다. `EnemySpawner`가 그만큼 스폰을 늦추므로 경고 UI는 이 시간만큼 재생하면 된다. 발행자를 넘기지 않으면(테스트) 경고 없이 바로 나온다.

`EnemyHpChanged.Damage`는 저항·취약·남은 HP를 반영한 실제 차감량의 `DamageInfo`다. 피해 출처와 `IsImpact`, `IsCritical`을 보존하므로 직접 타격과 지속·부가 피해의 반응을 구분할 수 있다. 회복과 기존 3인자 생성자는 기본 `DamageInfo`를 사용한다. 발행 순서는 HP 변경 → 사망이며 피해·상태·보상 시점은 바뀌지 않는다.

`SkillFired`는 주 스킬이 대상을 향해 시전될 때 한 번 발행하며 `BattleSoundPlayer`가 무기별 시전음에 사용한다. `Duration`은 시전 당시 강화가 반영된 광선 지속시간(게임 시간)이며 다른 공격은 0이다. 에너지 빔 시전음은 이 시간에 맞춰 유지한 뒤 공격 종료 전 마지막 실제 0.3초 동안 페이드아웃한다. 추가 투사체·자식 공격은 별도 시전음을 겹쳐 내지 않는다. 무기별 명중음은 `EnemyHpChanged.Damage`의 스킬 ID와 실제 양수 피해를 사용하고, 무효 피해·회복·스킬 출처 없는 지속 피해에는 재생하지 않는다. 연속 타격의 소리 간격은 `AudioLibrary`에서 제한한다.

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
