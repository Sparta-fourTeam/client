using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>계정 삭제와 소셜 계정 연동을 담당하는 API</summary>
    public interface IAccountApi
    {
        /// <summary>계정과 저장 데이터를 지운다. 되돌릴 수 없다</summary>
        UniTask DeleteAccount();

        /// <summary>지금 계정을 외부 계정에 연결한다. accountId는 ISocialSignIn이 돌려준 계정 ID(토큰)다</summary>
        UniTask LinkAccount(LoginMethod method, string accountId);
    }
}
