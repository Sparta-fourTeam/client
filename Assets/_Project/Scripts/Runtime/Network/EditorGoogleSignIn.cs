#if UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>에디터에서만 쓰는 가짜 구글 로그인. Android 기기가 없어도 구글 버튼 → 확인창 → 로비 흐름을 확인할 수 있게
    /// 고정 계정 ID로 바로 성공한다. 진짜 ID 토큰이 아니라서 서버(/auth/social/login)에는 쓸 수 없다.
    /// UNITY_EDITOR로만 컴파일되어 빌드에는 들어가지 않는다</summary>
    public sealed class EditorGoogleSignIn : ISocialSignIn
    {
        /// <summary>가짜 계정 ID. 진짜 토큰과 헷갈리지 않게 접두사를 붙였다</summary>
        public const string FakeAccountId = "editor-fake-google-account";

        public LoginMethod Method => LoginMethod.Google;

        public bool IsAvailable => true;

        public async UniTask<string> SignInAsync()
        {
            // 계정 선택 창이 뜨는 시간처럼 잠깐 기다린다
            await UniTask.Delay(TimeSpan.FromMilliseconds(300), ignoreTimeScale: true);
            return FakeAccountId;
        }
    }
}
#endif
