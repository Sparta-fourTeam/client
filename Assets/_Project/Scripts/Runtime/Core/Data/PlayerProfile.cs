using System;
using System.Linq;

namespace Game.Core
{
    /// <summary>현재 플레이어 상태를 들고 있는 순수 스냅샷 캐시. 서버 스냅샷이 오면 통째로 교체한다</summary>
    public sealed class PlayerProfile
    {
        private readonly GameDataStore _data;
        private PlayerSnapshot _snapshot = new PlayerSnapshot { stageProgress = new(), upgrades = new(), items = new(), equipments = new() };

        public event Action<PlayerProfile> Changed;

        public PlayerProfile(GameDataStore data)
        {
            _data = data;
        }

        public int Gold => _snapshot.gold;
        public int Exp => _snapshot.exp;
        public int Level => _data.PlayerLevels.At(_snapshot.exp).level;
        /// <summary>현재 레벨 안에서 쌓은 경험치</summary>
        public int ExpIntoLevel => _data.PlayerLevels.At(_snapshot.exp).expIntoLevel;
        /// <summary>다음 레벨까지 필요한 경험치 (만렙이면 0)</summary>
        public int ExpRequiredForNext => _data.PlayerLevels.RequiredToNext(Level);
        public bool IsMaxLevel => ExpRequiredForNext == 0;

        /// <summary>현재 레벨 안에서의 진행률 0~1. 만렙이면 1</summary>
        public float LevelProgress => IsMaxLevel ? 1f : (float)ExpIntoLevel / ExpRequiredForNext;
        public int ItemQuantity(string itemId) => _snapshot.items.Find(item => item.itemId == itemId)?.quantity ?? 0;
        public int EnergyStored => _snapshot.energyStored;

        /// <summary>energyUpdatedAt을 UTC로 해석한다. Apply 전(값 없음)에는 DateTime.UtcNow를 반환한다</summary>
        public DateTime EnergyUpdatedAt => EnergyRule.ParseUpdatedAt(_snapshot.energyUpdatedAt);

        public EnergyConfig EnergyConfig => _data.Energy;

        /// <summary>장비의 강화 레벨. 강화한 적이 없으면 0</summary>
        public int EquipmentLevel(string equipmentId) => _snapshot.equipments.Find(e => e.equipmentId == equipmentId)?.level ?? 0;
        public int UpgradeLevel(string id) => _snapshot.upgrades.Find(u => u.upgradeId == id)?.level ?? 0;
        public bool IsStageCleared(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.IsCleared ?? false;
        public bool HasUnclaimedReward(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.HasUnclaimedReward ?? false;

        /// <summary>서버(or Local) 스냅샷으로 상태를 통째로 덮어쓰고 Changed를 방송한다</summary>
        public void Apply(PlayerSnapshot snap)
        {
            snap.stageProgress ??= new();
            snap.upgrades ??= new();
            snap.items ??= new();
            snap.equipments ??= new();
            _snapshot = snap;
            Changed?.Invoke(this);
        }

        /// <summary>진행 기록에 줄이 있으면 해금 (LocalBattleApi의 STAGE_LOCKED 판정과 같은 규칙)</summary>
        public bool IsStageUnlocked(int stageId) => _snapshot.stageProgress.Exists(s => s.stageId == stageId);

        /// <summary>0 = 클리어 안 함, 1 = 클리어, 2 = 체력 50%, 3 = 체력 100%</summary>
        public int ClearRating(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.clearRating ?? 0;

        /// <summary>도전 가능한 가장 높은 스테이지. 기록이 없으면 1</summary>
        public int HighestUnlockedStage =>
            _snapshot.stageProgress.Count == 0 ? 1 : _snapshot.stageProgress.Max(s => s.stageId);


        public PlayerSnapshot Snapshot() => _snapshot;
    }
}
