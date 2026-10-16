using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>서버의 GET /api/user/me로 플레이어 상태를 받는다 (서버 PR #47).
    /// 서버에는 닉네임이 없어서, 아직 로컬 저장이 데이터를 맡는 동안은 로컬에 저장된 닉네임을 쓴다.
    /// PR #58의 문자열 아이템 ID는 그대로 옮기고, 이전 숫자 아이템·튜토리얼 ID는 대응이 없어 옮기지 않는다.</summary>
    public sealed class HttpPlayerApi : IPlayerApi
    {
        private const string MePath = "api/user/me";

        private readonly HttpApiClient _client;
        private readonly LocalPlayerApi _local;
        private bool _warnedIds;

        public HttpPlayerApi(HttpApiClient client, LocalPlayerApi local)
        {
            _client = client;
            _local = local;
        }

        public async UniTask<PlayerSnapshot> GetMe()
        {
            var data = await _client.Get<ServerUserData>(MePath);
            var local = await _local.GetMe();
            var snapshot = ServerUserMapper.ToSnapshot(data, local.nickname, local.playerId);

            int unapplied = (data.items?.Count ?? 0) - snapshot.items.Count + (data.tutorials?.Count ?? 0);
            if (!_warnedIds && unapplied > 0)
            {
                _warnedIds = true;
                Debug.LogWarning($"[Player] 서버가 준 아이템·튜토리얼 {unapplied}건은 ID 대응이 정해지지 않아 반영하지 않는다");
            }

            return snapshot;
        }
    }
}
