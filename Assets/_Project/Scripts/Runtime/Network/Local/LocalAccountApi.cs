using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>기기 로컬 저장소를 백엔드로 쓰는 Mock 계정 API.
    /// 연동은 서버 인증을 쓰는 빌드에서 ServerBackedAccountApi가 서버로 보낸다. 닉네임은 아직 로컬 저장만 지원한다. 로컬에서는 연동 정보를 따로 저장하지 않는다</summary>
    public sealed class LocalAccountApi : IAccountApi
    {
        private readonly LocalSaveStore _store;

        public LocalAccountApi(LocalSaveStore store)
        {
            _store = store;
        }

        public UniTask DeleteAccount()
        {
            _store.Delete();
            return UniTask.CompletedTask;
        }

        public UniTask<string> ChangeNickname(string nickname)
        {
            if (!NicknameRules.IsValid(nickname)) { throw new ApiException(ApiErrorKind.Rejected, "INVALID_NICKNAME"); }
            var save = _store.Load();
            save.nickname = nickname;
            _store.Flush(save);
            return UniTask.FromResult(nickname);
        }

        public UniTask LinkAccount(LoginMethod method, string accountId) => UniTask.CompletedTask;
    }
}
