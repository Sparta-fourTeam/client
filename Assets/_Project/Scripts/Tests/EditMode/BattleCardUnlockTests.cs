using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class BattleCardUnlockTests
    {
        private static SkillData Skill(int id, int unlockLevel) => new()
        {
            id = id,
            name = $"스킬{id}",
            unlockLevel = unlockLevel,
            baseStats = new SkillBaseStats(),
            maxLevel = 5,
            upgrades = new List<SkillUpgradeOption>(),
        };

        private static List<UpgradeChoice> NewSkillCards(Func<SkillData, bool> isUnlocked, params SkillData[] catalog) =>
            new SkillUpgradeChoices(new List<SkillBase>(), catalog, new EmptyState(), _ => true, isUnlocked).Candidates();

        [Test(Description = "새 스킬 카드는 열린 스킬만 나온다 (플레이어 레벨 기준)")]
        public void NewSkillCards_OnlyUnlockedSkills()
        {
            var profile = new PlayerProfile(new GameDataStore());   // 경험치 0 = 레벨 1
            var unlock = new ProfileSkillUnlock(profile);

            var offered = NewSkillCards(unlock.IsUnlocked, Skill(1, 1), Skill(2, 3), Skill(3, 5));

            CollectionAssert.AreEquivalent(new[] { 1 }, offered.Select(c => c.newSkillData.id).ToList());
        }

        [Test(Description = "레벨이 오르면 해금 레벨에 도달한 스킬이 새 스킬 카드에 추가된다")]
        public void NewSkillCards_GrowWithPlayerLevel()
        {
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            var unlock = new ProfileSkillUnlock(profile);
            int expForLevel3 = data.PlayerLevels.RequiredToNext(1) + data.PlayerLevels.RequiredToNext(2);
            profile.Apply(new PlayerSnapshot { exp = expForLevel3 });

            var offered = NewSkillCards(unlock.IsUnlocked, Skill(1, 1), Skill(2, 3), Skill(3, 5));

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, offered.Select(c => c.newSkillData.id).ToList());
        }

        [Test(Description = "열림 판정을 연결하지 않으면 모든 스킬이 열린 것으로 본다 (기존 동작 유지)")]
        public void NoUnlockSource_OffersEverySkill()
        {
            var offered = NewSkillCards(null, Skill(1, 1), Skill(2, 99));

            Assert.AreEqual(2, offered.Count);
        }

        private sealed class EmptyState : IUpgradeState
        {
            public int GetWeaponLevel(int weaponId) => 0;
            public int GetPermanentWeaponLevel(int weaponId) => 0;
            public int GetAcquiredCount(int weaponId, string cardId) => 0;
        }
    }
}
