using System;
using System.Linq;
using Game.Core;
using Game.Network;

namespace Game.Editor
{
    /// <summary>Playtest 창의 세이브 치트. LocalSave를 고치는 순수 규칙만 둔다 (Local 백엔드 전용)</summary>
    internal static class PlaytestCheats
    {
        internal static void AddGold(LocalSave save, int amount) =>
            save.wallet.gold = (int)Math.Clamp((long)save.wallet.gold + amount, 0, int.MaxValue);

        internal static void AddGems(LocalSave save, int amount) =>
            save.wallet.gems = (int)Math.Clamp((long)save.wallet.gems + amount, 0, int.MaxValue);

        /// <summary>게임의 초기화 버튼과 같게 일일상점을 다시 뽑는다. 새 구성이 나오고 산 칸 기록이 비워지며, 코인은 받지 않는다.
        /// 남은 횟수(광고, 그다음 코인)를 하나 쓰고, 횟수가 다 떨어졌으면 횟수를 비우고 처음부터 다시 센다</summary>
        internal static void ResetDailyShop(LocalSave save, GameDataStore data)
        {
            var state = save.shop.state;
            if (ShopRules.AdLeft(data.Shop, state) > 0) { state.adUsed++; }
            else if (ShopRules.GoldLeft(data.Shop, state) > 0) { state.goldUsed++; }
            else
            {
                state.adUsed = 1;
                state.goldUsed = 0;
            }

            state.purchased.Clear();
        }

        internal static void SetAllResources(LocalSave save, GameDataStore data)
        {
            save.wallet.gold = 999;
            save.wallet.gems = 999;
            save.wallet.energyStored = 999;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            save.items ??= new();
            foreach (var definition in data.Items.Values)
            {
                var item = save.items.Find(row => row.itemId == definition.Id);
                if (item == null)
                {
                    save.items.Add(new ItemAmount { itemId = definition.Id, quantity = 999 });
                }
                else { item.quantity = 999; }
            }
        }

        internal static void FillEnergy(LocalSave save, EnergyConfig energy)
        {
            save.wallet.energyStored = energy.Max;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
        }

        /// <summary>level에 막 도달한 exp. 레벨업 직전이면 다음 레벨까지 1 모자란 exp</summary>
        internal static int ExpFor(PlayerLevelTable table, int level, bool justBeforeNext)
        {
            level = Math.Clamp(level, 1, table.MaxLevel);
            long exp = 0;
            for (int i = 1; i < level; i++) { exp += table.RequiredToNext(i); }
            if (justBeforeNext && level < table.MaxLevel) { exp += table.RequiredToNext(level) - 1; }
            return (int)Math.Min(exp, int.MaxValue);
        }

        internal static void SetLevel(LocalSave save, PlayerLevelTable table, int level, bool justBeforeNext) =>
            save.exp = ExpFor(table, level, justBeforeNext);

        internal static void CompleteTutorials(LocalSave save, GameDataStore data) =>
            save.tutorials = data.Tutorials.Select(t => t.Id)
                .Concat(data.Tutorials.Select(t => TutorialProgress.DialogueId(t.Id))).ToList();

        internal static void ResetTutorials(LocalSave save) => save.tutorials = new();

        /// <summary>진행 기록에 줄이 없는 스테이지만 해금한다. 이미 있는 클리어 기록은 건드리지 않는다</summary>
        internal static void UnlockAllStages(LocalSave save, GameDataStore data)
        {
            foreach (var stage in data.Stages.Values)
            {
                if (!save.stageProgress.Exists(s => s.stageId == stage.Id))
                {
                    save.stageProgress.Add(new StageProgressRow { stageId = stage.Id });
                }
            }
        }

        /// <summary>모든 스테이지를 클리어 처리한다 (로비 기능 해금은 스테이지 클리어 기록 기준). 이미 더 높은 rating이면 유지한다</summary>
        internal static void ClearAllStages(LocalSave save, GameDataStore data)
        {
            foreach (var stage in data.Stages.Values)
            {
                var row = save.stageProgress.Find(s => s.stageId == stage.Id);
                if (row == null)
                {
                    row = new StageProgressRow { stageId = stage.Id };
                    save.stageProgress.Add(row);
                }

                row.clearRating = Math.Max(row.clearRating, 1);
            }
        }

        /// <summary>레벨을 정하고 그 레벨에서 열려 있어야 할 컨텐츠를 모두 연다. 튜토리얼을 먼저 완료해 두어,
        /// 스테이지 클리어로 시작 전투 튜토리얼이 완료될 때 받는 레벨 보상(setLevel)이 설정한 레벨을 덮어쓰지 않게 한다</summary>
        internal static void SetLevelAndUnlockAll(LocalSave save, GameDataStore data, int level)
        {
            CompleteTutorials(save, data);
            ClearAllStages(save, data);
            SetLevel(save, data.PlayerLevels, level, false);
        }
    }
}
