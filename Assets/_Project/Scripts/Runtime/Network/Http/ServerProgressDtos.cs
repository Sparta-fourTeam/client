using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Game.Core;

namespace Game.Network
{
    /// <summary>서버 #78의 튜토리얼 복원 상태. 시청 기록은 완료·수령과 구분한다.</summary>
    [Serializable]
    public sealed class ServerTutorialState
    {
        public string tutorialId;
        public bool dialogueSeen;
        public bool completed;
        public bool rewardClaimed;
        public string dialogueSeenAt;
        public string completedAt;
    }

    /// <summary>서버 #79의 기간별 퀘스트 보드. LOGIN·SPEND_ENERGY만 지원한다.</summary>
    [Serializable]
    public sealed class ServerQuestBoard
    {
        public string period;
        public string periodKey;
        public string nextResetAt;
        public string lastLoginDay;
        public int loginCount;
        public int spentEnergy;
        public long points;
        public List<ServerQuestRow> quests;
        public List<ServerQuestChest> chests;
    }

    [Serializable]
    public sealed class ServerQuestRow
    {
        public string id;
        public string kind;
        public int count;
        public int target;
        public int points;
        public bool claimed;
        public string claimedAt;
    }

    [Serializable]
    public sealed class ServerQuestChest
    {
        public string id;
        public int points;
        public bool claimed;
        public string claimedAt;
    }

    /// <summary>최초 요청을 그대로 재사용한다. 계정은 Bearer 토큰으로만 식별한다.</summary>
    [Serializable]
    public sealed class TutorialCompletionRequest
    {
        public readonly string tutorialId;

        public TutorialCompletionRequest(string tutorialId)
        {
            if (tutorialId == null || !Regex.IsMatch(tutorialId, @"\A[A-Za-z0-9_-]{1,64}(\.dialogue)?\z"))
            { throw new ArgumentException("서버 튜토리얼 ID가 필요합니다", nameof(tutorialId)); }
            this.tutorialId = tutorialId;
        }
    }

    /// <summary>조회한 서버 보드의 기간을 고정한다. 재시도에서 현재 날짜로 기간키를 바꾸지 않는다.</summary>
    [Serializable]
    public sealed class QuestClaimRequest
    {
        public readonly string id;
        public readonly string period;
        public readonly string periodKey;

        public QuestClaimRequest(string id, QuestPeriod period, string periodKey)
        {
            if (id == null || !Regex.IsMatch(id, @"\A[A-Za-z0-9_.-]{1,64}\z"))
            { throw new ArgumentException("서버 퀘스트 ID가 필요합니다", nameof(id)); }
            if (period != QuestPeriod.Daily && period != QuestPeriod.Weekly)
            { throw new ArgumentOutOfRangeException(nameof(period)); }
            if (!ServerProgressContract.Day(periodKey))
            { throw new ArgumentException("조회한 서버 기간키가 필요합니다", nameof(periodKey)); }
            this.id = id;
            this.period = period == QuestPeriod.Daily ? "DAILY" : "WEEKLY";
            this.periodKey = periodKey;
        }
    }

    /// <summary>최초 튜토리얼 영수증. ExpAfter와 Gold는 현재 잔액이 아니다.</summary>
    public sealed class TutorialCompletionReceipt
    {
        public string TutorialId { get; }
        public bool DialogueOnly { get; }
        public int Gold { get; }
        public IReadOnlyDictionary<string, int> Items { get; }
        public int ExpBefore { get; }
        public int ExpAfter { get; }
        public string RecordedAt { get; }

        internal TutorialCompletionReceipt(string id, bool dialogue, int gold, Dictionary<string, int> items,
            int expBefore, int expAfter, string recordedAt)
        {
            TutorialId = id; DialogueOnly = dialogue; Gold = gold;
            Items = new ReadOnlyDictionary<string, int>(items);
            ExpBefore = expBefore; ExpAfter = expAfter; RecordedAt = recordedAt;
        }
    }

    /// <summary>최초 퀘스트 점수·상자 보상 영수증. 현재 보드나 잔액과 별도로 보존한다.</summary>
    public sealed class QuestClaimReceipt
    {
        public string Id { get; }
        public string Period { get; }
        public string PeriodKey { get; }
        public bool Chest { get; }
        public int Points { get; }
        public int Gold { get; }
        public int Energy { get; }
        public int Gems { get; }
        public IReadOnlyDictionary<string, int> Items { get; }
        public string ClaimedAt { get; }

        internal QuestClaimReceipt(QuestClaimRequest request, bool chest, int points, int gold, int energy,
            int gems, Dictionary<string, int> items, string claimedAt)
        {
            Id = request.id; Period = request.period; PeriodKey = request.periodKey; Chest = chest;
            Points = points; Gold = gold; Energy = energy; Gems = gems;
            Items = new ReadOnlyDictionary<string, int>(items); ClaimedAt = claimedAt;
        }
    }
}
