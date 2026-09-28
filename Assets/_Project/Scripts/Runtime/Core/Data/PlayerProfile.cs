using System;

namespace Game.Core
{
    /// <summary>현재 플레이어 상태를 들고 있고, 서버 스냅샷이 오면 통째로 교체한다</summary>
    public sealed class PlayerProfile
    {
        private readonly GameDataStore _data;
        private PlayerSnapshot _snapshot = new PlayerSnapshot { stageProgress = new(), upgrades = new() };

        public event Action<PlayerProfile> Changed;

        public PlayerProfile(GameDataStore data)
        {
            _data = data;
        }

        public int Gold => _snapshot.gold;
        public int EnergyStored => _snapshot.energyStored;
        public DateTime EnergyUpdatedAt => DateTime.Parse(_snapshot.energyUpdatedAt);
        public EnergyConfig EnergyConfig => _data.Energy;

        public int UpgradeLevel(string id) => _snapshot.upgrades.Find(u => u.upgradeId == id)?.level ?? 0;
        public bool IsStageCleared(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.IsCleared ?? false;

        /// <summary>안 받은 rating 보상을 전부 골드로 지급하고 받은 금액을 반환한다</summary>
        public int ClaimRatingReward(int stageId)
        {
            var row = _snapshot.stageProgress.Find(s => s.stageId == stageId)
                ?? throw new ApiException { Kind = ApiErrorKind.Rejected, Code = "UNKNOWN_DATA_ID" };

            if (!row.HasUnclaimedReward)
            {
                throw new ApiException { Kind = ApiErrorKind.Rejected, Code = "NO_REWARD_TO_CLAIM" };
            }

            var rewards = _data.Stages.GetOrThrow(stageId).RatingRewards;
            var amount = 0;
            for (var rating = row.claimedRating + 1; rating <= row.clearRating; rating++)
            {
                amount += rewards[rating - 1];
            }

            row.claimedRating = row.clearRating;
            _snapshot.gold += amount;
            Changed?.Invoke(this);
            return amount;
        }

        /// <summary>서버(or Local) 스냅샷으로 상태를 통째로 덮어쓰고 Changed를 방송한다</summary>
        public void Apply(PlayerSnapshot snap)
        {
            _snapshot = snap;
            Changed?.Invoke(this);
        }

        public PlayerSnapshot Snapshot() => _snapshot;
    }
}
