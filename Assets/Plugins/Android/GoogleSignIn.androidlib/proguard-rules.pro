# C#(AndroidJavaClass, AndroidJavaProxy)이 이름으로 찾으므로 코드 축소를 켜도 남긴다
-keep class com.teamdoodle.auth.GoogleSignInBridge { *; }
-keep interface com.teamdoodle.auth.GoogleSignInBridge$Callback { *; }

# Credential Manager가 Play 서비스 구현을 리플렉션으로 찾는다 (androidx.credentials 안내)
-if class androidx.credentials.CredentialManager
-keep class androidx.credentials.playservices.** { *; }
