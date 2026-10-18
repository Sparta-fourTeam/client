using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>퀘스트 주기와 보드 점수 계산. 서버와 Local이 같은 규칙을 쓰고, 화면은 점수·수령 가능 여부를 이것으로 읽는다</summary>
    public static class QuestRules
    {
        /// <summary>한국시간 기준 일일 초기화 시각.</summary>
        public const int ResetHour = GameDay.ResetHour;

        /// <summary>지금이 속한 주기의 키. 키가 바뀌면 그 주기의 진행이 초기화된다. 주간은 월요일 시작</summary>
        public static string PeriodKey(QuestPeriod period, DateTime utcNow)
        {
            var shifted = GameDay.Date(utcNow);
            if (period == QuestPeriod.Weekly)
            {
                shifted = shifted.AddDays(-(((int)shifted.DayOfWeek + 6) % 7));
            }

            return shifted.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        /// <summary>게이지가 닿았고 아직 열지 않은 상자인가</summary>
        public static bool IsChestOpenable(PlayerProfile profile, QuestBoardDefinition board, QuestChestDefinition chest) =>
            !profile.IsQuestChestClaimed(chest.Id) && profile.QuestPoints(board) >= chest.Points;

        /// <summary>받을 수 있는 것(목표를 채운 퀘스트, 열 수 있는 상자)이 하나라도 있는가</summary>
        public static bool HasClaimable(PlayerProfile profile, IEnumerable<QuestBoardDefinition> boards)
        {
            foreach (var board in boards)
            {
                foreach (var quest in board.Quests)
                {
                    if (!profile.IsQuestClaimed(quest.Id) && profile.QuestCount(quest.Id) >= quest.Target) { return true; }
                }

                foreach (var chest in board.Chests)
                {
                    if (IsChestOpenable(profile, board, chest)) { return true; }
                }
            }

            return false;
        }

        /// <summary>받은 퀘스트의 점수 합</summary>
        public static int Points(QuestBoardDefinition board, IEnumerable<QuestProgressRow> rows)
        {
            int total = 0;
            foreach (var row in rows)
            {
                var quest = board.Quests.Find(q => q.Id == row.questId);
                if (quest != null && row.claimed)
                {
                    total += quest.Points;
                }
            }

            return total;
        }
    }
}
