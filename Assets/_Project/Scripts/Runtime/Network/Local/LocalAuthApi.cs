using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>기기 로컬 저장소를 백엔드로 쓰는 Mock 로그인 구현. 저장 파일이 없으면 기본값으로 생성해 저장한다</summary>
    public sealed class LocalAuthApi : IAuthApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalAuthApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        // 로컬에는 계정 구분이 없어 방식과 계정 ID는 쓰지 않는다 (기기의 저장 파일 하나가 계정이다)
        public UniTask<LoginResponse> Login(LoginMethod method, string accountId)
        {
            bool isNewAccount = !_store.Exists();
            var save = _store.Load();

            if (isNewAccount)
            {
                save.wallet.energyStored = _data.Energy.Start;
                save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            }

            LocalQuests.MarkLogin(save, _data, DateTime.UtcNow);
            _store.Flush(save);
            // TODO(server): 토큰 하드코딩 — 실제 로그인 응답의 accessToken으로 교체
            return UniTask.FromResult(new LoginResponse { accessToken = "local-dev-token" });
        }

        // 기기 진행은 유지하되 이전 서버 계정의 공개 신원은 다음 로그인에 재사용하지 않는다.
        internal void ClearCachedServerIdentity()
        {
            var save = _store.Load();
            save.nickname = null;
            save.playerId = null;
            save.avatarId = 0;
            _store.Flush(save);
        }
    }
}
