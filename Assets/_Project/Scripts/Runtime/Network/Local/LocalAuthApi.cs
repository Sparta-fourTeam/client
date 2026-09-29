using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>기기 로컬 저장소를 백엔드로 쓰는 Mock 로그인 구현. 저장 파일이 없으면 기본값으로 생성해 저장한다</summary>
    public sealed class LocalAuthApi : IAuthApi
    {
        private readonly LocalSaveStore _store;

        public LocalAuthApi(LocalSaveStore store)
        {
            _store = store;
        }

        public UniTask<LoginResponse> Login()
        {
            var save = _store.Load();
            _store.Flush(save);
            // TODO(server): 토큰/리비전 하드코딩 — 예: 실제 로그인 응답의 accessToken/dataRevision으로 교체
            return UniTask.FromResult(new LoginResponse { accessToken = "local-dev-token", dataRevision = 1 });
        }
    }
}
