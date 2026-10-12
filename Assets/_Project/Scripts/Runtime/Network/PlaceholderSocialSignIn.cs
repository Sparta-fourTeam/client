using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>아직 SDK가 없는 외부 계정 로그인의 자리. 항상 사용할 수 없다고 알려 로그인창에서 "준비 중"으로 막힌다.
    /// VContainer는 같은 구현 타입을 한 인터페이스에 두 번 등록할 수 없으므로 방식마다 클래스를 둔다.
    /// TODO(sdk): 구글은 Google Play Games(또는 Google Sign-In), 애플은 Sign in with Apple 구현으로 RootLifetimeScope 등록을 바꾼다.
    /// 서버가 외부 계정 토큰을 검증해야 하므로 server#1의 google_id/apple_id 계약과 함께 진행한다</summary>
    public abstract class PlaceholderSocialSignIn : ISocialSignIn
    {
        protected PlaceholderSocialSignIn(LoginMethod method)
        {
            Method = method;
        }

        public LoginMethod Method { get; }

        public bool IsAvailable => false;

        public UniTask<string> SignInAsync() =>
            throw new NotSupportedException($"{Method} 로그인은 아직 준비 중입니다");
    }

    public sealed class PlaceholderGoogleSignIn : PlaceholderSocialSignIn
    {
        public PlaceholderGoogleSignIn() : base(LoginMethod.Google) { }
    }

    public sealed class PlaceholderAppleSignIn : PlaceholderSocialSignIn
    {
        public PlaceholderAppleSignIn() : base(LoginMethod.Apple) { }
    }
}
