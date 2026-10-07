using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    // 방어막, 회피, 체력 조건 발동, 피격 시 회복·가속, 주기적 가속 패시브와 그 데이터 정의
    public sealed class DefensivePassiveTests
    {
        private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);

        private sealed class Recorder<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }

        // 미리 정한 값을 차례로 돌려주고, 다 쓰면 마지막 값을 계속 돌려주는 랜덤
        private sealed class ScriptedRandom : IRandomProvider
        {
            private readonly Queue<float> _values;
            private float _last;

            public ScriptedRandom(params float[] values)
            {
                _values = new Queue<float>(values);
                _last = values.Length > 0 ? values[values.Length - 1] : 0f;
            }

            public float Range(float min, float max) => _values.Count > 0 ? _values.Dequeue() : _last;
        }

        private static EnemyModel Model(IPassive passive, int maxHp = 100, float speed = 2f) =>
            new EnemyModel(1, speed, EnemyType.Normal, maxHp, Attack, new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                new[] { passive });

        // 스킬 공격 한 번(적중 피해)
        private static void Hit(EnemyModel model, int amount, Element element = Element.Neutral) =>
            model.TakeDamage(new DamageInfo(amount, 1, element, CastType.Projectile, true));

        // 같은 공격의 부가 피해(폭발 등, 적중 피해가 아님)
        private static void Splash(EnemyModel model, int amount) =>
            model.TakeDamage(new DamageInfo(amount, 1, Element.Neutral, CastType.Projectile, false));

        // ───────── 방어막 ─────────

        [Test(Description = "시작 방어막은 처음 N번의 적중을 막고 그 뒤부터 맞는다")]
        public void Shield_Start_BlocksTheFirstHits()
        {
            var model = Model(new ShieldPassive(ShieldTrigger.Start, 2));

            Hit(model, 10);
            Hit(model, 10);
            Assert.AreEqual(100, model.Hp);

            Hit(model, 10);
            Assert.AreEqual(90, model.Hp);
        }

        [Test(Description = "방어막이 켜져 있는 동안 부가 피해도 막지만 막은 횟수에는 세지 않는다")]
        public void Shield_BlocksSplashWithoutConsumingHits()
        {
            var model = Model(new ShieldPassive(ShieldTrigger.Start, 1));

            Splash(model, 10);
            Splash(model, 10);
            Assert.AreEqual(100, model.Hp);

            Hit(model, 10); // 이 적중이 방어막을 깬다
            Hit(model, 10);
            Assert.AreEqual(90, model.Hp);
        }

        [Test(Description = "상태이상 지속 피해(스킬 밖)는 방어막이 막지 않는다")]
        public void Shield_DoesNotBlockNonSkillDamage()
        {
            var model = Model(new ShieldPassive(ShieldTrigger.Start, 3));

            model.TakeDamage(10);

            Assert.AreEqual(90, model.Hp);
        }

        [Test]
        public void Shield_Duration_ExpiresWithTime()
        {
            var model = Model(new ShieldPassive(ShieldTrigger.Start, 99, duration: 2f));

            model.TickPassives(1f);
            Hit(model, 10);
            Assert.AreEqual(100, model.Hp, "아직 유지된다");

            model.TickPassives(1f);
            Hit(model, 10);
            Assert.AreEqual(90, model.Hp, "시간이 지나 사라졌다");
        }

        [Test(Description = "피격 방어막은 처음 맞은 뒤에 켜지고 한 번만 켜진다")]
        public void Shield_OnHit_ArmsAfterTheFirstHit_AndOnlyOnce()
        {
            var model = Model(new ShieldPassive(ShieldTrigger.OnHit, 1));

            Hit(model, 10); // 맞고 방어막이 켜진다
            Assert.AreEqual(90, model.Hp);

            Hit(model, 10); // 방어막이 막는다
            Assert.AreEqual(90, model.Hp);

            Hit(model, 10); // 깨진 뒤 다시 켜지지 않는다
            Hit(model, 10);
            Assert.AreEqual(70, model.Hp);
        }

        [Test]
        public void Shield_OnHit_RollsChance()
        {
            var model = Model(new ShieldPassive(ShieldTrigger.OnHit, 1, chance: 0.4f, random: new ScriptedRandom(0.9f, 0.1f)));

            Hit(model, 10); // 0.9 >= 0.4 → 켜지지 않는다
            Hit(model, 10); // 0.1 < 0.4 → 켜진다
            Hit(model, 10); // 막힌다

            Assert.AreEqual(80, model.Hp);
        }

        [Test(Description = "방어막이 켜져 있는 동안만 지정한 상태이상에 걸리지 않는다")]
        public void Shield_GrantsImmunityWhileUp()
        {
            var model = Model(new ShieldPassive(ShieldTrigger.Start, 1, immunities: StatusImmunity.AllDebuffs));

            model.ApplyStun(3f);
            Assert.IsFalse(model.IsStunned);

            Hit(model, 10); // 방어막이 깨진다
            model.ApplyStun(3f);
            Assert.IsTrue(model.IsStunned);
        }

        [Test]
        public void Shield_RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShieldPassive(ShieldTrigger.Start, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShieldPassive(ShieldTrigger.Start, 1, duration: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShieldPassive(ShieldTrigger.OnHit, 1, chance: 0f, random: new ScriptedRandom(0f)));
            Assert.Throws<ArgumentNullException>(() => new ShieldPassive(ShieldTrigger.OnHit, 1, chance: 0.5f));
            Assert.DoesNotThrow(() => new ShieldPassive(ShieldTrigger.Start, 1, chance: 1f));
        }

        // ───────── 회피 ─────────

        [Test]
        public void Evade_AvoidsTheHit_AndTheDebuffsThatComeWithIt()
        {
            var model = Model(new EvadePassive(1f));

            Hit(model, 30);
            model.ApplyStun(3f);

            Assert.AreEqual(100, model.Hp);
            Assert.IsFalse(model.IsStunned, "피한 공격의 상태이상도 막힌다");
        }

        [Test(Description = "피한 공격의 부가 피해(폭발 등)도 같이 막고, 시간이 지나면 풀린다")]
        public void Evade_AlsoBlocksTheFollowUpSplashShortly()
        {
            var model = Model(new EvadePassive(1f));

            Hit(model, 30);
            Splash(model, 20);
            Assert.AreEqual(100, model.Hp);

            model.TickStatus(0.2f);
            model.TickPassives(0.2f);
            model.ApplyStun(3f);
            Assert.IsTrue(model.IsStunned, "면역이 풀렸다");
        }

        [Test]
        public void Evade_DoesNotAvoidNonSkillDamage()
        {
            var model = Model(new EvadePassive(1f));

            model.TakeDamage(10);

            Assert.AreEqual(90, model.Hp);
        }

        [Test]
        public void Evade_RollsChancePerHit()
        {
            var model = Model(new EvadePassive(0.5f, new ScriptedRandom(0.2f, 0.9f)));

            Hit(model, 10); // 0.2 < 0.5 → 피한다
            model.TickStatus(0.2f);
            model.TickPassives(0.2f);
            Hit(model, 10); // 0.9 → 맞는다

            Assert.AreEqual(90, model.Hp);
        }

        [Test]
        public void Evade_RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EvadePassive(0f, new ScriptedRandom(0f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EvadePassive(1.5f, new ScriptedRandom(0f)));
            Assert.Throws<ArgumentNullException>(() => new EvadePassive(0.5f));
            Assert.DoesNotThrow(() => new EvadePassive(1f));
        }

        // ───────── 체력 조건 발동 ─────────

        [Test(Description = "체력 비율이 기준 이하가 되면 켜지고 시간에 걸쳐 회복하며 한 번만 켜진다")]
        public void LowHp_HealsOverTimeOnce()
        {
            var model = Model(new LowHpPassive(0.3f, 5f, healRatio: 0.5f));

            Hit(model, 60); // 체력 40: 아직 아니다
            model.TickPassives(5f);
            Assert.AreEqual(40, model.Hp, "발동 전에는 회복하지 않는다");

            Hit(model, 15); // 체력 25 ≤ 30%: 발동
            for (int i = 0; i < 5; i++)
            {
                model.TickPassives(1f);
            }

            Assert.AreEqual(75, model.Hp, "5초 동안 최대 체력의 50%(50)를 회복한다");

            Hit(model, 60); // 다시 기준 아래로 내려가도 두 번 켜지지 않는다
            for (int i = 0; i < 5; i++)
            {
                model.TickPassives(1f);
            }

            Assert.AreEqual(15, model.Hp);
        }

        [Test(Description = "무적은 켜져 있는 동안 상태이상 지속 피해까지 모두 막는다")]
        public void LowHp_InvulnerableBlocksEverythingWhileActive()
        {
            var model = Model(new LowHpPassive(0.3f, 2f, invulnerable: true));

            Hit(model, 75); // 체력 25: 발동
            Hit(model, 10);
            model.TakeDamage(10);
            Assert.AreEqual(25, model.Hp);

            model.TickPassives(2f);
            Hit(model, 10);
            Assert.AreEqual(15, model.Hp, "시간이 지나 무적이 끝났다");
        }

        [Test]
        public void LowHp_SpeedBoostAndImmunityLastOnlyWhileActive()
        {
            var model = Model(new LowHpPassive(0.3f, 2f, speedMultiplier: 3f, immunities: StatusImmunity.AllDebuffs), speed: 2f);

            Hit(model, 75);
            model.ApplyStun(3f);
            Assert.AreEqual(6f, model.MoveSpeed, 0.0001f);
            Assert.IsFalse(model.IsStunned);

            model.TickPassives(2f);
            model.ApplyStun(3f);
            Assert.IsTrue(model.IsStunned);
            Assert.AreEqual(0f, model.MoveSpeed, "이제 기절했다");
            model.TickStatus(3f);
            Assert.AreEqual(2f, model.MoveSpeed, 0.0001f, "가속은 끝났다");
        }

        [Test(Description = "기절·빙결·마비 중에는 시간이 흐르지 않아 회복도 멈춘다")]
        public void LowHp_PausesWhileDisabled()
        {
            var model = Model(new LowHpPassive(0.3f, 5f, healRatio: 0.5f));
            Hit(model, 75);

            model.ApplyFreeze(10f);
            model.TickPassives(5f);

            Assert.AreEqual(25, model.Hp);
        }

        [Test]
        public void LowHp_RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LowHpPassive(0f, 5f, healRatio: 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LowHpPassive(1f, 5f, healRatio: 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LowHpPassive(0.3f, 0f, healRatio: 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LowHpPassive(0.3f, 5f, healRatio: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LowHpPassive(0.3f, 5f, healRatio: 0.5f, speedMultiplier: 0f));
            Assert.Throws<ArgumentException>(() => new LowHpPassive(0.3f, 5f), "켜질 효과가 없다");
        }

        // ───────── 피격 시 회복·가속 ─────────

        [Test]
        public void HealOnHit_HealsAFractionOfMaxHp()
        {
            var model = Model(new HealOnHitPassive(0.1f));

            Hit(model, 30);

            Assert.AreEqual(80, model.Hp, "30을 맞고 최대 체력의 10%를 회복");
        }

        [Test]
        public void HealOnHit_HealsAtLeastOne()
        {
            var model = Model(new HealOnHitPassive(0.001f), maxHp: 100);

            Hit(model, 10);

            Assert.AreEqual(91, model.Hp);
        }

        [Test(Description = "속성을 정하면 그 속성의 적중에만 반응한다")]
        public void HealOnHit_ElementFilter()
        {
            var model = Model(new HealOnHitPassive(0.1f, element: Element.Earth));

            Hit(model, 30, Element.Fire);
            Assert.AreEqual(70, model.Hp);

            Hit(model, 30, Element.Earth);
            Assert.AreEqual(50, model.Hp, "토속성에 맞아 10 회복");
        }

        [Test(Description = "적중 피해에만 반응한다: 부가 피해와 상태이상 지속 피해는 반응하지 않는다")]
        public void HealOnHit_ReactsToImpactOnly()
        {
            var model = Model(new HealOnHitPassive(0.1f));

            Splash(model, 30);
            model.TakeDamage(10);

            Assert.AreEqual(60, model.Hp);
        }

        [Test]
        public void HealOnHit_RollsChance()
        {
            var model = Model(new HealOnHitPassive(0.1f, chance: 0.5f, random: new ScriptedRandom(0.9f, 0.1f)));

            Hit(model, 30); // 0.9 → 반응 없음
            Assert.AreEqual(70, model.Hp);

            Hit(model, 30); // 0.1 → 회복
            Assert.AreEqual(50, model.Hp);
        }

        [Test(Description = "맞으면 이속이 일정 시간 올라가고, 다시 맞으면 시간이 갱신된다")]
        public void HealOnHit_SpeedBoostRefreshes()
        {
            var model = Model(new HealOnHitPassive(0f, speedMultiplier: 2f, speedDuration: 2f), speed: 2f);

            Hit(model, 10);
            Assert.AreEqual(4f, model.MoveSpeed, 0.0001f);

            model.TickStatus(1.5f);
            Hit(model, 10); // 갱신
            model.TickStatus(1.5f);
            Assert.AreEqual(4f, model.MoveSpeed, 0.0001f, "갱신돼서 아직 남았다");

            model.TickStatus(1f);
            Assert.AreEqual(2f, model.MoveSpeed, 0.0001f);
        }

        [Test]
        public void HealOnHit_RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealOnHitPassive(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealOnHitPassive(0.1f, speedMultiplier: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealOnHitPassive(0.1f, speedMultiplier: 2f), "이속이 바뀌면 지속시간이 필요하다");
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealOnHitPassive(0.1f, chance: 0f, random: new ScriptedRandom(0f)));
            Assert.Throws<ArgumentNullException>(() => new HealOnHitPassive(0.1f, chance: 0.5f));
            Assert.Throws<ArgumentException>(() => new HealOnHitPassive(0f), "반응할 효과가 없다");
        }

        // ───────── 주기적 가속 ─────────

        [Test]
        public void SpeedBoost_TurnsOnEveryIntervalForTheDuration()
        {
            var model = Model(new SpeedBoostPassive(interval: 2f, duration: 1f, multiplier: 3f), speed: 2f);

            model.TickPassives(1.9f);
            Assert.AreEqual(2f, model.MoveSpeed, 0.0001f, "주기 전");

            model.TickPassives(0.1f);
            Assert.AreEqual(6f, model.MoveSpeed, 0.0001f, "가속");

            model.TickStatus(1f);
            Assert.AreEqual(2f, model.MoveSpeed, 0.0001f, "지속시간이 끝남");

            model.TickPassives(2f);
            Assert.AreEqual(6f, model.MoveSpeed, 0.0001f, "다음 주기에 다시");
        }

        [Test]
        public void SpeedBoost_PausesWhileDisabled()
        {
            var model = Model(new SpeedBoostPassive(2f, 1f, 3f), speed: 2f);

            model.ApplyFreeze(1f);
            model.TickPassives(10f);
            model.TickStatus(1f); // 빙결이 풀린다

            Assert.AreEqual(2f, model.MoveSpeed, 0.0001f, "빙결 중에는 시간이 흐르지 않았다");
        }

        [Test]
        public void SpeedBoost_RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpeedBoostPassive(0f, 1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpeedBoostPassive(2f, 0f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpeedBoostPassive(2f, 1f, 0f));
        }

        // ───────── 데이터 정의 ─────────

        private static MonsterDefinition Monster(params PassiveDefinition[] passives) =>
            new MonsterDefinition { Id = 7, Hp = 10, AttackInterval = 1f, Passives = new List<PassiveDefinition>(passives) };

        private static PassiveDefinition ShieldDef(string trigger = "Start", int hits = 3, float duration = 0f, float chance = 1f, params string[] statuses) =>
            new PassiveDefinition { Kind = PassiveKind.Shield, Trigger = trigger, Hits = hits, Duration = duration, Chance = chance, Statuses = statuses };

        [Test]
        public void Data_DefaultsAreSafe()
        {
            var definition = new PassiveDefinition();

            Assert.AreEqual(1f, definition.Chance);
            Assert.AreEqual(1f, definition.SpeedMultiplier);
        }

        [Test]
        public void Data_AcceptsValidDefinitions()
        {
            var monster = Monster(
                ShieldDef("OnHit", 6, 4f, 0.4f, "Stun", "Burn"),
                new PassiveDefinition { Kind = PassiveKind.Evade, Chance = 0.3f },
                new PassiveDefinition { Kind = PassiveKind.LowHp, HpRatio = 0.3f, Duration = 5f, HealRatio = 0.5f },
                new PassiveDefinition { Kind = PassiveKind.LowHp, HpRatio = 0.1f, Duration = 6f, Invulnerable = true, SpeedMultiplier = 2f, Statuses = new[] { "AllDebuffs" } },
                new PassiveDefinition { Kind = PassiveKind.HealOnHit, HealRatio = 0.05f, SpeedMultiplier = 1.5f, Duration = 3f, Element = "Earth" },
                new PassiveDefinition { Kind = PassiveKind.SpeedBoost, Interval = 6f, Duration = 2f, SpeedMultiplier = 3f });

            Assert.DoesNotThrow(() => monster.Validate());
        }

        [Test]
        public void Data_RejectsBadShield()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(ShieldDef("Always")).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(ShieldDef(hits: 0)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(ShieldDef(duration: -1f)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(ShieldDef("OnHit", chance: 0f)).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(ShieldDef(statuses: "Teleport")).Validate());
        }

        [Test]
        public void Data_RejectsBadEvadeAndSpeedBoost()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.Evade, Chance = 0f }).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.Evade, Chance = 1.5f }).Validate());
            Assert.Throws<InvalidOperationException>(() =>
                Monster(new PassiveDefinition { Kind = PassiveKind.SpeedBoost, Interval = 0f, Duration = 1f, SpeedMultiplier = 2f }).Validate());
            Assert.Throws<InvalidOperationException>(() =>
                Monster(new PassiveDefinition { Kind = PassiveKind.SpeedBoost, Interval = 2f, Duration = 1f, SpeedMultiplier = 0f }).Validate());
        }

        [Test]
        public void Data_RejectsBadLowHp()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.LowHp, HpRatio = 1f, Duration = 5f, HealRatio = 0.5f }).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.LowHp, HpRatio = 0.3f, Duration = 0f, HealRatio = 0.5f }).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.LowHp, HpRatio = 0.3f, Duration = 5f }).Validate(), "켜질 효과가 없다");
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.LowHp, HpRatio = 0.3f, Duration = 5f, HealRatio = -1f }).Validate());
        }

        [Test]
        public void Data_RejectsBadHealOnHit()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.HealOnHit }).Validate(), "반응할 효과가 없다");
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.HealOnHit, SpeedMultiplier = 2f }).Validate(), "이속이 바뀌면 Duration이 필요하다");
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.HealOnHit, HealRatio = 0.1f, Element = "fire" }).Validate(), "속성 이름은 대소문자를 구분한다");
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.HealOnHit, HealRatio = 0.1f, Element = "7" }).Validate());
            Assert.Throws<InvalidOperationException>(() => Monster(new PassiveDefinition { Kind = PassiveKind.HealOnHit, HealRatio = 0.1f, Chance = 0f }).Validate());
        }

        [Test]
        public void Builder_CreatesTheRightPassives()
        {
            var monster = Monster(
                ShieldDef(),
                new PassiveDefinition { Kind = PassiveKind.Evade, Chance = 0.3f },
                new PassiveDefinition { Kind = PassiveKind.LowHp, HpRatio = 0.3f, Duration = 5f, HealRatio = 0.5f },
                new PassiveDefinition { Kind = PassiveKind.HealOnHit, HealRatio = 0.05f },
                new PassiveDefinition { Kind = PassiveKind.SpeedBoost, Interval = 6f, Duration = 2f, SpeedMultiplier = 3f });

            var passives = PassiveBuilder.BuildPassives(monster, new ScriptedRandom(0f));

            Assert.AreEqual(5, passives.Count);
            Assert.IsInstanceOf<ShieldPassive>(passives[0]);
            Assert.IsInstanceOf<EvadePassive>(passives[1]);
            Assert.IsInstanceOf<LowHpPassive>(passives[2]);
            Assert.IsInstanceOf<HealOnHitPassive>(passives[3]);
            Assert.IsInstanceOf<SpeedBoostPassive>(passives[4]);
        }

        [Test(Description = "확률이 1보다 작은 패시브는 랜덤 없이 만들 수 없다")]
        public void Builder_RequiresRandomForChancePassives()
        {
            var evade = Monster(new PassiveDefinition { Kind = PassiveKind.Evade, Chance = 0.3f });

            Assert.Throws<ArgumentNullException>(() => PassiveBuilder.BuildPassives(evade));
            Assert.DoesNotThrow(() => PassiveBuilder.BuildPassives(evade, new ScriptedRandom(0f)));
            Assert.DoesNotThrow(() => PassiveBuilder.BuildPassives(Monster(ShieldDef())), "확률 1이면 랜덤이 필요 없다");
        }

        [Test(Description = "Shield·LowHp의 Statuses는 켜져 있는 동안만의 면역이라 처음부터 있는 면역에 합치지 않는다")]
        public void Builder_StaticImmunitiesIgnoreShieldStatuses()
        {
            var monster = Monster(
                ShieldDef(statuses: "Stun"),
                new PassiveDefinition { Kind = PassiveKind.Immunity, Statuses = new[] { "Burn" } });

            Assert.AreEqual(StatusImmunity.Burn, PassiveBuilder.BuildImmunities(monster));
        }

        [Test]
        public void Builder_StatusesAcceptTheNewNames()
        {
            var monster = Monster(new PassiveDefinition { Kind = PassiveKind.Immunity, Statuses = new[] { "Slow", "Frostbite", "Vulnerability" } });

            Assert.DoesNotThrow(() => monster.Validate());
            Assert.AreEqual(StatusImmunity.Slow | StatusImmunity.Frostbite | StatusImmunity.Vulnerability, PassiveBuilder.BuildImmunities(monster));
            Assert.AreEqual(StatusImmunity.AllDebuffs, new PassiveDefinition { Kind = PassiveKind.Immunity, Statuses = new[] { "AllDebuffs" } }.ToImmunities());
        }

        [Test]
        public void RealData_StillLoads()
        {
            Assert.DoesNotThrow(() => new GameDataStore());
        }
    }
}
