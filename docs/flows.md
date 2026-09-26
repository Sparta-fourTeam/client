# 주요 흐름

실행 중에 무엇이 어떤 순서로 일어나는지를 다룬다.

> 그림 읽는 법: 초록은 로직, 주황은 뷰, 회색은 서버 통신. 실선은 메시지, 점선은 직접 호출.

## 스테이지 생명주기와 서버 통신 구간

```mermaid
flowchart TD
    A[로그인<br/>미전송 결과 전송, 테이블 동기화] --> B[스테이지 시작<br/>battleId 발급]
    B --> P[전투 진행<br/>서버 없이 클라이언트만]
    P --> CL[Clear]
    P --> FA[Fail]
    CL --> S[결과 전송<br/>검증, 멱등성, 재시도]
    FA --> S
    S --> R[결과 화면<br/>서버 잔액으로 보상 표시]
    classDef logic fill:#0f5445,stroke:#2f9b7c,color:#e6f4ef
    classDef ui fill:#7a2e15,stroke:#d0643a,color:#fdebe3
    classDef server fill:#4a4a46,stroke:#8a8a84,color:#f0f0ec
    classDef bus fill:#1c1c1c,stroke:#9a9a9a,color:#ffffff
    class P,CL,FA logic
    class R ui
    class A,B,S server
```

회색 세 곳만 서버와 통신한다.

### 실패와 복구

| 어디서 | 무슨 일이 | 그다음 |
| --- | --- | --- |
| 스테이지 시작 | `DATA_OUTDATED` | Boot로 이동해 재동기화 후 다시 발급 |
| 스테이지 시작 | `INSUFFICIENT_ENERGY`, `STAGE_LOCKED` | Lobby로 이동 |
| 스테이지 시작 | 그 밖의 실패 | 재시도 또는 Lobby |
| 전투 진행 | 일시정지에서 포기하기 | 판정 없이 결과 전송으로 |
| 결과 전송 | 네트워크·서버 오류 | 자동 재시도 3회 → 수동 재시도 버튼 |
| 결과 전송 | 거절 (만료, 검증 실패 등) | 거절 안내 후 Lobby |
| 결과 전송 중 | 앱 강제 종료 | 다음 로그인 때 "미전송 결과 전송"에서 보냄 |

### 꼭 지킬 것

- Clear·Fail은 상태가 아니라 결과값이다. 실패해도 새 상태를 만들지 않고 같은 상태에 머문 채 실패 메시지만 방송한다
- 미전송 결과는 새 전투 발급보다 **반드시 먼저** 보낸다. 새 발급이 진행 중 전투를 포기 처리하기 때문이다
- 재시도 요청은 처음 만든 스냅샷을 그대로 쓴다. 매번 새로 만들면 `playTime`이 달라져 다른 요청이 된다
- 골드는 `+=`가 아니라 서버 잔액으로 덮어쓴다. 재전송 때 두 번 표시되는 것을 막는다

## 씬 흐름

```mermaid
flowchart LR
    Boot[Boot<br/>로그인, 테이블 동기화] --> Lobby[Lobby<br/>로비]
    Lobby --> Stage[Stage<br/>전투]
    Stage -->|DATA_OUTDATED| Boot
    Stage -->|INSUFFICIENT_ENERGY, STAGE_LOCKED| Lobby
    Lobby --> Stage
```

씬은 Boot, Lobby, Stage 셋뿐이다. 인터페이스(`ISceneNavigator` 등)는 Core, 전환 구현은 Boot에 있다([architecture.md](architecture.md)).

## 웨이브 게이지와 카드 선택

```mermaid
flowchart TD
    A[웨이브<br/>적 처치] -->|EnemyDied| B[웨이브<br/>게이지 증가]
    B -->|WaveGaugeChanged| H[HUD<br/>게이지 표시]
    B -->|WaveGaugeFilled| C[스테이지 흐름<br/>CardSelect 상태, 게임 정지]
    C -->|StageStateChanged| D[카드 선택 팝업<br/>선택지 3개]
    D -.->|PickCard| SM[스테이지 흐름<br/>StageManager]
    SM -->|CardPicked| E[성장<br/>카드 획득·강화]
    E -->|SkillChanged| F[HUD<br/>스킬 아이콘]
    E -->|SkillChanged| G[성장<br/>시전·패시브 반영]
    classDef logic fill:#0f5445,stroke:#2f9b7c,color:#e6f4ef
    classDef ui fill:#7a2e15,stroke:#d0643a,color:#fdebe3
    classDef server fill:#4a4a46,stroke:#8a8a84,color:#f0f0ec
    classDef bus fill:#1c1c1c,stroke:#9a9a9a,color:#ffffff
    class A,B,C,SM,E,G logic
    class D,F,H ui
```

카드를 고르면 팝업이 `StageManager.PickCard(cardId)`를 호출하고(호출), `CardPicked`는 StageManager가 발행한다. 웨이브당 카드는 정확히 1장이고, 마지막 웨이브는 카드 없이 클리어한다.

- 일시정지 후 CardSelect로 돌아올 때는 선택지를 새로 뽑지 않는다. 일시정지로 리롤하는 꼼수를 막기 위해서다

## 스테이지 상태 머신

여기 없는 전환은 무시된다.

```mermaid
stateDiagram-v2
    [*] --> Starting
    Starting --> Playing: 전투 발급 성공
    Playing --> Paused: 일시정지, 백그라운드
    Playing --> CardSelect: WaveGaugeFilled 수신
    Playing --> Submitting: StageEnded 수신
    Paused --> Playing: 계속하기
    Paused --> CardSelect: 카드 선택 중이었으면 복귀
    Paused --> Submitting: 포기하기
    CardSelect --> Playing: 카드 선택 완료
    CardSelect --> Paused: 백그라운드
    Submitting --> Finished: 전송 성공
    Finished --> [*]
```
