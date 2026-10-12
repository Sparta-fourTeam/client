using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>외부 계정 로그인 한 종류(구글, 애플 등). 플랫폼 SDK가 붙기 전에는 IsAvailable이 false다</summary>
    public interface ISocialSignIn
    {
        LoginMethod Method { get; }

        /// <summary>이 기기·빌드에서 쓸 수 있는지. false면 로그인창에서 "준비 중"으로 막는다</summary>
        bool IsAvailable { get; }

        /// <summary>외부 계정으로 로그인하고 서버에 넘길 계정 ID(토큰)를 돌려준다. 사용자가 취소하면 null</summary>
        UniTask<string> SignInAsync();
    }
}
