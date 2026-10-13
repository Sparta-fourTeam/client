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
            save.tutorials = data.Tutorials.Select(t => t.Id).ToList();

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
    }
}
