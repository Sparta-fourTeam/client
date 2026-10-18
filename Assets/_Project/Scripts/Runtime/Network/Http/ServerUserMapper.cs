using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>서버의 유저 상태(ServerUserData)를 클라이언트 PlayerSnapshot으로 바꾼다.
    /// 클라이언트만으로 맞출 수 있는 것(필드 이름, null, 숫자 범위)은 여기서 맞추고, 서버와 합의가 필요한 것은 아래에 적는다.
    /// - 닉네임: 서버 응답에 없어서 호출한 쪽이 넘긴 값을 쓴다
    /// - 튜토리얼 문자열 ID·시청/수령 상태와 서버 기간의 퀘스트 진행을 그대로 복원한다.</summary>
    public static class ServerUserMapper
    {
        public static PlayerSnapshot ToSnapshot(ServerUserData data, string nickname, string playerId = null)
        {
            var snapshot = new PlayerSnapshot
            {
                nickname = nickname,
                playerId = playerId,
                gold = data.gold,
                gems = data.gem ?? data.jem,
                exp = data.exp ?? 0,
                energyStored = data.energyStored,
                energyUpdatedAt = data.energyUpdatedAt,
                stageProgress = new List<StageProgressRow>(),
                upgrades = new List<UpgradeRow>(),
                items = new List<ItemAmount>(),
                codex = new List<CodexRow>(),
                tutorials = new List<string>(),
                tutorialStates = new List<TutorialAccountState>(),
                quests = new List<QuestProgressRow>(),
                questChests = new List<string>(),
                questPeriods = new List<QuestAccountPeriod>(),
            };

            foreach (var row in data.stageProgress ?? new List<ServerStageProgress>())
            {
                snapshot.stageProgress.Add(new StageProgressRow
                {
                    stageId = ToInt(row.stageId),
                    clearRating = row.clearRating,
                    claimedRating = row.claimedRating,
                });
            }

            foreach (var row in data.upgrades ?? new List<ServerUpgrade>())
            {
                snapshot.upgrades.Add(new UpgradeRow { upgradeId = row.upgradeId, level = row.level ?? 0 });
            }
            var itemIds = new HashSet<string>();
            foreach (var row in data.items ?? new List<ServerItem>())
            {
                // 이전 서버의 숫자 ID를 문자열 ID로 추측하지 않는다.
                if (row?.itemId?.Type != JTokenType.String) { continue; }
                string itemId = row.itemId.Value<string>();
                if (string.IsNullOrWhiteSpace(itemId) || row.quantity < 0 || !itemIds.Add(itemId))
                {
                    throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
                }
                snapshot.items.Add(new ItemAmount { itemId = itemId, quantity = row.quantity });
            }

            foreach (var row in data.codex ?? new List<ServerCodex>())
            {
                snapshot.codex.Add(new CodexRow { monsterId = ToInt(row.monsterId), claimed = row.claimed });
            }


            return ServerProgressContract.Parse(() => MapProgress(data, snapshot));
        }

        private static PlayerSnapshot MapProgress(ServerUserData data, PlayerSnapshot snapshot)
        {
            var states = JToken.FromObject(data.tutorialStates ?? new List<ServerTutorialState>());
            ServerProgressContract.ValidateTutorials(JToken.FromObject(data.tutorials ?? new List<string>()), states);
            snapshot.tutorials.AddRange(data.tutorials ?? new List<string>());
            foreach (var state in data.tutorialStates ?? new List<ServerTutorialState>())
            {
                snapshot.tutorialStates.Add(new TutorialAccountState
                {
                    tutorialId = state.tutorialId,
                    dialogueSeen = state.dialogueSeen,
                    completed = state.completed,
                    rewardClaimed = state.rewardClaimed,
                    dialogueSeenAt = state.dialogueSeenAt,
                    completedAt = state.completedAt,
                });
            }
            if (data.quests == null) { return snapshot; }
            ServerProgressContract.ValidateBoards(JToken.FromObject(data.quests));
            foreach (var board in data.quests)
            {
                snapshot.questPeriods.Add(new QuestAccountPeriod
                {
                    period = board.period == "DAILY" ? QuestPeriod.Daily : QuestPeriod.Weekly,
                    periodKey = board.periodKey,
                    nextResetAt = board.nextResetAt,
                    lastLoginDay = board.lastLoginDay,
                    loginCount = board.loginCount,
                    spentEnergy = board.spentEnergy,
                    points = board.points,
                });
                snapshot.quests.AddRange(board.quests.Select(row => new QuestProgressRow
                { questId = row.id, count = row.count, claimed = row.claimed }));
                snapshot.questChests.AddRange(board.chests.Where(row => row.claimed).Select(row => row.id));
            }
            return snapshot;
        }

        // 서버는 Long ID를 주지만 클라이언트는 int를 쓴다. 범위를 넘으면 잘못된 응답으로 본다
        private static int ToInt(long value) =>
            value >= int.MinValue && value <= int.MaxValue
                ? (int)value
                : throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
    }
}
