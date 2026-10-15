using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public enum QuestKind
    {
        /// <summary>로그인한 날 수</summary>
        Login,
        /// <summary>전투 입장으로 쓴 에너지</summary>
        SpendEnergy,
        /// <summary>전투에서 처치한 몬스터 수</summary>
        KillMonsters
    }

    public enum QuestPeriod
    {
        Daily,
        Weekly
    }

    /// <summary>퀘스트 한 줄. 목표를 채운 뒤 받으면 Points만큼 보드 게이지가 찬다</summary>
    [Serializable]
    public class QuestDefinition
    {
        public string Id;
        /// <summary>QuestKind 이름</summary>
        public string Kind;
        public int Target;
        public int Points;

        public QuestKind GetKind()
        {
            if (!Enum.TryParse(Kind, out QuestKind kind) || !Enum.IsDefined(typeof(QuestKind), kind))
            {
                throw new InvalidOperationException($"Quests {Id}: 알 수 없는 종류입니다: {Kind}");
            }

            return kind;
        }
    }

    /// <summary>보물상자. 게이지가 Points에 닿으면 열 수 있고, 골드·에너지·잼을 준다</summary>
    [Serializable]
    public class QuestChestDefinition
    {
        public string Id;
        public int Points;
        public int Gold;
        public int Energy;
        /// <summary>잼. 없으면 0이다</summary>
        public int Gems;
    }

    /// <summary>일일·주간 퀘스트 보드. Id가 곧 주기(QuestPeriod 이름)다</summary>
    [Serializable]
    public class QuestBoardDefinition
    {
        public string Id;
        public List<QuestDefinition> Quests = new();
        public List<QuestChestDefinition> Chests = new();

        public QuestPeriod Period => Enum.Parse<QuestPeriod>(Id);

        /// <summary>상자 임계값의 최댓값. 게이지의 끝이다</summary>
        public int MaxPoints => Chests.Count == 0 ? 0 : Chests.Max(chest => chest.Points);

        public void Validate(ISet<string> usedIds)
        {
            if (!Enum.TryParse(Id, out QuestPeriod period) || !Enum.IsDefined(typeof(QuestPeriod), period))
            {
                throw new InvalidOperationException($"Quests: 알 수 없는 주기입니다: {Id}");
            }

            foreach (var quest in Quests)
            {
                quest.GetKind();
                if (string.IsNullOrWhiteSpace(quest.Id) || !usedIds.Add(quest.Id))
                {
                    throw new InvalidOperationException($"Quests {Id}: 퀘스트 Id가 비었거나 중복됩니다: {quest.Id}");
                }

                if (quest.Target <= 0 || quest.Points <= 0)
                {
                    throw new InvalidOperationException($"Quests {quest.Id}: Target과 Points는 1 이상이어야 합니다");
                }
            }

            int previous = 0;
            foreach (var chest in Chests)
            {
                if (string.IsNullOrWhiteSpace(chest.Id) || !usedIds.Add(chest.Id))
                {
                    throw new InvalidOperationException($"Quests {Id}: 상자 Id가 비었거나 중복됩니다: {chest.Id}");
                }

                if (chest.Points <= previous || chest.Gold < 0 || chest.Energy < 0 || chest.Gems < 0 || chest.Gold + chest.Energy + chest.Gems <= 0)
                {
                    throw new InvalidOperationException($"Quests {chest.Id}: Points는 앞 상자보다 커야 하고 보상은 1 이상이어야 합니다");
                }

                previous = chest.Points;
            }

            if (Quests.Sum(quest => quest.Points) < MaxPoints)
            {
                throw new InvalidOperationException($"Quests {Id}: 퀘스트 점수의 합이 마지막 상자에 못 미칩니다");
            }
        }
    }
}
