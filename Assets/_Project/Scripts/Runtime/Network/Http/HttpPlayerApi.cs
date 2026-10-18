using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>서버의 GET /api/user/me로 플레이어 상태를 받는다 (서버 PR #47).
    /// 서버에는 닉네임이 없어서, 아직 로컬 저장이 데이터를 맡는 동안은 로컬에 저장된 닉네임을 쓴다.
    /// #78/#79 문자열 튜토리얼과 서버 퀘스트 보드를 복원한다. 부분 응답은 적용하지 않는다.</summary>
    public sealed class HttpPlayerApi : IPlayerApi
    {
        private const string MePath = "api/user/me";

        private readonly HttpApiClient _client;
        private readonly LocalPlayerApi _local;

        public HttpPlayerApi(HttpApiClient client, LocalPlayerApi local)
        {
            _client = client;
            _local = local;
        }

        public async UniTask<PlayerSnapshot> GetMe()
        {
            var json = await _client.GetJson(MePath);
            var data = ServerProgressContract.Parse(() => ServerProgressContract.ReadMe(json));
            var local = await _local.GetMe();
            var snapshot = ServerUserMapper.ToSnapshot(data, local.nickname, local.playerId);

            return snapshot;
        }
    }
}
