# 서버 전투 세션 계약

`HttpBattleSessionApi`는 서버의 세션 발급·종료·포기만 담당한다. `HttpApiClient`와 인증 토큰 저장소를 전달해 사용한다. `IBattleApi.SubmitResult`의 보상·등급 계약과 다르므로 런타임 DI의 `LocalBattleApi`를 교체하지 않는다. 서버와 Local의 재화 변경을 한 전투에서 혼합하지 않는다.

## 요청과 응답

기준은 서버 PR #57의 dev 구현과 [배포 OpenAPI](https://doodle-defense.duckdns.org/api/v3/api-docs)다. 사용자 ID는 모든 경로에서 Bearer 토큰으로 결정한다.

| 경로 | 본문 | 성공 |
| --- | --- | --- |
| `POST /api/battle/start` | `{ "createId": "클라이언트 UUID" }` | `{ "battleId": "서버 발급 ID" }` |
| `POST /api/battle/end` | `{ "battleId": "서버 발급 ID" }` | 200, 본문 없음 |
| `POST /api/battle/cancel` | `{ "battleId": "서버 발급 ID" }` | 200, 본문 없음 |

start에는 `userId`, `stageId`, 데이터 개정, seed를 보내지 않는다. 서버는 새 키면 에너지를 소모하고 사용자의 기존 세션을 덮어쓴다. 현재 세션과 키가 같으면 기존 battleId를 반환한다. end/cancel은 세션 ID가 다르면 400, 세션이 없으면 404다. 보상·진척 반영과 포기 시 환불 계약은 없다.

## 시작 요청 재시도

입장 동작 하나에 `new BattleStartRequest()`를 한 번 만들고 성공 여부가 불확실하면 **같은 객체**로 `StartBattle(request)`를 재호출한다. 새 객체는 새 키이므로 이전 발급이 성공했다면 에너지가 다시 차감될 수 있다. 요청 객체의 `createId`는 읽기 전용 UUID이며 시작 메서드 내부에서 매번 생성하지 않는다. 401 토큰 재발급도 기존 HTTP 클라이언트가 같은 직렬화 본문으로 한 번만 재전송한다.

일반 네트워크 오류·시간 초과·5xx는 자동 재전송하지 않는다. 성공 응답에 battleId가 없으면 `INVALID_RESPONSE`로 거절한다. 이때도 서버가 발급했을 가능성이 있으므로 재시도할 때 같은 객체를 유지한다. end/cancel의 응답 유실 뒤 404는 이미 삭제했을 수도, 원래 없었을 수도 있어 성공으로 바꾸지 않는다.

이 모듈은 요청을 디스크에 저장하지 않는다. 앱 종료 후 복구·계정 전환 시 요청 수명·동시 입장 방지는 실제 Player 그룹 연결 시 정해야 한다. 끝난 세션의 키는 서버에서 지워지므로 영구 멱등성이나 동시 요청의 중복 차감 방지를 보장하지 않는다. 서버 start는 중복 키 조회 후 별도로 에너지 락을 얻으므로, 같은 키를 동시에 보내는 경우 DB 기반 검증이 필요하다.

## Player 그룹 전환 전 필요한 계약

- 서버 end는 세션 삭제만 구현되어 있다. 클라이언트의 `SubmitResultRequest`와 확정 보상 응답, 재제출 원장, 스테이지 진척·도감 등록 처리가 필요하다. 빈 성공을 `SubmitResultResponse`로 꾸미거나 Local로 보상을 지급하지 않는다.
- 서버 PR #58은 아이템·강화 조회와 `POST /api/user/upgrades`를 추가했다. 구매 요청은 `{ upgradeId, createId }`, 응답은 `{ upgradeId, level }`이며 전체 스냅샷은 별도 조회가 필요하다. 서버 구매 서비스는 `unlockLevel`을 검사하지 않는다. 클라이언트의 경험치 기반 진입 제한을 서버 보안 보장으로 해석하지 않는다.
- 스테이지·도감 보상 수령, 에너지 회복, 상점·퀘스트·튜토리얼 변경 API는 여전히 없다. 서비스의 `grantEnergy`만으로 외부 API가 있다고 판단하지 않는다.
- 서버 오류 본문은 `status`, `error`, `message`, `path`이며 기계 판독용 도메인 코드가 없다. 409는 `INSUFFICIENT_ENERGY`와 `ENERGY_LOCK_CONFLICT` 모두 가능하므로 현재 `HTTP_409`를 유지한다. message 문자열로 둘을 구분하지 않는다.
- 서버 `ClockConfig`는 `Asia/Seoul`이고 `UserService`는 이 Clock의 시간대 없는 LocalDateTime을 쓴다. 기존 `ServerUserMapper`/`EnergyRule`은 UTC로 읽으므로 실제 전환 전 offset 포함 시각 계약과 기존 DB 시각 확인이 필요하다. 서버 최대 에너지·회복 주기·입장 비용과 클라이언트 Energy 테이블의 일치도 함께 확인한다.
- PR #58의 아이템 ID는 문자열로 정리됐다. `user/me`의 `jem`은 `gem`으로 바뀌므로 이전 클라이언트 매핑은 호환 수정이 필요하다. 튜토리얼 ID 대응과 전체 테이블 본문 계약은 여전히 미확정이다. 인증·user/me·버전·스킬·아이템·강화 조회 API가 있어도 전체 게임 데이터 전환이 완료된 것은 아니다.

OpenAPI에 새 필드와 경로가 나타나는 것은 명세 배포의 증거다. 보호된 API의 실제 처리와 `battle_session.create_id` 컬럼·유일 제약 등 DB 적용을 증명하지 않는다. 운영 상태 변경 요청 없이 이를 확인할 수 없으므로, 실제 검증은 서버 담당자가 로컬/테스트 DB에서 진행해야 한다.
