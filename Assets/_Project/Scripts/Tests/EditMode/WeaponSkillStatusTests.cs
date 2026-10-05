using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class WeaponSkillStatusTests
    {
        private sealed class TestWeapon : WeaponBase
        {
            public int Fired;
            public float Damage => stats.Cast.Damage;

            public TestWeapon(WeaponData data) : base(data, null, null)
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

        private static WeaponData Data(float cooldown = 2f)
        {
            return new WeaponData
            {
                id = 7,
                name = "테스트",
                iconKey = "weapon_test",
                castType = CastType.Projectile,
                baseStats = new WeaponBaseStats { cooldown = cooldown, baseDamage = 10f, range = 10f, speed = 10f, hitCount = 1 },
                maxLevel = 5,
                upgrades = new List<WeaponUpgradeOption>()
            };
        }

        private static WeaponUpgradeOption AttackSpeed(float percent)
        {
            return new WeaponUpgradeOption
            {
                id = "speed",
                effects = new List<StatEffect> { new StatEffect { type = UpgradeType.AttackSpeed, value = percent } }
            };
        }

        [Test(Description = "무기는 Id, Level, IconKey를 HUD에 그대로 내준다")]
        public void Status_ExposesIdLevelAndIconKey()
        {
            ISkillStatus status = new TestWeapon(Data());

            Assert.AreEqual(7, status.Id);
            Assert.AreEqual(1, status.Level);
            Assert.AreEqual("weapon_test", status.IconKey);
        }

        [Test(Description = "강화하면 Level이 HUD에 올라간 값으로 보인다")]
        public void Status_LevelFollowsLevelUp()
        {
            var weapon = new TestWeapon(Data());

            weapon.LevelUp(AttackSpeed(10f));

            Assert.AreEqual(2, ((ISkillStatus)weapon).Level);
        }

        [Test(Description = "아직 한 번도 발사하지 않았으면 쿨타임 진행도는 0(발사 가능)이다")]
        public void CooldownRatio_BeforeFirstFire_IsZero()
        {
            var weapon = new TestWeapon(Data());

            Assert.AreEqual(0f, weapon.CooldownRatio);
        }

        [Test(Description = "발사한 직후에는 쿨타임 진행도가 1이다")]
        public void CooldownRatio_RightAfterFire_IsOne()
        {
            var weapon = new TestWeapon(Data());

            weapon.Tick();

            Assert.AreEqual(1, weapon.Fired);
            Assert.AreEqual(1f, weapon.CooldownRatio);
        }

        [Test(Description = "쿨타임이 절반 남았으면 진행도는 0.5다")]
        public void CooldownRatio_HalfRemaining_IsHalf()
        {
            var weapon = new TestWeapon(Data(2f));
            weapon.SetCooldownTimer(1f);

            Assert.AreEqual(0.5f, weapon.CooldownRatio, 0.0001f);
        }

        [Test(Description = "쿨타임이 0 이하인 무기는 0으로 나눠 NaN이 되지 않고 0을 돌려준다")]
        public void CooldownRatio_NonPositiveCooldown_IsZero()
        {
            var weapon = new TestWeapon(Data(0f));
            weapon.SetCooldownTimer(1f);

            Assert.AreEqual(0f, weapon.CooldownRatio);
        }

        [Test(Description = "발사 직후 공격 속도를 강화해 쿨타임이 줄어도 진행도는 1을 넘지 않는다")]
        public void CooldownRatio_AfterCooldownShrinks_IsClampedToOne()
        {
            var weapon = new TestWeapon(Data(2f));
            weapon.Tick();

            weapon.LevelUp(AttackSpeed(50f));

            Assert.AreEqual(1f, weapon.CooldownRatio);
        }

        [Test]
        public void InvalidEffect_DoesNotPartiallyApplyUpgrade()
        {
            var weapon = new TestWeapon(Data());
            var option = new WeaponUpgradeOption
            {
                id = "invalid",
                effects = new List<StatEffect>
                {
                    new StatEffect { type = UpgradeType.Damage, value = 80f },
                    new StatEffect { type = (UpgradeType)999, value = 1f }
                }
            };
            Assert.IsFalse(weapon.LevelUp(option));
            Assert.AreEqual(1, weapon.Level);
            Assert.AreEqual(0, weapon.GetAcquiredCount(option.id));
            Assert.AreEqual(10f, weapon.Damage);
        }
    }
}
