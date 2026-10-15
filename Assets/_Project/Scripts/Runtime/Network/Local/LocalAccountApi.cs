using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>기기 로컬 저장소를 백엔드로 쓰는 Mock 계정 API.
    /// 연동은 서버 인증을 쓰는 빌드에서 ServerBackedAccountApi가 서버로 보낸다. 삭제 API는 서버에 아직 없다. 로컬에서는 연동 정보를 따로 저장하지 않는다</summary>
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

        public UniTask LinkAccount(LoginMethod method, string accountId) => UniTask.CompletedTask;
    }
}
