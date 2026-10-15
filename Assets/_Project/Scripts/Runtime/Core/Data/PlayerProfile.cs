using System;
using System.Linq;

namespace Game.Core
{
    /// <summary>현재 플레이어 상태를 들고 있는 순수 스냅샷 캐시. 서버 스냅샷이 오면 통째로 교체한다</summary>
    public sealed class PlayerProfile
    {
        private readonly GameDataStore _data;
        private PlayerSnapshot _snapshot = new PlayerSnapshot { stageProgress = new(), upgrades = new(), items = new(), codex = new(), quests = new(), questChests = new() };

        public event Action<PlayerProfile> Changed;

        public PlayerProfile(GameDataStore data)
        {
            _data = data;
        }

        public string Nickname => _snapshot.nickname ?? string.Empty;
        public int Gold => _snapshot.gold;
        public int Gems => _snapshot.gems;
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
        public int QuestCount(string questId) => _snapshot.quests.Find(q => q.questId == questId)?.count ?? 0;
        public bool IsQuestClaimed(string questId) => _snapshot.quests.Find(q => q.questId == questId)?.claimed ?? false;
        public bool IsQuestChestClaimed(string chestId) => _snapshot.questChests.Contains(chestId);

        /// <summary>보드 게이지의 현재 점수(받은 퀘스트의 점수 합)</summary>
        public int QuestPoints(QuestBoardDefinition board) => QuestRules.Points(board, _snapshot.quests);

        public int EnergyStored => _snapshot.energyStored;

        /// <summary>energyUpdatedAt을 UTC로 해석한다. Apply 전(값 없음)에는 DateTime.UtcNow를 반환한다</summary>
        public DateTime EnergyUpdatedAt => EnergyRule.ParseUpdatedAt(_snapshot.energyUpdatedAt);

        public EnergyConfig EnergyConfig => _data.Energy;

        public int UpgradeLevel(string id) => _snapshot.upgrades.Find(u => u.upgradeId == id)?.level ?? 0;
        public bool IsStageCleared(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.IsCleared ?? false;
        public bool IsTutorialDone(string tutorialId) => _snapshot.tutorials?.Contains(tutorialId) ?? false;
        public bool HasUnclaimedReward(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.HasUnclaimedReward ?? false;

        /// <summary>전투에서 한 번이라도 마주쳐 도감에 등록된 몬스터인가</summary>
        public bool IsMonsterRegistered(int monsterId) => _snapshot.codex.Exists(c => c.monsterId == monsterId);

        /// <summary>도감에 등록됐고 등록 보상을 아직 안 받았는가</summary>
        public bool CanClaimMonsterReward(int monsterId) => _snapshot.codex.Exists(c => c.monsterId == monsterId && !c.claimed);

        /// <summary>서버 스냅샷을 한 번이라도 받았는지. 받기 전 값(레벨 1 등)은 실제 상태가 아니다</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>서버(or Local) 스냅샷으로 상태를 통째로 덮어쓰고 Changed를 방송한다</summary>
        public void Apply(PlayerSnapshot snap)
        {
            snap.stageProgress ??= new();
            snap.upgrades ??= new();
            snap.items ??= new();
            snap.codex ??= new();
            snap.quests ??= new();
            snap.questChests ??= new();
            _snapshot = snap;
            IsLoaded = true;
            Changed?.Invoke(this);
        }

        /// <summary>진행 기록에 줄이 있으면 해금 (LocalBattleApi의 STAGE_LOCKED 판정과 같은 규칙)</summary>
        public bool IsStageUnlocked(int stageId) => _snapshot.stageProgress.Exists(s => s.stageId == stageId);

        /// <summary>0 = 클리어 안 함, 1 = 클리어, 2 = 체력 50%, 3 = 체력 100%</summary>
        public int ClearRating(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.clearRating ?? 0;

        public int ClaimedRating(int stageId) => _snapshot.stageProgress.Find(s => s.stageId == stageId)?.claimedRating ?? 0;

        /// <summary>도전 가능한 가장 높은 스테이지. 기록이 없으면 1</summary>
        public int HighestUnlockedStage =>
            _snapshot.stageProgress.Count == 0 ? 1 : _snapshot.stageProgress.Max(s => s.stageId);


        public PlayerSnapshot Snapshot() => _snapshot;
    }
}
