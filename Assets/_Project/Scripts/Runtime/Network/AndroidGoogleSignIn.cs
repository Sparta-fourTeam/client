using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>Android에서 Credential Manager로 Google 계정 로그인을 하고 Google ID 토큰을 돌려준다.
    /// 네이티브 쪽은 Assets/Plugins/Android/GoogleSignIn.androidlib의 GoogleSignInBridge다.
    /// Android 기기가 아니거나 웹 클라이언트 ID가 비어 있으면 쓸 수 없다(로그인창에서 "준비 중")</summary>
    public sealed class AndroidGoogleSignIn : ISocialSignIn
    {
        // Google Cloud 콘솔의 "웹 애플리케이션" 유형 OAuth 클라이언트 ID. ID 토큰의 aud가 되고 서버(GOOGLE_CLIENT_IDS)가 같은 값으로 검증한다.
        // Android 유형 클라이언트 ID가 아니다. Android 클라이언트(패키지명 + 서명 SHA-1)는 같은 프로젝트에 따로 등록만 하면 된다.
        // 클라이언트 ID는 비밀값이 아니라 코드에 둔다
        // TODO(google): 웹 클라이언트 ID를 받으면 채운다
        private const string WebClientId = "";

        private const string BridgeClass = "com.teamdoodle.auth.GoogleSignInBridge";

        public LoginMethod Method => LoginMethod.Google;

        public bool IsAvailable => Application.platform == RuntimePlatform.Android && !string.IsNullOrEmpty(WebClientId);

        public async UniTask<string> SignInAsync()
        {
            if (!IsAvailable)
            {
                throw new NotSupportedException("Google 로그인은 Android 빌드에서 웹 클라이언트 ID가 있을 때만 쓸 수 있습니다");
            }

            var result = new UniTaskCompletionSource<string>();
            var callback = new Callback(result);
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass(BridgeClass))
            {
                bridge.CallStatic("signIn", activity, WebClientId, callback);
            }

            try
            {
                return await result.Task;
            }
            finally
            {
                // 결과는 Android 메인 스레드에서 오므로 Unity 메인 스레드로 돌아온 뒤 이어간다
                await UniTask.SwitchToMainThread();
                GC.KeepAlive(callback);
            }
        }

        /// <summary>GoogleSignInBridge.Callback의 C# 구현. 메서드 이름은 Java 인터페이스와 같아야 한다</summary>
        private sealed class Callback : AndroidJavaProxy
        {
            private readonly UniTaskCompletionSource<string> _result;

            public Callback(UniTaskCompletionSource<string> result) : base(BridgeClass + "$Callback")
            {
                _result = result;
            }

            public void onSuccess(string idToken) => _result.TrySetResult(idToken);

            public void onCancel() => _result.TrySetResult(null);

            public void onError(string code, string message) =>
                _result.TrySetException(new GoogleSignInException(code, message));
        }
    }

    /// <summary>Google 로그인 실패. Code는 GoogleSignInBridge가 정한 값(NO_CREDENTIAL 등)이나 Credential Manager 예외 타입이다</summary>
    public sealed class GoogleSignInException : Exception
    {
        public string Code { get; }

        public GoogleSignInException(string code, string message) : base($"Google 로그인 실패 ({code}): {message}")
        {
            Code = code;
        }
    }
}
