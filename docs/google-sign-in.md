# Google 로그인 (Android)

로그인창과 계정 연동의 [구글] 버튼이 쓰는 Android Google 로그인 구조와 설정 방법이다.

## 구조

| 위치 | 역할 |
| --- | --- |
| `Assets/Plugins/Android/GoogleSignIn.androidlib` | Android 라이브러리. Credential Manager의 "Google 계정으로 로그인" 창을 띄워 Google ID 토큰을 받는다 (`GoogleSignInBridge.java`). 의존성은 이 폴더의 `build.gradle`에만 있고 Unity 기본 Gradle 템플릿은 건드리지 않는다 |
| `Network/AndroidGoogleSignIn.cs` | `ISocialSignIn` 구현. 브리지를 불러 ID 토큰을 돌려준다. 취소하면 null, 실패하면 `GoogleSignInException` |
| `Boot/RootLifetimeScope.cs` | `ISocialSignIn`으로 등록. `LoginFlow`(로그인창)와 `AccountService`(계정 연동)가 같이 쓴다 |

- **에디터**에서는 `EditorGoogleSignIn`(가짜)이 등록된다. 고정 계정 ID로 바로 성공해서 구글 버튼 → 확인창 → 로비 흐름만 확인할 수 있다. 진짜 ID 토큰이 아니라서 서버에는 쓸 수 없고, `#if UNITY_EDITOR`라 빌드에는 들어가지 않는다
- Android 이외의 빌드에서는 `AndroidGoogleSignIn.IsAvailable`이 false라 버튼이 "준비 중"으로 막힌다. 웹 클라이언트 ID가 비어 있어도 마찬가지다
- 받은 ID 토큰은 서버 `POST /api/auth/social/login`에 `{ "provider": "GOOGLE", "idToken": "..." }`로 보낸다 (`docs/server-auth.md`). 이 요청에는 클라이언트 ID를 싣지 않는다. 서버는 토큰 안의 `aud`가 자기 `GOOGLE_CLIENT_IDS`에 있는지만 확인한다

## 앱을 켤 때의 자동 로그인

저장된 로그인 방식이 구글이면 앱을 켤 때마다 구글 로그인을 다시 해서 새 토큰을 받는다. 이때는 `ISocialSignIn.SignInSilentAsync`를 불러 **계정 선택창 없이** 로그인한다.

- 브리지는 `GetGoogleIdOption`(이미 이 앱에 로그인한 계정만, 자동 선택)으로 먼저 시도한다. 그런 계정이 없으면 `NO_CREDENTIAL`이라 계정 선택창으로 넘어간다
- 로그인창에서 사용자가 구글을 직접 고른 경우는 `SignInAsync`라서 계정 선택창이 뜬다
- 한 기기에 승인한 계정이 여러 개면 시스템이 계정을 고르는 창을 띄울 수 있다. 구글이 로그인 중에 잠깐 안내를 보여 주는 것도 앱에서 없앨 수 없다
- 기기에는 방식만 저장한다(`auth.account_id`는 `social`). 구글 ID 토큰은 오래되어 쓸 수 없고 민감해서 저장하지 않는다 (`AuthCredentials`)

## 설정 (한 번만)

1. **Google Cloud 콘솔**에서 OAuth 동의 화면을 만든다. "테스트" 상태면 등록한 테스트 사용자만 로그인할 수 있다
2. 같은 프로젝트에 OAuth 클라이언트를 두 개 만든다
   - **웹 애플리케이션** (만들었음): `961722707844-cbu71g50il4274edtmv39i6ctrqqjvj5.apps.googleusercontent.com`. 이 ID는 `AndroidGoogleSignIn.WebClientId`에 들어 있고, 서버 환경변수 `GOOGLE_CLIENT_IDS`에도 같은 값을 넣어야 한다. ID 토큰의 `aud`가 이 값이다. 클라이언트 보안 비밀번호는 쓰지 않는다
   - **Android**: 패키지 이름 `com.teamdoodle.doodledefense`(Player Settings의 Android 패키지 이름과 같아야 한다)와 서명 인증서 SHA-1을 등록한다. 앱에 넣는 값은 없다
3. Android 클라이언트는 **SHA-1 하나당 하나씩** 만든다 (아래 키 세 개면 클라이언트 세 개). 빠진 키로 서명된 앱에서는 로그인이 `DEVELOPER_ERROR`류 오류로 실패한다
   - 디버그 키 (`%USERPROFILE%\.android\debug.keystore`, 에디터에서 바로 빌드할 때)
   - 업로드 키 (릴리스 빌드 키스토어)
   - Play 앱 서명 키 (Play Console → 앱 무결성. Play에서 받은 앱은 이 키로 다시 서명된다)

SHA-1 확인: `keytool -list -v -keystore <키스토어 경로> -alias <별칭>`

## 확인

- 에디터: 구글 버튼이 눌리고 로비로 들어가면 정상이다 (가짜 로그인, `AndroidGoogleSignInTests`)
- Android 기기: 구글 → 확인창 [로그인] → 계정 선택 창 → 로비 진입. 실패하면 `adb logcat -s Unity`에서 `GoogleSignInException`의 코드를 본다
  - `NO_CREDENTIAL`: 기기에 Google 계정이 없거나, SHA-1·패키지 이름이 Android 클라이언트와 맞지 않는다
  - 창이 뜨지 않고 바로 실패: 웹 클라이언트 ID가 다른 프로젝트 것이거나 OAuth 동의 화면 테스트 사용자가 아니다
  - 계정을 골랐는데 로그인창으로 돌아옴(취소처럼 보임): 구글이 앱을 등록되지 않은 앱으로 거절한 것이다. `adb logcat`에 `UNREGISTERED_ON_API_CONSOLE`과 `This android application is not registered to use OAuth2.0`이 보인다. Android 클라이언트의 패키지 이름과 SHA-1이 설치한 APK와 같은지 확인한다 (`apksigner verify --print-certs 앱.apk`로 APK의 SHA-1을 볼 수 있다)
  - 구글은 통과했는데 서버가 401: 서버 환경변수 `GOOGLE_CLIENT_IDS`에 웹 클라이언트 ID가 없다. 서버가 돌려준 message는 앱 로그의 `ApiException`에 괄호로 붙는다
- 브리지의 실패 원인은 `adb logcat -s GoogleSignIn`으로 본다 (취소와 설정 오류가 모두 취소로 올 수 있어 종류와 메시지를 남긴다)
