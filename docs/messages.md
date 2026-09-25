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
| `EnemyDied` | 사건 | EnemySpawner |
| `WaveStarted` | 사건 | WaveManager |
| `AllWavesSpawned` | 사건 | WaveManager |
| `WallHpChanged` | Buffered | Wall |
| `WallDestroyed` | 사건 | Wall |
| `ExpChanged` | Buffered | ExperienceSystem |
| `LevelUp` | 사건 | ExperienceSystem |
| `SkillChanged` | 사건 | SkillInventory |
| `StageEnded` | 사건 | StageJudge |
| `StageStateChanged` | Buffered | StageManager |
| `StartFailed` | 사건 | StageManager |
| `StageResult` | 사건 | StageManager |
| `SubmitFailed` | 사건 | StageManager |
| `SubmitRejected` | 사건 | StageManager |

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
