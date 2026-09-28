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

        /// <summary>서버(or Local) 스냅샷으로 상태를 통째로 덮어쓰고 Changed를 방송한다</summary>
        public void Apply(PlayerSnapshot snap)
        {
            _snapshot = snap;
            Changed?.Invoke(this);
        }

        public PlayerSnapshot Snapshot() => _snapshot;
    }
}
