using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>서버의 유저 상태(ServerUserData)를 클라이언트 PlayerSnapshot으로 바꾼다.
    /// 클라이언트만으로 맞출 수 있는 것(필드 이름, null, 숫자 범위)은 여기서 맞추고, 서버와 합의가 필요한 것은 아래에 적는다.
    /// - 닉네임: 서버 응답에 없어서 호출한 쪽이 넘긴 값을 쓴다
    /// - PR #58의 문자열 아이템 ID는 그대로 옮긴다. 이전 숫자 아이템 ID와 숫자 튜토리얼 ID는 대응표가 없어 옮기지 않는다.</summary>
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


            return snapshot;
        }

        // 서버는 Long ID를 주지만 클라이언트는 int를 쓴다. 범위를 넘으면 잘못된 응답으로 본다
        private static int ToInt(long value) =>
            value >= int.MinValue && value <= int.MaxValue
                ? (int)value
                : throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
    }
}
