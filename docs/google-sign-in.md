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
- 받은 ID 토큰은 서버 `POST /auth/social/login`에 `{ "provider": "GOOGLE", "idToken": "..." }`로 보낸다 (서버 연결은 #180에서 붙인다. 지금 로그인은 Local 구현이다)

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
