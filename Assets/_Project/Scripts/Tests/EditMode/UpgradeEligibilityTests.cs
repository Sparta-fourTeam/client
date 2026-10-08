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

        [Test]
        public void LightningSanctionAndJudgement_UsePermanentGatesAndDirectionalExclusion()
        {
            var cards = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3).upgrades;
            var sanction = cards.Find(c => c.id == "lightning_sanction");
            var judgement = cards.Find(c => c.id == "lightning_judgement");
            var state = new State(); state.Levels[3] = 1; state.PermanentLevels[3] = 5;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(sanction, 3, state));
            state.Counts[(3, "lightning_damage")] = 1;
            state.PermanentLevels[3] = 4;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(sanction, 3, state));
            state.PermanentLevels[3] = 5;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(sanction, 3, state));
            state.Counts[(3, "lightning_voltage")] = 1; state.Counts[(3, "lightning_sanction")] = 1;
            state.PermanentLevels[3] = 12;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(judgement, 3, state));
            state.PermanentLevels[3] = 13;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(judgement, 3, state));
            state.Counts[(3, "lightning_storm")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(judgement, 3, state));
            state.Counts.Remove((3, "lightning_storm")); state.Counts[(3, "lightning_judgement")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(cards.Find(c => c.id == "lightning_storm"), 3, state));
        }

        [Test]
        public void FieldCards_RequireStormThenFieldAndRespectOneSuccessfulPick()
        {
            var cards = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 3).upgrades;
            var state = new State(); state.Levels[3] = 1;
            var field = cards.Find(c => c.id == "lightning_field");
            Assert.IsFalse(UpgradeEligibility.CanAcquire(field, 3, state));
            state.Counts[(3, "lightning_storm")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(field, 3, state));
            foreach (string id in new[] { "lightning_field_stable", "lightning_field_voltage" })
            {
                var option = cards.Find(c => c.id == id);
                Assert.IsFalse(UpgradeEligibility.CanAcquire(option, 3, state));
                state.Counts[(3, "lightning_field")] = 1;
                Assert.IsTrue(UpgradeEligibility.CanAcquire(option, 3, state));
                state.Counts[(3, id)] = 1;
                Assert.IsFalse(UpgradeEligibility.CanAcquire(option, 3, state));
                state.Counts.Remove((3, "lightning_field"));
            }
        }

        [Test]
        public void TriangleIce_RequiresAllThreeCardsAndEnablesActualForm()
        {
            var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 4);
            var triangle = data.upgrades.Find(c => c.id == "frost_crystal_triangle");
            var state = new State(); state.Levels[4] = 1;
            state.Counts[(4, "frost_crystal_pierce")] = 1; state.Counts[(4, "frost_crystal_extreme")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(triangle, 4, state));
            state.Counts[(4, "frost_crystal_damage")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(triangle, 4, state));
            var go = new UnityEngine.GameObject("TriangleCardTest");
            try
            {
                var weapon = Casters.Projectile(data, go, go.transform, new NullEnemyTargetProvider());
                weapon.UseChildCaster(new NoopChildCaster());
                Assert.IsTrue(weapon.LevelUp(triangle)); Assert.IsFalse(weapon.LevelUp(triangle));
                var stats = (SkillStats)typeof(SkillBase).GetField("stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(weapon);
                Assert.AreEqual(SkillForm.TriangleIce, stats.Cast.Form);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void LogRepeatAndSize_KeepConservativeDamagePrerequisite()
        {
            var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
            var state = new State(); state.Levels[5] = 1;
            foreach (string id in new[] { "log_repeat", "log_size" })
            {
                var card = data.upgrades.Find(c => c.id == id);
                Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 5, state));
                state.Counts[(5, "log_damage")] = 1;
                Assert.IsTrue(UpgradeEligibility.CanAcquire(card, 5, state));
                state.Counts.Remove((5, "log_damage"));
            }
        }

        [Test]
        public void LogForms_RequirePredecessorsPermanentNineAndMutualExclusion()
        {
            var data = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5);
            var large = data.upgrades.Find(c => c.id == "log_large"); var fire = data.upgrades.Find(c => c.id == "log_fire");
            var state = new State(); state.Levels[5] = 1; state.PermanentLevels[5] = 9;
            state.Counts[(5, "log_size")] = 1; state.Counts[(5, "log_impact")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(large, 5, state));
            state.Counts[(5, "log_weight")] = 1; Assert.IsTrue(UpgradeEligibility.CanAcquire(large, 5, state));
            state.Counts[(5, "log_damage")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(fire, 5, state));
            state.Levels[2] = 1; state.PermanentLevels[5] = 8;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(fire, 5, state));
            state.PermanentLevels[5] = 9; Assert.IsTrue(UpgradeEligibility.CanAcquire(fire, 5, state));
            state.Counts[(5, "log_large")] = 1; Assert.IsFalse(UpgradeEligibility.CanAcquire(fire, 5, state));
            state.Counts.Remove((5, "log_large")); state.Counts[(5, "log_fire")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(large, 5, state));
        }

        [Test]
        public void ReserveLog_RequiresWeightAndPermanentThirteen()
        {
            var card = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 5).upgrades.Find(c => c.id == "log_reserve");
            var state = new State(); state.Levels[5] = 1; state.PermanentLevels[5] = 13;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 5, state));
            state.Counts[(5, "log_weight")] = 1; state.PermanentLevels[5] = 12;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 5, state));
            state.PermanentLevels[5] = 13; Assert.IsTrue(UpgradeEligibility.CanAcquire(card, 5, state));
            state.Counts[(5, "log_reserve")] = 1; Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 5, state));
        }

        private static State Owned(int level = 1) => new State { Levels = { [1] = level } };
        private static SkillUpgradeOption Option(string id = "upgrade") => new SkillUpgradeOption { id = id, maxPickCount = 3 };

        [Test]
        public void KunaiAmplification_RequiresPermanent13AndAllowsOneSelection()
        {
            var option = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 1).upgrades.Find(c => c.id == "arrow_amplify");
            var state = Owned();
            state.Counts[(1, "arrow_spread")] = 1;
            state.PermanentLevels[1] = 12;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(option, 1, state));
            state.PermanentLevels[1] = 13;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(option, 1, state));
            state.Counts[(1, option.id)] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(option, 1, state));
        }

        [Test]
        public void KunaiLightningRoute_RequiresPredecessorsAndExcludesExplosion()
        {
            var cards = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 1).upgrades;
            var state = Owned();
            var shock = cards.Find(c => c.id == "arrow_shock");
            Assert.IsFalse(UpgradeEligibility.CanAcquire(shock, 1, state));
            state.Levels[3] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(shock, 1, state));
            state.Counts[(1, "arrow_explosion")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(shock, 1, state));
            state.Counts.Remove((1, "arrow_explosion"));
            state.Counts[(1, "arrow_shock")] = 1;
            var thunder = cards.Find(c => c.id == "arrow_thunder");
            Assert.IsFalse(UpgradeEligibility.CanAcquire(thunder, 1, state));
            state.Counts[(1, "arrow_light")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(thunder, 1, state));
            var rod = cards.Find(c => c.id == "arrow_rod");
            Assert.IsFalse(UpgradeEligibility.CanAcquire(rod, 1, state));
            state.Counts[(1, "arrow_quick")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(rod, 1, state));
            var auxiliary = cards.Find(c => c.id == "arrow_aux_rod");
            state.Counts[(1, "arrow_rod")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(auxiliary, 1, state));
            state.Counts[(1, "arrow_spread")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(auxiliary, 1, state));
        }

        [Test]
        public void BurnCards_RequireTheirPredecessors()
        {
            var catalog = new DefaultSkillDataProvider(new GameDataStore()).LoadAll();
            var state = Owned();
            var flame = catalog.Find(w => w.id == 1).upgrades.Find(c => c.id == "arrow_flame");
            state.Counts[(1, "arrow_explosion")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(flame, 1, state));
            state.Counts[(1, "arrow_sharp")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(flame, 1, state));
            var burn = catalog.Find(w => w.id == 2).upgrades.Find(c => c.id == "fireball_burn");
            state.Levels[2] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(burn, 2, state));
            state.Counts[(2, "fireball_explosion_damage")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(burn, 2, state));
        }

        [Test]
        public void FireballTraining_RequiresImpactAndExpandedExplosion()
        {
            var option = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 2).upgrades.Find(c => c.id == "fireball_training");
            var state = Owned();
            state.Levels[2] = 1;
            state.Counts[(2, "fireball_impact_damage")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(option, 2, state));
            state.Counts[(2, "fireball_explosion_radius")] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(option, 2, state));
            state.Counts[(2, option.id)] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(option, 2, state));
        }

        [Test]
        public void Enbakutsu_UnlocksAt13AndRepeatRestrictionEndsAt25()
        {
            var cards = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == 2).upgrades;
            var enbakutsu = cards.Find(c => c.id == "fireball_phoenix");
            var repeat = cards.Find(c => c.id == "fireball_burst");
            var state = Owned(); state.Levels[2] = 1;
            state.Counts[(2, "fireball_impact_damage")] = 1;
            state.Counts[(2, "fireball_explosion_radius")] = 1;
            state.PermanentLevels[2] = 12;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(enbakutsu, 2, state));
            state.PermanentLevels[2] = 13;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(enbakutsu, 2, state));
            state.Counts[(2, "fireball_burst")] = 1;
            state.PermanentLevels[2] = 24;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(enbakutsu, 2, state));
            state.PermanentLevels[2] = 25;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(enbakutsu, 2, state));
            state.Counts[(2, "fireball_phoenix")] = 1;
            state.PermanentLevels[2] = 24;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(repeat, 2, state));
            state.PermanentLevels[2] = 25;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(repeat, 2, state));
        }

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
            o.requiredCardCounts = new[] { new CardCountRequirement { skillId = 2, cardId = "explosion" } };
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
            o.enabled = true; s.Counts[(1, o.id)] = 1;
            o.requiredCardCounts = new[] { new CardCountRequirement { cardId = o.id } };
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
