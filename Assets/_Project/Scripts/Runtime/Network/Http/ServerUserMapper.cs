using System.Collections.Generic;
using Game.Core;

namespace Game.Network
{
    /// <summary>서버의 유저 상태(ServerUserData)를 클라이언트 PlayerSnapshot으로 바꾼다.
    /// 클라이언트만으로 맞출 수 있는 것(필드 이름, null, 숫자 범위)은 여기서 맞추고, 서버와 합의가 필요한 것은 아래에 적는다.
    /// - 닉네임: 서버 응답에 없어서 호출한 쪽이 넘긴 값을 쓴다
    /// - **아이템·튜토리얼은 옮기지 않는다**: 서버는 숫자 ID, 클라이언트는 문자열 ID(예: book.arrow, intro_battle)라서 서버 ID와
    ///   클라이언트 ID의 대응이 정해져야 한다. 틀린 ID로 옮기면 소유량·완료 여부를 잘못 읽으므로, 대응이 정해질 때까지 빈 목록으로 둔다</summary>
    public static class ServerUserMapper
    {
        public static PlayerSnapshot ToSnapshot(ServerUserData data, string nickname, string playerId = null)
        {
            var snapshot = new PlayerSnapshot
            {
                nickname = nickname,
                playerId = playerId,
                gold = data.gold,
                gems = data.jem,
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
