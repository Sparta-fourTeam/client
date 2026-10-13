using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>기기 로컬 저장소를 백엔드로 쓰는 Mock 계정 API.
    /// TODO(server): 연동·삭제 API 계약(server#1)이 정해지면 Http 구현으로 바꾼다. 로컬에서는 연동 정보를 따로 저장하지 않는다</summary>
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
