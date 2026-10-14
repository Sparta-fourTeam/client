# 서버 인증 연결

로그인(게스트·구글)을 서버 인증 API에 연결한 구조와, 켜고 끄는 방법이다. 서버에는 아직 인증 외의 플레이어 API가 없어서 게임 데이터는 로컬이 계속 맡는다.

## 서버

| 항목 | 값 |
| --- | --- |
| 주소 | `https://doodle-defense.duckdns.org` (`ServerConfig.BaseUrl`, 정식 HTTPS 인증서). 요청 경로가 `api/`로 시작하므로 주소에 `/api`를 붙이지 않는다. 이 도메인의 `/auth`는 웹사이트 화면이라 API 주소가 아니다 |
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
4. 리프레시 토큰이 거절되면(만료·이미 사용) 토큰을 비우고 `ApiException`의 코드 **`SESSION_EXPIRED`** 를 던진다. 호출부는 이 코드를 받으면 로그인으로 돌려보낸다. 네트워크 오류나 서버 오류로 재발급에 실패하면 로그인이 풀린 것이 아니므로 토큰을 그대로 두고 그 오류를 던진다.
5. 로그인·재발급 경로(`api/auth/`)의 401은 자격 증명이 틀린 것이라 재발급하지 않는다. 로그인하지 않은 채 보낸 요청의 401도 마찬가지다.

개별 요청을 취소하면 그 요청의 대기는 즉시 끝나지만 공유 재발급 전송은 취소하지 않는다. 서버가 이미 소비한 리프레시 토큰을 다시 쓰지 않도록, 재발급은 HTTP 제한 시간 안에서 끝까지 받아 다른 요청과 새 토큰을 공유한다.

아직 인증이 필요한 서버 API가 없어서 실제 요청에서는 쓰이지 않는다. `SESSION_EXPIRED`를 받았을 때 로그인으로 돌려보내는 화면 흐름은 인증이 필요한 API를 붙일 때 함께 만든다.

## 오류 확인

서버가 거절하면 `ApiException`의 `Code`는 `HTTP_상태코드`이고(게스트 재가입 판단이 여기에 기댄다), 서버가 본문에 적은 `message`는 `Detail`에 담겨 로그에 `(설명)`으로 붙는다. 예: `Rejected: HTTP_401 (소셜 로그인 정보가 올바르지 않습니다)`. 화면에 보여 주거나 분기에 쓰지 않는다.

## 켜고 끄기

`ServerConfig.UseServerAuth`로 정한다.

| 환경 | 기본값 | 이유 |
| --- | --- | --- |
| Android 개발 빌드 | 켬 | 기기 확인이 끝나기 전이라 개발 빌드에서만 켠다 |
| 에디터 | 끔 | 가짜 구글 로그인의 토큰은 서버가 검증하지 못한다 |
| 릴리스 빌드 | 끔 | 기기 확인 뒤에 켠다 (주소가 HTTPS라 릴리스에서도 연결된다) |

개발 중에는 `PlayerPrefs`의 `dev.server_auth`에 `"1"`(켬) 또는 `"0"`(끔)을 넣어 기본값을 바꿀 수 있다. 에디터에서 게스트만 서버로 시험해 보려면 `"1"`로 둔다 (구글은 가짜라 실패한다).

## `BackendSettings`와의 차이

`conventions.md`는 서버 전환을 `BackendSettings`(ScriptableObject) 스위치로 한다고 적었지만 코드에는 아직 없다. 지금은 `ServerConfig`가 Account 그룹의 `IAuthApi`만 서버로 바꾸는 임시 스위치다. `IDataApi`를 포함해 Account 그룹 전체와 Player 그룹을 서버로 옮길 때 `BackendSettings`로 합친다.

## 아직 안 한 것

- `SESSION_EXPIRED`를 받았을 때 로그인으로 돌려보내는 화면 흐름 (인증이 필요한 API를 붙일 때)
- 릴리스 빌드에서 서버 인증 켜기 (기기 확인 뒤)
- 서버 쪽 환경변수 `GOOGLE_CLIENT_IDS`에 웹 클라이언트 ID가 들어가야 구글 로그인이 통과한다 (`docs/google-sign-in.md`)
- 게스트 → 구글 계정 연동 (서버 이슈 #42)
- 한 기기에서 계정을 바꾸면 로컬 저장 파일을 같이 쓴다 (로컬 데이터는 서버로 옮기지 않는다)
