using System;
using System.Globalization;

namespace Game.Core
{
    /// <summary>현재 플레이어 상태를 들고 있는 순수 스냅샷 캐시. 서버 스냅샷이 오면 통째로 교체한다</summary>
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

        /// <summary>energyUpdatedAt을 UTC로 해석한다. Apply 전(값 없음)에는 DateTime.UtcNow를 반환한다</summary>
        public DateTime EnergyUpdatedAt =>
            string.IsNullOrEmpty(_snapshot.energyUpdatedAt)
                ? DateTime.UtcNow
                : DateTime.Parse(_snapshot.energyUpdatedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        public EnergyConfig EnergyConfig => _data.Energy;

        public int UpgradeLevel(string id) => _snapshot.upgrades.Find(u => u.upgradeId == id)?.level ?? 0;
        public bool IsStageCleared(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.IsCleared ?? false;

        /// <summary>서버(or Local) 스냅샷으로 상태를 통째로 덮어쓰고 Changed를 방송한다</summary>
        public void Apply(PlayerSnapshot snap)
        {
            snap.stageProgress ??= new();
            snap.upgrades ??= new();
            _snapshot = snap;
            Changed?.Invoke(this);
        }

        public PlayerSnapshot Snapshot() => _snapshot;
    }
}
