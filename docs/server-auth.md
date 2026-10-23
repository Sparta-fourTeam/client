# 서버 인증 연결

로그인(게스트·구글)을 서버 인증 API에 연결한 구조와, 켜고 끄는 방법이다. 클라이언트의 게임 데이터는 로컬이 계속 맡는다. 서버의 플레이어 조회 연결은 별도 작업이다.

## 서버

| 항목 | 값 |
| --- | --- |
| 주소 | `https://doodledefense.duckdns.org` (`ServerConfig.BaseUrl`, 정식 HTTPS 인증서). 요청 경로가 `api/`로 시작하므로 주소에 `/api`를 붙이지 않는다. 이 도메인의 `/auth`는 웹사이트 화면이라 API 주소가 아니다 |
| 게스트 가입 | `POST /api/auth/guest/signup` → `{ guestId, tokens }` |
| 게스트 로그인 | `POST /api/auth/guest/login` `{ guestId }` → `{ accessToken, refreshToken }` |
| 소셜 로그인 | `POST /api/auth/social/login` `{ provider: GOOGLE \| APPLE, idToken }` → `{ accessToken, refreshToken }`. 처음 로그인하면 서버가 가입시킨다 |
| 토큰 재발급 | `POST /api/auth/refresh` `{ refreshToken }` → `{ accessToken, refreshToken }`. 쓴 리프레시 토큰은 무효가 된다 (아래 "토큰 만료") |

