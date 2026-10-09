using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SkillSkillStatusTests
    {
        private sealed class TestSkill : SkillBase
        {
            public int Fired;
            public float Damage => stats.Cast.Damage;

            public TestSkill(SkillData data) : base(data, null, null)
            {
            }

            /// <summary>Tick이 Time.deltaTime을 쓰는데 EditMode에서는 0이라, 쿨타임이 줄어든 상황을 직접 만든다</summary>
            public void SetCooldownTimer(float value)
            {
                cooldownTimer = value;
            }

            protected override void OnFire()
            {
                Fired++;
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

        private static SkillUpgradeOption AttackSpeed(float percent)
        {
            return new SkillUpgradeOption
            {
                id = "speed",
                effects = new List<EffectDef> { new EffectDef { kind = "attackSpeed", value = percent } }
            };
        }

        [Test(Description = "아직 한 번도 발사하지 않았으면 쿨타임 진행도는 0(발사 가능)이다")]
        public void CooldownRatio_BeforeFirstFire_IsZero()
        {
            var weapon = new TestSkill(Data());

            Assert.AreEqual(0f, weapon.CooldownRatio);
        }

        [Test(Description = "발사한 직후에는 쿨타임 진행도가 1이다")]
        public void CooldownRatio_RightAfterFire_IsOne()
        {
            var weapon = new TestSkill(Data());

            weapon.Tick();

            Assert.AreEqual(1, weapon.Fired);
            Assert.AreEqual(1f, weapon.CooldownRatio);
        }

        [Test(Description = "쿨타임이 절반 남았으면 진행도는 0.5다")]
        public void CooldownRatio_HalfRemaining_IsHalf()
        {
            var weapon = new TestSkill(Data(2f));
            weapon.SetCooldownTimer(1f);

            Assert.AreEqual(0.5f, weapon.CooldownRatio, 0.0001f);
        }

        [Test(Description = "쿨타임이 0 이하인 무기는 0으로 나눠 NaN이 되지 않고 0을 돌려준다")]
        public void CooldownRatio_NonPositiveCooldown_IsZero()
        {
            var weapon = new TestSkill(Data(0f));
            weapon.SetCooldownTimer(1f);

            Assert.AreEqual(0f, weapon.CooldownRatio);
        }

        [Test(Description = "발사 직후 공격 속도를 강화해 쿨타임이 줄어도 진행도는 1을 넘지 않는다")]
        public void CooldownRatio_AfterCooldownShrinks_IsClampedToOne()
        {
            var weapon = new TestSkill(Data(2f));
            weapon.Tick();

            weapon.LevelUp(AttackSpeed(50f));

            Assert.AreEqual(1f, weapon.CooldownRatio);
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
