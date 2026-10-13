using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Battle progression only. Values are committed after every participant is prepared.</summary>
    internal sealed class SkillUpgradeState
    {
        private readonly SkillData data;
        private readonly Dictionary<string, int> acquiredCounts = new();
        public int Level { get; private set; } = 1;
        public int UpgradeCount => Level - 1;
        public bool IsMaxLevel => UpgradeCount >= data.maxLevel;

        public SkillUpgradeState(SkillData data) => this.data = data;

        public int GetAcquiredCount(string cardId)
        {
            var option = data.upgrades?.Find(o => o.id == cardId);
            string key = string.IsNullOrEmpty(option?.sharedId) ? cardId : option.sharedId;
            return acquiredCounts.TryGetValue(key, out int count) ? count : 0;
        }

        public bool CanPrepare(SkillUpgradeOption option) => !IsMaxLevel && option != null && option.enabled
            && !string.IsNullOrEmpty(option.id) && option.maxPickCount > 0
            && GetAcquiredCount(Key(option)) < option.maxPickCount;

        public void Commit(SkillUpgradeOption option)
        {
            string key = Key(option);
            acquiredCounts[key] = GetAcquiredCount(key) + 1;
            Level++;
        }

        private static string Key(SkillUpgradeOption option) =>
            string.IsNullOrEmpty(option.sharedId) ? option.id : option.sharedId;
    }
}
