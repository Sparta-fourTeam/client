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
| 스테이지 시작 | `DATA_OUTDATED` | 테이블 재동기화 후 다시 발급 |
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

## 스킬 선택

```mermaid
flowchart TD
    A[웨이브<br/>적 사망] -->|EnemyDied| B[성장<br/>경험치 누적]
    B -->|LevelUp| C[스테이지 흐름<br/>레벨업 상태, 게임 정지]
    C -->|StageStateChanged| D[스킬 선택 팝업<br/>선택지 3개]
    D -.->|Upgrade| E[성장<br/>스킬 획득·강화]
    E -->|SkillChanged| F[HUD<br/>스킬 아이콘]
    E -->|SkillChanged| G[성장<br/>시전·패시브 반영]
    classDef logic fill:#0f5445,stroke:#2f9b7c,color:#e6f4ef
    classDef ui fill:#7a2e15,stroke:#d0643a,color:#fdebe3
    classDef server fill:#4a4a46,stroke:#8a8a84,color:#f0f0ec
    classDef bus fill:#1c1c1c,stroke:#9a9a9a,color:#ffffff
    class A,B,C,E,G logic
    class D,F ui
```

선택이 끝나면 팝업이 스테이지 흐름에 알린다(호출). 남은 레벨업이 있으면 새 선택지가 다시 뜨고, 없으면 전투로 돌아간다.

- 일시정지 후 레벨업으로 돌아올 때는 선택지를 새로 뽑지 않는다. 일시정지로 리롤하는 꼼수를 막기 위해서다

## 스테이지 상태 머신

여기 없는 전환은 무시된다.

```mermaid
stateDiagram-v2
    [*] --> Starting
    Starting --> Playing: 전투 발급 성공
    Playing --> Paused: 일시정지, 백그라운드
    Playing --> LevelUp: LevelUp 수신
    Playing --> Submitting: StageEnded 수신
    Paused --> Playing: 계속하기
    Paused --> LevelUp: 레벨업 중이었으면 복귀
    Paused --> Submitting: 포기하기
    LevelUp --> LevelUp: 남은 레벨업 있음
    LevelUp --> Playing: 스킬 선택 완료
    LevelUp --> Paused: 백그라운드
    LevelUp --> Submitting: 레벨업 중 클리어
    Submitting --> Finished: 전송 성공
    Finished --> [*]
```
