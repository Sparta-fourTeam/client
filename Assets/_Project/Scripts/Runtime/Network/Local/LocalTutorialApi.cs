using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>튜토리얼 완료를 로컬 저장소에 반영하는 Mock 구현. 조건과 보상은 Tutorials.json을 따른다.
    /// 시작 조건(앞 튜토리얼·레벨)과 완료 조건을 채워야 완료할 수 있고, 이미 완료한 튜토리얼은 보상을 다시 주지 않는다</summary>
    public sealed class LocalTutorialApi : ITutorialApi
    {
        private readonly LocalSaveStore _store;
        private readonly GameDataStore _data;

        public LocalTutorialApi(LocalSaveStore store, GameDataStore data)
        {
            _store = store;
            _data = data;
        }

        public UniTask<TutorialCompleteResponse> Complete(string tutorialId)
        {
            var save = _store.Load();
            var rewards = new List<ItemAmount>();
            if (save.tutorials.Contains(tutorialId))
            {
                return UniTask.FromResult(Response(save, rewards));
            }

            if (TutorialProgress.IsDialogueId(tutorialId, out string baseId) && _data.Tutorials.Any(t => t.Id == baseId))
            {
                // 도입 대사를 봤다는 기록. 조건과 보상 없이 저장만 한다
                save.tutorials.Add(tutorialId);
                _store.Flush(save);
                return UniTask.FromResult(Response(save, rewards));
            }

            var tutorial = _data.Tutorials.FirstOrDefault(t => t.Id == tutorialId)
                ?? throw new ApiException(ApiErrorKind.Rejected, "UNKNOWN_TUTORIAL");
            if (!TutorialProgress.IsCompletable(tutorial, Facts(save, _data)))
            {
                throw new ApiException(ApiErrorKind.Rejected, "NOT_READY");
            }

            var items = new List<ItemAmount>();
            int gold = 0;
            foreach (var reward in tutorial.Rewards)
            {
                switch (reward.Kind)
                {
                    case TutorialReward.SetLevel:
                        save.exp = ExpToReach(reward.Level);
                        break;
                    case TutorialReward.UpgradeMaterial:
                        var upgrade = _data.Upgrades.GetOrThrow(reward.UpgradeId);
                        items.Add(new ItemAmount { itemId = upgrade.MaterialItemId, quantity = upgrade.MaterialAt(reward.Level) });
                        break;
                    case TutorialReward.UpgradeCost:
                        gold = checked(gold + _data.Upgrades.GetOrThrow(reward.UpgradeId).CostAt(reward.Level));
                        break;
                    case TutorialReward.Item:
                        items.Add(new ItemAmount { itemId = reward.ItemId, quantity = reward.Quantity });
                        break;
                }
            }

            if (items.Count > 0)
            {
                rewards = LocalItemRewards.Apply(save, items, _data);
            }

            save.wallet.gold = checked(save.wallet.gold + gold);
            save.tutorials.Add(tutorialId);
            _store.Flush(save);
            return UniTask.FromResult(Response(save, rewards, gold));
        }

        /// <summary>저장 데이터로 튜토리얼 조건을 판정한다. 전투 발급(튜토리얼 전투 무료)도 쓴다</summary>
        internal static TutorialFacts Facts(LocalSave save, GameDataStore data)
        {
            bool Cleared(int id) => save.stageProgress.Find(s => s.stageId == id)?.IsCleared ?? false;
            return new TutorialFacts
            {
                Level = data.PlayerLevels.At(save.exp).level,
                IsStageCleared = Cleared,
                IsFeatureUnlocked = feature => !data.FeatureUnlocks.IsLocked(feature, Cleared),
                UpgradeLevel = id => save.upgrades.Find(u => u.upgradeId == id)?.level ?? 0,
                IsDone = id => save.tutorials?.Contains(id) ?? true,
            };
        }

        // 1레벨(누적 0)부터 level에 도달하는 데 필요한 누적 경험치
        private int ExpToReach(int level)
        {
            int total = 0;
            for (int i = 1; i < level; i++)
            {
                total += _data.PlayerLevels.RequiredToNext(i);
            }

            return total;
        }

        private static TutorialCompleteResponse Response(LocalSave save, List<ItemAmount> rewards, int gold = 0) =>
            new() { snapshot = LocalPlayerApi.ToSnapshot(save), rewardItems = rewards, rewardGold = gold };
    }
}
