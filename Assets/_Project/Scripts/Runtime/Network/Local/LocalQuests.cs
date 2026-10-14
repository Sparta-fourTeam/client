using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Network
{
    /// <summary>LocalSave에 저장되는 퀘스트 상태. 보드마다 지금 주기의 키를 들고 있어, 키가 바뀌면 그 보드의 진행을 비운다</summary>
    [Serializable]
    public class QuestSave
    {
        public string dailyKey;
        public string weeklyKey;
        /// <summary>마지막으로 로그인을 센 날의 일일 키. 같은 날 여러 번 로그인해도 주간 로그인 수가 한 번만 오른다</summary>
        public string lastLoginDay;
        public List<QuestProgressRow> rows = new();
        public List<string> chests = new();
    }

    /// <summary>퀘스트 진행과 수령의 Local 구현. 서버 전환 후에는 서버가 이 역할을 대신한다. 저장은 호출한 API가 한 번에 한다</summary>
    internal static class LocalQuests
    {
        /// <summary>주기가 바뀐 보드의 진행·상자 기록을 비운다. 저장 파일을 읽을 때마다 부른다</summary>
        public static void Roll(LocalSave save, GameDataStore data, DateTime localNow)
        {
            save.quests ??= new QuestSave();
            var quests = save.quests;
            quests.rows ??= new();
            quests.chests ??= new();
            foreach (var board in data.QuestBoards)
            {
                string key = QuestRules.PeriodKey(board.Period, localNow);
                string saved = board.Period == QuestPeriod.Daily ? quests.dailyKey : quests.weeklyKey;
                if (saved == key) { continue; }

                quests.rows.RemoveAll(row => board.Quests.Exists(q => q.Id == row.questId));
                quests.chests.RemoveAll(id => board.Chests.Exists(c => c.Id == id));
                if (board.Period == QuestPeriod.Daily) { quests.dailyKey = key; } else { quests.weeklyKey = key; }
            }
        }

        /// <summary>오늘 첫 로그인이면 로그인 퀘스트를 한 번 센다</summary>
        public static void MarkLogin(LocalSave save, GameDataStore data, DateTime localNow)
        {
            Roll(save, data, localNow);
            string today = save.quests.dailyKey;
            if (save.quests.lastLoginDay == today) { return; }

            save.quests.lastLoginDay = today;
            Add(save, data, QuestKind.Login, 1);
        }

        /// <summary>해당 종류의 모든 퀘스트 진행을 올린다. 목표를 넘기지 않는다</summary>
        public static void Add(LocalSave save, GameDataStore data, QuestKind kind, int amount)
        {
            if (amount <= 0) { return; }

            foreach (var quest in data.QuestBoards.SelectMany(board => board.Quests).Where(q => q.GetKind() == kind))
            {
                var row = save.quests.rows.Find(r => r.questId == quest.Id);
                if (row == null)
                {
                    row = new QuestProgressRow { questId = quest.Id };
                    save.quests.rows.Add(row);
                }

                if (!row.claimed)
                {
                    row.count = (int)Math.Min((long)row.count + amount, quest.Target);
                }
            }
        }

        public static void ClaimQuest(LocalSave save, GameDataStore data, string questId)
        {
            var quest = data.QuestBoards.SelectMany(board => board.Quests).FirstOrDefault(q => q.Id == questId)
                ?? throw new ApiException(ApiErrorKind.Rejected, "UNKNOWN_DATA_ID");
            var row = save.quests.rows.Find(r => r.questId == questId);
            if (row != null && row.claimed) { throw new ApiException(ApiErrorKind.Rejected, "ALREADY_CLAIMED"); }
            if (row == null || row.count < quest.Target) { throw new ApiException(ApiErrorKind.Rejected, "NOT_COMPLETE"); }

            row.claimed = true;
        }

        public static void ClaimChest(LocalSave save, GameDataStore data, string chestId, DateTime utcNow)
        {
            var board = data.QuestBoards.FirstOrDefault(b => b.Chests.Exists(c => c.Id == chestId))
                ?? throw new ApiException(ApiErrorKind.Rejected, "UNKNOWN_DATA_ID");
            var chest = board.Chests.Find(c => c.Id == chestId);
            if (save.quests.chests.Contains(chestId)) { throw new ApiException(ApiErrorKind.Rejected, "ALREADY_CLAIMED"); }
            if (QuestRules.Points(board, save.quests.rows) < chest.Points) { throw new ApiException(ApiErrorKind.Rejected, "NOT_REACHED"); }

            var (current, _) = EnergyRule.At(save.wallet.energyStored, EnergyRule.ParseUpdatedAt(save.wallet.energyUpdatedAt), utcNow, data.Energy);
            int gold, energy;
            try { gold = checked(save.wallet.gold + chest.Gold); energy = checked(current + chest.Energy); }
            catch (OverflowException) { throw new ApiException(ApiErrorKind.Rejected, "REWARD_QUANTITY_OVERFLOW"); }

            save.quests.chests.Add(chestId);
            save.wallet.gold = gold;
            save.wallet.energyStored = energy;
            save.wallet.energyUpdatedAt = utcNow.ToString("O");
        }
    }
}