`/api/auth/web/login`은 웹용 로그인으로 보이며 게임은 쓰지 않는다 (서버 이슈 #43에서 웹·게임 토큰을 나눌 예정).

## 흐름

`LoginFlow` → `IAuthApi.Login(방식, 계정ID)`

- **게스트**: 저장된 게스트 ID가 있으면 `guest/login`, 없거나 서버가 모르면(400·401) `guest/signup`으로 새로 가입한다. 서버가 정해 준 게스트 ID는 `LoginResponse.accountId`로 돌아와 기기에 저장된다
- **구글**: 구글 로그인으로 받은 ID 토큰을 `social/login`에 보낸다. 외부 계정은 저장한 값이 오래된 토큰이라, 앱을 켤 때마다 구글 로그인을 다시 해서 새 토큰을 받는다. 기기에는 방식만 저장하고(`auth.account_id`는 `social`) ID 토큰은 남기지 않는다. 앱을 켤 때는 계정 선택창 없이 자동으로 로그인한다 (`docs/google-sign-in.md`)
- 서버 로그인에 성공하면 토큰을 `AuthSession`에 담고(요청마다 `Authorization: Bearer` 헤더), 이어서 기존 로컬 로그인(`LocalAuthApi`)을 한다. 서버 로그인에 실패하면 로그인창의 [다시 시도]를 기다린다

## 토큰 만료

`HttpApiClient`가 처리한다 (`AuthSession`이 토큰을 들고 있다).

1. 로그인한 상태로 보낸 요청이 **401**이면 액세스 토큰 만료로 본다. 서버가 요청을 처리하기 전에 거절한 것이라 다시 보내도 중복되지 않는다.
2. 리프레시 토큰으로 `POST /api/auth/refresh`를 보내 새 토큰 쌍을 `AuthSession`에 담고, **같은 요청을 한 번만** 다시 보낸다. 다시 보낸 요청이 또 401이면 그대로 던진다.
3. 재발급은 **한 번에 하나만** 보낸다. 서버는 쓴 리프레시 토큰을 무효로 하고, 이미 쓴 토큰이 또 오면 저장된 토큰까지 지워 다시 로그인하게 만든다 (Apidog 재발급 계약). 요청 여러 개가 동시에 401을 받아도 하나의 재발급을 같이 기다린다.
4. 리프레시 토큰이 거절되면(만료·이미 사용) 토큰을 비우고 `ApiException`의 코드 **`SESSION_EXPIRED`** 를 던진다. 호출부는 이 코드를 받으면 로그인으로 돌려보낸다. 재발급 전송의 네트워크 오류·시간 초과·서버 오류도 서버가 이미 토큰을 회전시켰을 수 있어 토큰을 비우고 `SESSION_EXPIRED`로 끝낸다. 서버에 멱등 재발급/유예 계약이 없으므로 같은 refresh 토큰을 다시 보내지 않는다. 일반 API 요청 자체의 통신 오류 매핑은 바뀌지 않는다.
5. Bearer 인증을 쓰는 `POST /api/auth/social/link`와 `DELETE /api/auth/me`의 401은 재발급 대상이다. 로그인·재발급 등 나머지 `api/auth/` 경로의 401은 자격 증명이 틀린 것이라 재발급하지 않는다. 로그인하지 않은 채 보낸 요청의 401도 마찬가지다.

개별 요청을 취소하면 그 요청의 대기는 즉시 끝나지만 공유 재발급 전송은 취소하지 않는다. 서버가 이미 소비한 리프레시 토큰을 다시 쓰지 않도록, 재발급은 HTTP 제한 시간 안에서 끝까지 받아 다른 요청과 새 토큰을 공유한다.

현재 클라이언트의 플레이어 API는 Local 구현이다. 서버 `GET /api/user/me`를 연결하는 별도 작업에서는 이 재발급 처리를 재사용한다. `SESSION_EXPIRED`를 받았을 때 로그인으로 돌려보내는 화면 흐름은 인증이 필요한 API를 붙일 때 함께 만든다.

## 로그아웃과 계정 삭제

`AccountService`가 저장 자격 정보를 지울 때 `SessionAuthCredentialStore`가 메모리의 `AuthSession`도 비운다. 이후 요청에는 이전 계정의 Bearer 헤더가 붙지 않는다. 로그인/초기화마다 세대가 바뀌며, 재발급 응답은 시작한 세대에만 반영한다. 로그아웃이나 새 로그인 뒤 도착한 옛 재발급 성공/실패는 새 세션을 덮어쓰거나 초기화하지 않는다. 새 세션은 자체 재발급 하나를 공유한다. 소셜 로그아웃은 기존대로 로컬 로그인 정보만 비우며 `POST /api/auth/logout` 연결은 별도 작업이다.

서버 인증을 쓰면 `IAccountApi`는 `ServerBackedAccountApi`이며, 로비 메뉴 [계정 삭제] → 두 단계 확인(닉네임 입력) → `AccountService.DeleteAccountAsync()` → **`DELETE /api/auth/me`** 순서로 호출한다. 본문 없이 현재 Bearer access 토큰을 보낸다. **204를 받은 뒤에만** 로컬 저장 파일을 지우고, 저장 자격 정보·메모리 토큰·`PlayerProfile` 진행 캐시를 비운 뒤 Loading의 로그인 선택 화면으로 돌아간다. 서버 인증을 쓰지 않으면 기존 `LocalAccountApi`가 로컬 데이터만 지운다. 서버 인증을 끄고 로컬에서 삭제해도 서버 탈퇴를 보장하지 않는다.

- 서버 책임: 계정과 하위 데이터 즉시 삭제, GAME/WEB refresh 폐기, Redis 차단으로 남은 access 토큰 무효화. 복구 유예는 없다. 같은 Google 계정으로 다음 `social/login`을 하면 서버가 **새 게임 사용자 ID와 초기 상태**를 만든다. 클라이언트는 게임 사용자 ID를 발급하거나 이전 진행을 서버로 복원하지 않는다.
- 클라이언트 책임: 204 확인 후 로컬 파일·캐시·자격 정보를 정리한다. 파일을 지우기 전에 기기에 미완료 정리를 기록한다. 파일 I/O·권한 오류가 나도 인증·캐시 초기화와 로그인 전환은 마치며, 저장소는 다음 읽기·쓰기 전에 삭제를 재시도한다. 정리가 끝나지 않으면 이전 진행을 읽지 못하므로 새 로그인도 완료되지 않는다. 다음 Google 로그인은 정상 계정 선택과 `social/login`을 거치고 새 로컬 저장으로 시작한다. UI는 확인·요청·오류 안내 중 중복 클릭을 막고 요청 중에는 [삭제 중…]을 표시한다.
- 401은 만료 access 토큰일 때 재발급 한 번만 허용한다. 재발급도 거절되면 토큰은 기존 세션 만료 정책으로 비우지만 **계정 삭제 성공으로 처리하지 않는다**. 탈퇴·게스트 로그아웃 모두 실패를 안내하고, 401/SESSION_EXPIRED 안내의 [다시 로그인]을 누르면 Loading에서 재인증한다. 닫으면 현재 화면에 남는다. 저장된 로그인 방식·원시 게스트 ID·로컬 진행은 유지해 새 게스트 가입으로 바뀌지 않게 한다.
- 서버 오류(500 포함), 미배포 경로(404), 통신 실패·시간 초과, 204 이외의 2xx는 삭제 완료로 처리하지 않는다. 로컬 진행을 유지하고 실패 안내를 띄운다. 자동 삭제 재전송은 하지 않는다. 응답 유실 시 서버에서 이미 삭제했을 수도 있으므로 완료를 단정할 수 없다.
- 게스트 로그아웃은 기존 정책대로 삭제 API를 거친다. 서버 인증 상태에서는 서버 삭제 확인 뒤에만 로그인으로 돌아간다.
- 원시 guest ID는 로그인 자격 증명이므로 문의 본문·클립보드에 포함하지 않는다.

서버 PR #54에는 삭제·연동 경로의 인증 보호와 삭제 서비스의 null 사용자 ID 거절이 반영됐다. 무토큰·만료토큰의 계약은 401이며, 실제 배포 완료는 별도로 확인해야 한다. 기존/미배포 서버에서 500이 와도 클라이언트는 이를 삭제 실패로 처리한다.

실제 기기 확인은 배포 후 전용 테스트 계정으로 한다: Google 로그인 → 진행 생성 → 삭제 확인 → 204와 로그인 화면 확인 → 같은 Google 계정 재가입 → **서버 사용자 ID가 바뀌고** 초기 상태인지 확인한다. 이전 refresh/access 토큰의 폐기·차단은 서버 검증 대상이다. 클라이언트 가짜 전송 테스트는 로컬 진행 미복원과 요청 계약을 검증하며 실제 서버의 새 ID 발급이나 데이터 삭제를 증명하지 않는다.

## 플레이어 상태 (`GET /api/user/me`)

전투 세션 발급·종료·포기의 HTTP 모듈과 Player 그룹 전환 조건은 [server-battle.md](server-battle.md)에 있다.

서버 PR #47(머지됨)이 `GET /user/me`로 플레이어 상태를 한 번에 준다. 클라이언트는 `HttpPlayerApi`가 받아 `PlayerSnapshot`으로 바꾼다 (`ServerUserMapper`). **기본은 꺼져 있다.** 서버가 배포되기 전에는 요청이 실패하므로, 개발 중에 `PlayerPrefs`의 `dev.server_player`를 `"1"`로 둘 때만 쓴다.

| 서버 필드 | 클라이언트 | 처리 |
| --- | --- | --- |
| `gold`, `energyStored` | 같은 이름 | 그대로 |
| `gem` (PR #58), 이전 `jem` | `gems` | `gem`이 있으면 0도 그대로 사용하고, 없을 때만 이전 `jem`을 읽는다 |
| `exp`(null 가능) | `exp` | null이면 0 |
| `energyUpdatedAt`(시간대 없는 `LocalDateTime`) | `energyUpdatedAt` | 클라이언트가 UTC로 읽는다 (`EnergyRule`). **서버가 UTC로 주는지 확인 필요** |
| `stageProgress[].stageId`, `codex[].monsterId`(`Long`) | `int` | 변환하고, `int` 범위를 넘으면 잘못된 응답 |
| `upgrades[]`(`upgradeId`, `level`) | 같은 이름 | 레벨이 null이면 0 |
| 닉네임 | `nickname` | 서버에 없다. 로컬 저장 값을 쓴다 |
| `items[].itemId` (PR #58 문자열) | 문자열 ID(`book.arrow`) | 문자열 ID와 수량을 그대로 반영한다. 이전 배포의 숫자 ID는 문자열로 추측 변환하지 않고 건너뛴다. 문자열의 빈 ID·음수 수량·중복 행은 잘못된 응답으로 거절한다 |
| `tutorials[]`(`Long`) | 문자열 ID(`intro_battle`) | 서버 ID와 클라이언트 ID의 대응이 없어 아직 옮기지 않는다 |

서버 PR #58은 `Item.itemId`와 `UserItemResponse.itemId`를 문자열로 맞췄다. 서버와 정해야 할 것은 튜토리얼 ID 대응, 닉네임 필드, 에너지 시각의 시간대다. 튜토리얼은 아직 숫자 ID여서 클라이언트 문자열 ID와 대응표가 필요하다.

PR #58은 `GET /api/item`, `GET /api/upgrade`, `POST /api/user/upgrades`도 추가했다. 강화 구매는 `{ upgradeId, createId }`를 받고 `{ upgradeId, level }`만 반환하며 전체 스냅샷은 주지 않는다. `unlockLevel`은 서버 구매 서비스에서 검사하지 않으므로 클라이언트 표시·진입 제한을 서버의 보안 보장으로 해석하지 않는다. 지갑 락과 중복 요청 원장이 있어도 실제 DB 동시성·스키마 마이그레이션 적용은 별도 검증 대상이다. Player 그룹·강화 구매 DI는 이 조회 호환 수정만으로 전환하지 않는다.

## 계정 연동 (`POST /api/auth/social/link`)

로비 메뉴의 [계정 연동]이 게스트 계정에 구글 계정을 붙인다. 서버 인증을 쓰는 빌드에서는 `ServerBackedAccountApi`가 서버로 보낸다 (서버 PR #54가 배포돼야 동작한다).

- 요청: `{ "provider": "GOOGLE", "idToken": "..." }` + 게스트 로그인으로 받은 액세스 토큰(`Authorization: Bearer`). 응답은 **204**
- 서버는 같은 유저에 외부 계정을 붙이므로, **연동해도 유저와 데이터는 그대로 이어진다**. 다음 실행부터는 구글 ID 토큰으로 로그인하면 같은 유저가 나온다
- 서버 PR #56은 같은 인증 사용자에 **같은 provider와 검증된 소셜 식별자**로 재시도하면 204를 반환한다. 성공 응답이 유실됐어도 같은 Google 계정으로 명시적으로 다시 연동해 복구할 수 있다. 병합과 실제 배포는 별도로 확인해야 한다.
- 거절: **401**(로그인 필요), **409**(다른 외부 계정이 이미 연동됐거나, 그 외부 계정이 이미 다른 유저에 연동됨). 다른 provider/계정의 충돌은 성공으로 간주하지 않는다. 409 두 가지는 본문에 오류 코드가 없어 클라이언트가 구분하지 않는다
- 클라이언트는 서버가 받아들인 **뒤에만** 로그인 정보를 구글로 바꾼다. 거절되거나 실패하면 게스트 그대로 두고 다시 시도할 수 있다 (`LinkResult.Conflict`, `LinkResult.Failed`와 로비 알림 문구)
- 재발급 실패 등으로 `SESSION_EXPIRED`/401을 받으면 일반 재시도 대신 [다시 로그인]을 안내한다. 기존 로그인 방식·게스트 ID·진행을 지우지 않고 Loading의 정상 로그인 흐름으로 돌아간 뒤 연동을 다시 시도한다.
- 서버 인증을 끈 환경(에디터 기본값 또는 명시적 override 0)은 로컬에서 로그인 방식만 바꾼다

## OpenAPI와 구현 차이

[서버 OpenAPI](https://doodle-defense.duckdns.org/api/v3/api-docs)의 `/api/auth/social/link`는 필수 `provider`·`idToken` JSON, `/api/auth/me`는 본문 없는 DELETE라 클라이언트 경로·본문과 같다. 공유 provider enum에는 GUEST도 있지만 소셜 요청에는 GOOGLE/APPLE만 보낸다.

명세의 생성된 `servers` URL은 `http`이고 Bearer `securitySchemes`/`security` 선언이 없다. 클라이언트는 HTTPS를 유지하고 보호된 연동·삭제 요청에 Bearer를 붙인다. 연동·삭제·로그아웃의 `responses`는 200만 기록돼 있지만, 확인한 서버 컨트롤러는 204를 반환하며 로그아웃 설명에도 204가 명시돼 있다. 삭제의 204 확인을 200 허용으로 바꾸지 않는다. 서버 명세의 성공/오류 응답과 인증 선언은 구현에 맞게 보완해야 한다. 명세 조회만으로 실제 인증·삭제·배포 동작이 검증된 것은 아니다.

## 오류 확인

서버가 거절하면 `ApiException`의 `Code`는 `HTTP_상태코드`이고(게스트 재가입 판단이 여기에 기댄다), 서버가 본문에 적은 `message`는 `Detail`에 담겨 로그에 `(설명)`으로 붙는다. 예: `Rejected: HTTP_401 (소셜 로그인 정보가 올바르지 않습니다)`. 화면에 보여 주거나 분기에 쓰지 않는다.

## 켜고 끄기

`ServerConfig.UseServerAuth`로 정한다.

| 환경 | 기본값 | 이유 |
| --- | --- | --- |
| Android 개발 빌드 | 켬 | Android는 서버 인증·계정 API를 기본으로 사용한다 |
| 에디터 | 끔 | 가짜 구글 로그인의 토큰은 서버가 검증하지 못한다 |
| Android 릴리스 빌드 | 켬 | 비공개 테스트와 일반 출시도 서버 로그인·계정 삭제를 사용한다 |

`UseServerPlayer`와 `UseServerProgress`는 `UseServerAuth`와 같은 값이다. 서버 인증이 켜지면 진행 데이터도 서버 진행 API를 사용한다. 서버 인증이 꺼진 에디터에서는 진행 데이터를 로컬에 유지한다.

에디터에서는 **Tools → Project Nova → Server Authentication** 메뉴를 눌러 켜고 끈다. 체크 표시가 있으면 서버 인증을 사용한다. 재생을 중지한 상태에서 변경하고 Boot부터 다시 재생한다. 게스트로 서버 로그인을 시험할 수 있으며 에디터의 가짜 구글 로그인은 서버에서 실패한다. 선택은 프로젝트 폴더별 EditorPrefs에 저장되어 에디터를 다시 열어도 유지되며, 에디터는 `dev.server_auth` PlayerPrefs를 사용하지 않는다.

기기 빌드에서는 `PlayerPrefs`의 `dev.server_auth`에 `"1"`(켬) 또는 `"0"`(끔)을 넣어 기본값을 바꿀 수 있다. 이 override는 개발·릴리스 모두 기본값보다 우선한다. 기존 설치에 `"0"`이 남아 있으면 Android release도 로컬 인증·로컬 계정 삭제를 사용한다.

## `BackendSettings`와의 차이

`conventions.md`는 서버 전환을 `BackendSettings`(ScriptableObject) 스위치로 한다고 적었지만 코드에는 아직 없다. 지금은 `ServerConfig`가 Account 그룹의 `IAuthApi`와 `IAccountApi`를 서버로 바꾸는 임시 스위치다. `IDataApi`를 포함해 Account 그룹 전체와 Player 그룹을 서버로 옮길 때 `BackendSettings`로 합친다.

## 아직 안 한 것

- `SESSION_EXPIRED`를 받았을 때 로그인으로 돌려보내는 화면 흐름 (인증이 필요한 API를 붙일 때)
- 서버 쪽 환경변수 `GOOGLE_CLIENT_IDS`에 웹 클라이언트 ID가 들어가야 구글 로그인이 통과한다 (`docs/google-sign-in.md`)
- 서명된 비개발 Android release에서 실제 Google ID 토큰·서버 로그인·계정 연동·삭제 확인 (서버 머지·배포와 별도로 검증)
- 한 기기에서 계정을 바꾸면 로컬 저장 파일을 같이 쓴다 (로컬 데이터는 서버로 옮기지 않는다)

## 공개 프로필: 닉네임·초상·표시 ID

서버 인증에서는 `GET /api/user/profile`로 `{userId, nickname, avatarId}`를 조회하고 `POST /api/user/profile`에 바꿀 `nickname` 또는 `avatarId`만 보낸다. 서버 PR [#92](https://github.com/Sparta-fourTeam/server/pull/92)의 배포가 필요하다. 공개 표시 ID는 서버 유저 ID이며 로그인 자격 정보인 `guestId`를 표시하거나 복사하지 않는다.

- 닉네임은 한글 완성형·영문·숫자·밑줄 2~20자다. 서버는 앞뒤 공백·NFKC를 정규화하고 대소문자를 무시한 중복을 409로 거절한다. 중복 시 입력한 이름을 유지한 채 재입력하고, 기존 프로필은 유지한다.
- 초상 ID는 0~4다. 프로필 초상이나 상단 프로필 카드의 초상을 눌러 선택한다. 저장이 성공하면 두 화면을 함께 갱신한다.
- 서버 진행 모드에서는 로컬 저장소 없이 프로필을 조회하며, 진행을 다시 조회할 때도 닉네임·초상을 합친다. 로그아웃·계정 전환 후 도착한 이전 프로필 응답은 반영하지 않는다.
- 서버 인증과 로컬 진행을 함께 쓰는 개발 모드에서는 성공한 서버 프로필을 로컬에 캐시한다. 로컬 이름을 서버 계정에 자동 이관하지 않는다.
- 로컬 인증 모드는 기존 `Nameless_` 기본 닉네임과 로컬 공개 UUID를 유지하고, 이름·초상도 로컬에 저장한다. 중복 검사는 서버 인증에서 적용한다.

## 로비 채팅

`GET /api/chat/recent`로 최근 30개를 받은 뒤 순수 WebSocket `/api/ws-chat`에 같은 Bearer 토큰을 핸드셰이크 헤더로 넣어 연결한다. STOMP CONNECT 후 `/topic/chat`과 `/topic/chat/presence`를 구독한다. 전송은 `/app/chat.send`에 `{content}`만 보내며 화면에는 서버의 방송을 받은 뒤 추가한다.

접속자 수는 서버가 보낸 `{onlineCount}`로 표시하고, 아직 받지 않았으면 ‘접속자 확인 중’으로 둔다. 초상·닉네임 확장 필드가 없는 메시지도 발신자 ID와 기본 초상으로 표시한다. 창을 닫은 동안 받은 새 메시지는 숫자 배지로 알린다. 이모지 한 개만 보낸 메시지는 크게 표시하고, 입력창·이모지 선택창 높이가 바뀌면 대화 목록의 아래쪽 읽기 위치를 유지한다.
