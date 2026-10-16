# 서버 전투 계약

`HttpBattleSessionApi`는 서버의 발급·결과 제출·포기를 담당한다. `HttpApiClient`와 인증 토큰 저장소를 전달해 사용한다. end가 보상·진척을 변경해도 응답은 비어 있으므로 `IBattleApi.SubmitResult`를 구현하거나 런타임 `LocalBattleApi`를 교체하지 않는다. 서버와 Local의 재화 변경을 한 전투에서 혼합하지 않는다.

## 요청과 응답

기준은 서버 PR #59(dev `e42def63a609ecaa68097e7c0bd449321e7d5788`)와 [배포 OpenAPI](https://doodle-defense.duckdns.org/api/v3/api-docs)다. 사용자 ID는 모든 경로에서 Bearer 토큰으로 결정한다.

| 경로 | 본문 | 성공 |
| --- | --- | --- |
| `POST /api/battle/start` | `{ "createId": "클라이언트 UUID" }` | `{ "battleId": "서버 발급 ID" }` |
| `POST /api/battle/end` | `{ "battleId": "서버 발급 ID", "stageId": 1, "clearedWave": 10, "rating": 2 }` | 200, 본문 없음 |
| `POST /api/battle/cancel` | `{ "battleId": "서버 발급 ID" }` | 200, 본문 없음 |

end에는 `BattleEndRequest`를 사용한다. 네 필드는 모두 필수이며 `clearedWave`는 0~20, `rating`은 0~3이다. DTO 생성 시 빈 battleId와 범위 밖 결과를 거절한다. `stageId`는 서버 카탈로그의 정수 ID이며 명세에는 최소값 제약이 없다. Local 테이블의 스테이지와 일치하는지 확인 없이 임의 변환하지 않는다.

start에는 `userId`, `stageId`, 데이터 개정, seed를 보내지 않는다. 서버는 새 키면 에너지 5를 소모하고 기존 세션을 덮어쓴다. 현재 세션과 키가 같으면 기존 battleId를 반환한다. cancel은 세션 삭제만 하며 환불 계약은 없다. end/cancel의 ID 불일치는 400, 세션 미존재는 404다.

PR #59의 end는 트랜잭션 안에서 세션을 먼저 삭제해 선점한 후 기본 경험치·골드·아이템을 지급하고 최고 클리어 등급을 갱신한다. 세션 삭제 실패 요청은 보상을 지급하지 않는다. 실패 시 롤백·동시성의 실제 DB 보장은 클라이언트 fixture 테스트로 입증하지 않는다. 성공 본문에 보상 내역·지갑·진행도 스냅샷은 없으므로 이를 만들어 반환하거나 Local 보상을 지급하지 않는다.

## 재시도와 불확실한 결과

입장 동작 하나에 `new BattleStartRequest()`를 한 번 만들고 성공 여부가 불확실하면 **같은 객체**로 `StartBattle(request)`를 재호출한다. 새 객체는 새 키이므로 이전 발급이 성공했다면 에너지가 다시 차감될 수 있다. 요청 객체의 `createId`는 읽기 전용 UUID이며 시작 메서드 내부에서 매번 생성하지 않는다. 401 토큰 재발급은 기존 HTTP 클라이언트가 같은 직렬화 본문으로 한 번만 재전송한다.

일반 네트워크 오류·시간 초과·5xx는 자동 재전송하지 않는다. start 성공 응답에 battleId가 없으면 `INVALID_RESPONSE`로 거절하며, 서버가 발급했을 가능성이 있으므로 같은 요청 객체를 유지한다.

end 응답이 유실되면 보상·진척이 이미 적용됐을 수 있다. 같은 요청을 다시 보낸 뒤 404를 받아도 이미 완료했는지 원래 없었는지 구분할 수 없으므로 성공으로 바꾸지 않는다. cancel도 동일하게 404를 성공으로 간주하지 않는다. 호출자가 오류를 숨기거나 로컬 보상을 대신 지급해서는 안 된다. 실제 연결 전 사용자 스냅샷 재조회·중복 지급 방지·결과 복구 정책이 필요하다.

이 모듈은 요청을 디스크에 저장하지 않는다. 앱 종료 후 복구·계정 전환 시 요청 수명·동시 입장 방지는 Player 그룹 연결 시 정해야 한다. 끝난 세션의 키는 서버에서 지워지므로 영구 멱등성은 아니다. start의 중복 키 조회와 에너지 락 획득은 별도이므로 동일 키 동시 요청의 DB 기반 검증도 필요하다.

## Player 그룹 연결 전 남은 정합성

- PR #59는 `GET /api/stage`, `POST /api/user/stage-progress/claim`을 추가했다. 별 보상 수령 응답은 stageId·clearRating·claimedRating이며 전체 지갑·아이템 상태가 아니다. `user/me` 스냅샷 재조회와 Player 그룹 6개 API의 상태 일관성을 함께 확인해야 한다.
- end의 stageId·clearedWave·rating은 클라이언트가 보내며 시작 세션에 스테이지·판정 근거가 묶이지 않는다. 서버 검증을 보장한다고 해석하지 않는다. 클라이언트 등급 계산과 서버 지급 결과·표시를 맞춰야 하며, 보상 계산을 Local 공식으로 복제하지 않는다. PR #59의 20웨이브 계산은 finalCoin/finalAmount 값 자체와 다를 수 있어 카탈로그의 의미도 확인해야 한다. 이를 클라이언트에서 임의 보정하지 않고 현재 서버의 지급 계약으로 취급한다.
- PR #58의 강화 구매는 `{ upgradeId, createId }`를 받고 `{ upgradeId, level }`만 반환한다. 전체 스냅샷은 별도 조회이며 서버는 unlockLevel을 검사하지 않는다.
- 서버 오류 본문은 status·error·message·path이며 기계 판독용 도메인 코드가 없다. 409는 에너지 부족과 락 충돌 모두 가능하므로 `HTTP_409`를 유지하고 message 문자열로 구분하지 않는다.
- 서버 Clock은 Asia/Seoul이고 에너지 시각은 offset 없는 LocalDateTime이다. 클라이언트 UTC 해석·Energy 테이블의 최대값/회복주기/입장 비용, 실제 DB 시각을 확인해야 한다.
- 도감 보상·에너지 회복·상점/퀘스트/튜토리얼 변경 API, 튜토리얼 ID 대응 및 전체 테이블 본문 전환은 여전히 남아 있다. 아이템 문자열 ID·gem/jem 호환 수정만으로 전체 Player 전환이 완료되지는 않는다.

OpenAPI 변경은 명세 배포의 증거이며 실제 보상 트랜잭션이나 DB 마이그레이션 완료의 증거는 아니다. 운영 상태 변경 요청 없이 계약만 확인하며, 실제 계정·동시성 검증은 로컬/테스트 DB에서 수행해야 한다.
