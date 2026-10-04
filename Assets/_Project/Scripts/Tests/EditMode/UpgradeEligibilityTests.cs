using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UpgradeEligibilityTests
    {
        private sealed class State : IUpgradeState
        {
            public readonly Dictionary<int, int> Levels = new();
            public readonly Dictionary<int, int> PermanentLevels = new();
            public readonly Dictionary<(int, string), int> Counts = new();
            public int GetWeaponLevel(int id) => Levels.TryGetValue(id, out var level) ? level : 0;
            public int GetPermanentWeaponLevel(int id) => PermanentLevels.TryGetValue(id, out var level) ? level : 0;
            public int GetAcquiredCount(int id, string card) => Counts.TryGetValue((id, card), out var count) ? count : 0;
        }

        private static State Owned(int level = 1) => new State { Levels = { [1] = level } };
        private static WeaponUpgradeOption Option(string id = "upgrade") => new WeaponUpgradeOption { id = id, maxPickCount = 3 };

        [Test]
        public void RequiredWeapons_AllMustBeOwned()
        {
            var s = Owned(); var o = Option(); o.requiredWeaponIds = new[] { 2, 3 };
            s.Levels[2] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
            s.Levels[3] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(o, 1, s));
        }

        [Test]
        public void RequiredCardCount_NeedsTwoPicks()
        {
            var s = Owned(); var o = Option();
            o.requiredCardCounts = new[] { new CardCountRequirement { cardId = "repeat", count = 2 } };
            s.Counts[(1, "repeat")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
            s.Counts[(1, "repeat")] = 2;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(o, 1, s));
        }

        [Test]
        public void CrossWeaponRequirements_DoNotUseSameNamedLocalCard()
        {
            var s = Owned(); var o = Option();
            o.requiredCardCounts = new[] { new CardCountRequirement { weaponId = 2, cardId = "explosion" } };
            s.Counts[(1, "explosion")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
            s.Counts[(2, "explosion")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(o, 1, s));
        }

        [TestCase(12, false)]
        [TestCase(13, true)]
        public void MinimumBattleLevel_UsesBoundary(int level, bool allowed)
        {
            var o = Option(); o.minBattleLevel = 13;
            Assert.AreEqual(allowed, UpgradeEligibility.CanAcquire(o, 1, Owned(level)));
        }

        [TestCase(24, false)]
        [TestCase(25, true)]
        public void ConditionalExclusion_IsLiftedAt25(int level, bool allowed)
        {
            var s = Owned(); s.PermanentLevels[1] = level; s.Counts[(1, "repeat")] = 1;
            var o = Option(); o.exclusions = new[] { new CardExclusion { cardId = "repeat", belowPermanentLevel = 25 } };
            Assert.AreEqual(allowed, UpgradeEligibility.CanAcquire(o, 1, s));
        }

        [TestCase("shock", "explosion")]
        [TestCase("explosion", "shock")]
        public void PermanentExclusion_BlocksBothConfiguredDirections(string picked, string next)
        {
            var s = Owned(99); s.Counts[(1, picked)] = 1;
            var o = Option(next); o.exclusions = new[] { new CardExclusion { cardId = picked } };
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
        }

        [Test]
        public void DirectionalExclusions_AllowRepeatThenVolleyThenPierce()
        {
            var s = Owned();
            var repeat = Option("repeat"); repeat.exclusions = new[] { new CardExclusion { cardId = "volley" } };
            var volley = Option("volley"); volley.exclusions = new[] { new CardExclusion { cardId = "pierce" } };
            var pierce = Option("pierce");
            Assert.IsTrue(UpgradeEligibility.CanAcquire(repeat, 1, s)); s.Counts[(1, "repeat")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(volley, 1, s)); s.Counts[(1, "volley")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(repeat, 1, s));
            Assert.IsTrue(UpgradeEligibility.CanAcquire(pierce, 1, s)); s.Counts[(1, "pierce")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(volley, 1, s));
            Assert.AreEqual(1, s.Counts[(1, "repeat")]);
        }

        [Test]
        public void ThunderBeforeStorm_IsAllowedButReverseIsBlocked()
        {
            var s = Owned();
            var thunder = Option("thunder"); thunder.exclusions = new[] { new CardExclusion { cardId = "storm" } };
            Assert.IsTrue(UpgradeEligibility.CanAcquire(thunder, 1, s)); s.Counts[(1, "thunder")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(Option("storm"), 1, s));
            s.Counts[(1, "storm")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(thunder, 1, s));
        }

        [Test]
        public void PickLimit_BlocksAtMaximum()
        {
            var s = Owned(); var o = Option(); s.Counts[(1, o.id)] = 3;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
        }

        [Test]
        public void DisabledAndSelfDependentOptions_AreBlocked()
        {
            var s = Owned(); var o = Option(); o.enabled = false;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
            o.enabled = true; o.requiredCardIds = new[] { o.id }; s.Counts[(1, o.id)] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
            o.requiredCardIds = null; o.requiredCardCounts = new[] { new CardCountRequirement { cardId = o.id } };
            Assert.IsFalse(UpgradeEligibility.CanAcquire(o, 1, s));
        }

        [TestCase(12, false)]
        [TestCase(13, true)]
        public void PermanentGate_DoesNotUseBattleLevel(int permanentLevel, bool allowed)
        {
            var s = Owned(99); s.PermanentLevels[1] = permanentLevel;
            var o = Option(); o.minPermanentLevel = 13;
            Assert.AreEqual(allowed, UpgradeEligibility.CanAcquire(o, 1, s));
        }
    }
}
