using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SkillSkillStatusTests
    {
        private sealed class TestSkill : SkillBase
        {
            public float Damage => stats.Cast.Damage;

            public TestSkill(SkillData data) : base(data, null, null)
            {
            }

            protected override void OnFire()
            {
            }
        }

        private static SkillData Data(float cooldown = 2f)
        {
            return new SkillData
            {
                id = 7,
                name = "테스트",
                assetKey = "weapon_test",
                castType = CastType.Projectile,
                baseStats = new SkillBaseStats { cast = { cooldown = cooldown, baseDamage = 10f, range = 10f, projectileCount = 1 }, projectile = { speed = 10f } },
                maxLevel = 5,
                upgrades = new List<SkillUpgradeOption>()
            };
        }

        [Test]
        public void InvalidEffect_DoesNotPartiallyApplyUpgrade()
        {
            var weapon = new TestSkill(Data());
            var option = new SkillUpgradeOption
            {
                id = "invalid",
                effects = new List<EffectDef>
                {
                    new EffectDef { kind = "damage", value = 80f },
                    new EffectDef { kind = "unknownKind", value = 1f }
                }
            };
            Assert.IsFalse(weapon.LevelUp(option));
            Assert.AreEqual(1, weapon.Level);
            Assert.AreEqual(0, weapon.GetAcquiredCount(option.id));
            Assert.AreEqual(10f, weapon.Damage);
        }
    }
}
