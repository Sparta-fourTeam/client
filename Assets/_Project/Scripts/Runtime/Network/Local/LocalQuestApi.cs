using System;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>퀘스트·보물상자 수령을 로컬 저장소에 반영하는 Mock 구현</summary>
    public sealed class LocalQuestApi : IQuestApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalQuestApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<PlayerSnapshot> ClaimQuest(string questId)
        {
            var save = _store.Load();
            LocalQuests.ClaimQuest(save, _data, questId);
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }

        public UniTask<PlayerSnapshot> ClaimQuestChest(string chestId)
        {
            var save = _store.Load();
            LocalQuests.ClaimChest(save, _data, chestId, DateTime.UtcNow);
            _store.Flush(save);
            return UniTask.FromResult(LocalPlayerApi.ToSnapshot(save));
        }
    }
}
