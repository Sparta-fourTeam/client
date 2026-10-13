using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>계정 로그인을 담당하는 API</summary>
    public interface IAuthApi
    {
        /// <summary>로그인하고 액세스 토큰을 받는다.
        /// 게스트는 accountId가 저장된 게스트 ID이고 없으면(null) 새 게스트로 가입한다. 구글·애플은 accountId가 외부 계정의 ID 토큰이다.
        /// 응답의 accountId는 다음 로그인에 쓸 계정 ID이고, 서버가 정하지 않으면 null이다</summary>
        UniTask<LoginResponse> Login(LoginMethod method, string accountId);
    }
}
