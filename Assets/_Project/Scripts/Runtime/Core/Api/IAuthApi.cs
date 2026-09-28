using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>계정 로그인을 담당하는 API</summary>
    public interface IAuthApi
    {
        /// <summary>로그인하고 액세스 토큰과 현재 테이블 리비전을 받는다</summary>
        UniTask<LoginResponse> Login();
    }
}
