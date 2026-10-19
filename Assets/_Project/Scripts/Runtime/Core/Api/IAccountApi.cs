using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>계정 삭제·연동과 표시 이름 변경을 담당하는 API</summary>
    public interface IAccountApi
    {
        /// <summary>계정과 저장 데이터를 지운다. 되돌릴 수 없다</summary>
        UniTask DeleteAccount();

        /// <summary>표시 이름을 저장하고 확정된 이름을 돌려준다. 서버는 중복 이름에 NICKNAME_DUPLICATED를 반환한다</summary>
        UniTask<string> ChangeNickname(string nickname);

        /// <summary>지금 계정을 외부 계정에 연결한다. accountId는 ISocialSignIn이 돌려준 계정 ID(토큰)다</summary>
        UniTask LinkAccount(LoginMethod method, string accountId);
    }
}
