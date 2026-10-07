using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    // 패시브 훅(OnSpawn, OnBeforeDamage, OnDamaged), 회복, 출처별 동적 면역, 이속 배율
    public sealed class DamageHookTests
    {
        private static readonly EnemyAttackStats Attack = new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f);

        private sealed class Recorder<T> : IPublisher<T>
        {
            public readonly List<T> Messages = new();
            public void Publish(T message) => Messages.Add(message);
        }

        // 훅 호출을 기록하고 동작을 바꿔 끼울 수 있는 패시브
        private sealed class Probe : IPassive
        {
            public Func<DamageInfo, int, int> BeforeDamage = (info, amount) => amount;
            public readonly List<(DamageInfo Info, int Amount)> BeforeCalls = new();
            public readonly List<(DamageInfo Info, int Applied)> DamagedCalls = new();
            public int SpawnCalls;
            public int DiedCalls;
            public float SpawnHpRatio = -1f;

            public void OnSpawn(EnemyModel self)
            {
                SpawnCalls++;
                SpawnHpRatio = self.HpRatio;
            }

            public void OnDied(EnemyModel self) => DiedCalls++;

            public int OnBeforeDamage(EnemyModel self, DamageInfo info, int amount)
            {
                BeforeCalls.Add((info, amount));
                return BeforeDamage(info, amount);
            }

            public void OnDamaged(EnemyModel self, DamageInfo info, int appliedDamage) => DamagedCalls.Add((info, appliedDamage));
        }

        private static EnemyModel Model(int maxHp = 100, float speed = 2f, IReadOnlyList<IPassive> passives = null,
            Recorder<EnemyHpChanged> hp = null, DamageProfile profile = null, StatusImmunity immunities = StatusImmunity.None) =>
            new EnemyModel(1, speed, EnemyType.Normal, maxHp, Attack, hp ?? new Recorder<EnemyHpChanged>(), new Recorder<EnemyDied>(),
                passives, immunities: immunities, damageProfile: profile);

        private static DamageInfo Skill(int amount, Element element = Element.Neutral, bool impact = true) =>
            new DamageInfo(amount, 1, element, CastType.Projectile, impact);

        // ───────── 시작 훅 ─────────

        [Test]
        public void OnSpawn_IsCalledOnceWithTheFinishedModel()
        {
            var probe = new Probe();

            var model = Model(passives: new IPassive[] { probe });

            Assert.AreEqual(1, probe.SpawnCalls);
            Assert.AreEqual(1f, probe.SpawnHpRatio, "생성이 끝난 모델(체력이 정해진 뒤)을 받는다");
            Assert.AreEqual(100, model.Hp);
        }

        // ───────── 피해 전 훅 ─────────

        [Test]
        public void BeforeDamage_CanBlockDamage()
        {
            var hp = new Recorder<EnemyHpChanged>();
            var probe = new Probe { BeforeDamage = (info, amount) => 0 };
            var model = Model(passives: new IPassive[] { probe }, hp: hp);

            model.TakeDamage(Skill(30));

            Assert.AreEqual(100, model.Hp);
            Assert.IsEmpty(hp.Messages, "막힌 피해는 체력 변화 메시지도 내지 않는다");
            Assert.IsEmpty(probe.DamagedCalls, "막힌 피해는 피격 후 훅도 부르지 않는다");
        }

        [Test]
        public void BeforeDamage_CanReduceDamage()
        {
            var probe = new Probe { BeforeDamage = (info, amount) => amount / 2 };
            var model = Model(passives: new IPassive[] { probe });

            model.TakeDamage(Skill(30));

            Assert.AreEqual(85, model.Hp);
        }

        [Test(Description = "훅은 속성·시전 형태 저항(DamageProfile)보다 먼저 피해를 줄인다")]
        public void BeforeDamage_RunsBeforeDamageProfile()
        {
            var monster = new MonsterDefinition { Id = 7, Hp = 100, AttackInterval = 1f, Resists = new Dictionary<string, float> { { "Fire", 0.5f } } };
            var probe = new Probe { BeforeDamage = (info, amount) => 10 };
            var model = Model(passives: new IPassive[] { probe }, profile: DamageProfile.From(monster));

            model.TakeDamage(Skill(20, Element.Fire)); // 훅 20 → 10, 약점 +50% → 15

            Assert.AreEqual(85, model.Hp);
            Assert.AreEqual(20, probe.BeforeCalls[0].Amount, "훅은 원래 피해량을 받는다");
        }

        [Test(Description = "패시브가 여럿이면 목록 순서대로 이어서 줄인다")]
        public void BeforeDamage_ChainsInListOrder()
        {
            var first = new Probe { BeforeDamage = (info, amount) => amount - 5 };
            var second = new Probe { BeforeDamage = (info, amount) => amount * 2 };
            var model = Model(passives: new IPassive[] { first, second });

            model.TakeDamage(Skill(20)); // (20-5) * 2

            Assert.AreEqual(70, model.Hp);
            Assert.AreEqual(15, second.BeforeCalls[0].Amount);
        }

        [Test(Description = "앞의 패시브가 막으면 뒤의 패시브는 부르지 않는다")]
        public void BeforeDamage_StopsAtTheFirstBlock()
        {
            var blocker = new Probe { BeforeDamage = (info, amount) => 0 };
            var after = new Probe();
            var model = Model(passives: new IPassive[] { blocker, after });

            model.TakeDamage(Skill(20));

            Assert.IsEmpty(after.BeforeCalls);
        }

        [Test(Description = "훅은 피해 정보를 받고, 상태이상 지속 피해(스킬 밖)도 불린다")]
        public void BeforeDamage_ReceivesInfoIncludingNonSkillDamage()
        {
            var probe = new Probe();
            var model = Model(passives: new IPassive[] { probe });

            model.TakeDamage(Skill(10, Element.Ice, impact: false));
            model.TakeDamage(5);

            Assert.AreEqual(Element.Ice, probe.BeforeCalls[0].Info.Element);
            Assert.IsFalse(probe.BeforeCalls[0].Info.IsImpact);
            Assert.IsTrue(probe.BeforeCalls[0].Info.IsFromSkill);
            Assert.IsFalse(probe.BeforeCalls[1].Info.IsFromSkill);
        }

        // ───────── 피해 후 훅 ─────────

        [Test]
        public void OnDamaged_ReceivesAppliedDamageAfterSurviving()
        {
            var probe = new Probe();
            var model = Model(passives: new IPassive[] { probe });
            model.ApplyVulnerability(0.5f, 5f);

            model.TakeDamage(Skill(20)); // 취약 +50% → 30

            Assert.AreEqual(1, probe.DamagedCalls.Count);
            Assert.AreEqual(30, probe.DamagedCalls[0].Applied);
            Assert.AreEqual(70, model.Hp);
        }

        [Test(Description = "죽으면 피격 후 훅 대신 사망 훅이 불린다")]
        public void OnDamaged_NotCalledWhenTheHitKills()
        {
            var probe = new Probe();
            var model = Model(maxHp: 10, passives: new IPassive[] { probe });

            model.TakeDamage(Skill(50));

            Assert.IsEmpty(probe.DamagedCalls);
            Assert.AreEqual(1, probe.DiedCalls);
        }

        // ───────── 회복 ─────────

        [Test]
        public void Heal_RestoresHpUpToMaxAndPublishes()
        {
            var hp = new Recorder<EnemyHpChanged>();
            var model = Model(hp: hp);
            model.TakeDamage(60);
            hp.Messages.Clear();

            model.Heal(20);
            model.Heal(100);

            Assert.AreEqual(100, model.Hp);
            Assert.AreEqual(2, hp.Messages.Count);
            Assert.AreEqual(60, hp.Messages[0].Current);
            Assert.AreEqual(100, hp.Messages[1].Current);
        }

        [Test]
        public void Heal_IgnoresInvalidOrUselessRequests()
        {
            var hp = new Recorder<EnemyHpChanged>();
            var model = Model(maxHp: 10, hp: hp);

            model.Heal(5); // 이미 가득
            model.TakeDamage(3);
            hp.Messages.Clear();
            model.Heal(0);
            model.Heal(-4);
            model.TakeDamage(100);
            hp.Messages.Clear();
            model.Heal(5); // 죽었다

            Assert.IsEmpty(hp.Messages);
            Assert.IsTrue(model.IsDead);
        }

        [Test]
        public void HpRatio_FollowsHp()
        {
            var model = Model(maxHp: 200);

            model.TakeDamage(50);

            Assert.AreEqual(0.75f, model.HpRatio, 0.0001f);
        }

        // ───────── 출처별 동적 면역 ─────────

        [Test]
        public void GrantImmunity_BlocksOnlyTheGrantedStatuses_UntilRevoked()
        {
            var model = Model();
            var source = new object();

            model.GrantImmunity(source, StatusImmunity.Stun | StatusImmunity.Burn);
            model.ApplyStun(3f);
            model.ApplyBurn(5f, 3f);
            model.ApplyFreeze(3f);

            Assert.IsFalse(model.IsStunned);
            Assert.AreEqual(0f, model.BurnRemaining);
            Assert.IsTrue(model.IsFrozen, "걸지 않은 상태이상은 걸린다");
            Assert.IsTrue(model.IsImmuneTo(StatusImmunity.Stun));

            model.RevokeImmunity(source);
            model.ApplyStun(3f);

            Assert.IsTrue(model.IsStunned);
            Assert.IsFalse(model.IsImmuneTo(StatusImmunity.Burn));
        }

        [Test(Description = "시간제 면역은 시간이 지나면 풀린다")]
        public void GrantImmunity_WithDuration_Expires()
        {
            var model = Model();

            model.GrantImmunity(new object(), StatusImmunity.Stun, 2f);
            model.TickStatus(1f);
            Assert.IsTrue(model.IsImmuneTo(StatusImmunity.Stun));

            model.TickStatus(1f);
            Assert.IsFalse(model.IsImmuneTo(StatusImmunity.Stun));
        }

        [Test(Description = "같은 출처가 다시 걸면 바뀌고, 출처가 다르면 따로 유지된다")]
        public void GrantImmunity_ReplacesPerSource()
        {
            var model = Model();
            var a = new object();
            var b = new object();

            model.GrantImmunity(a, StatusImmunity.Stun);
            model.GrantImmunity(b, StatusImmunity.Burn);
            model.GrantImmunity(a, StatusImmunity.Freeze);

            Assert.IsFalse(model.IsImmuneTo(StatusImmunity.Stun), "a의 옛 면역은 바뀌었다");
            Assert.IsTrue(model.IsImmuneTo(StatusImmunity.Freeze));
            Assert.IsTrue(model.IsImmuneTo(StatusImmunity.Burn));

            model.RevokeImmunity(a);

            Assert.IsFalse(model.IsImmuneTo(StatusImmunity.Freeze));
            Assert.IsTrue(model.IsImmuneTo(StatusImmunity.Burn), "b는 그대로");
        }

        [Test(Description = "동적 면역은 처음부터 있던 면역과 합쳐서 본다")]
        public void GrantImmunity_AddsToStaticImmunity()
        {
            var model = Model(immunities: StatusImmunity.Stun);

            model.GrantImmunity(new object(), StatusImmunity.Burn);

            Assert.IsTrue(model.IsImmuneTo(StatusImmunity.Stun));
            Assert.IsTrue(model.IsImmuneTo(StatusImmunity.Burn));
        }

        [Test]
        public void GrantImmunity_IgnoresInvalidRequests()
        {
            var model = Model();

            model.GrantImmunity(null, StatusImmunity.Stun);
            model.GrantImmunity(new object(), StatusImmunity.None);
            model.GrantImmunity(new object(), StatusImmunity.Stun, -1f);
            model.GrantImmunity(new object(), StatusImmunity.Stun, float.NaN);

            Assert.IsFalse(model.IsImmuneTo(StatusImmunity.Stun));
        }

        // ───────── 새 면역 플래그 ─────────

        [Test]
        public void SlowFrostbiteVulnerabilityImmunity_BlockTheirStatuses()
        {
            var slowImmune = Model(immunities: StatusImmunity.Slow);
            slowImmune.ApplySlow(0.5f, 3f);
            slowImmune.SetAreaSlow(this, 0.5f);

            var frostbiteImmune = Model(immunities: StatusImmunity.Frostbite);
            frostbiteImmune.ApplyFrostbite(5f);

            var vulnerabilityImmune = Model(immunities: StatusImmunity.Vulnerability);
            vulnerabilityImmune.ApplyVulnerability(0.5f, 3f);

            Assert.AreEqual(1f, slowImmune.MovementMultiplier);
            Assert.AreEqual(0f, slowImmune.SlowRemaining);
            Assert.AreEqual(0, frostbiteImmune.FrostbiteStacks);
            Assert.AreEqual(0f, vulnerabilityImmune.VulnerabilityRemaining);
        }

        [Test]
        public void AllDebuffs_BlocksEveryStatus()
        {
            var model = Model(immunities: StatusImmunity.AllDebuffs);

            model.ApplyStun(3f);
            model.ApplyBurn(5f, 3f);
            model.ApplyParalysis(3f);
            model.ApplyFreeze(3f);
            model.ApplySlow(0.5f, 3f);
            model.ApplyFrostbite(5f);
            model.ApplyVulnerability(0.5f, 3f);

            Assert.IsFalse(model.IsStunned || model.IsParalyzed || model.IsFrozen);
            Assert.AreEqual(0f, model.BurnRemaining + model.SlowRemaining + model.VulnerabilityRemaining);
            Assert.AreEqual(0, model.FrostbiteStacks);
        }

        // ───────── 이속 배율 ─────────

        [Test]
        public void SpeedBoost_MultipliesMoveSpeed_AlongsideSlow()
        {
            var model = Model(speed: 2f);
            var source = new object();

            model.AddSpeedBoost(source, 3f);
            Assert.AreEqual(6f, model.MoveSpeed, 0.0001f);

            model.ApplySlow(0.5f, 5f); // 감속과 따로 곱해진다
            Assert.AreEqual(3f, model.MoveSpeed, 0.0001f);

            model.RemoveSpeedBoost(source);
            Assert.AreEqual(1f, model.MoveSpeed, 0.0001f);
        }

        [Test]
        public void SpeedBoost_WithDuration_Expires()
        {
            var model = Model(speed: 2f);

            model.AddSpeedBoost(new object(), 2f, 1f);
            Assert.AreEqual(4f, model.MoveSpeed, 0.0001f);

            model.TickStatus(1f);
            Assert.AreEqual(2f, model.MoveSpeed, 0.0001f);
        }

        [Test(Description = "같은 출처는 갱신, 다른 출처끼리는 곱한다")]
        public void SpeedBoost_ReplacesPerSource_AndMultipliesAcrossSources()
        {
            var model = Model(speed: 1f);
            var a = new object();
            var b = new object();

            model.AddSpeedBoost(a, 2f);
            model.AddSpeedBoost(b, 3f);
            Assert.AreEqual(6f, model.MoveSpeed, 0.0001f);

            model.AddSpeedBoost(a, 4f);
            Assert.AreEqual(12f, model.MoveSpeed, 0.0001f);
        }

        [Test]
        public void SpeedBoost_IgnoresInvalidRequests_AndStopsWhileDisabled()
        {
            var model = Model(speed: 2f);

            model.AddSpeedBoost(null, 2f);
            model.AddSpeedBoost(new object(), 0f);
            model.AddSpeedBoost(new object(), -1f);
            model.AddSpeedBoost(new object(), float.NaN);
            Assert.AreEqual(2f, model.MoveSpeed, 0.0001f);

            model.AddSpeedBoost(new object(), 3f);
            model.ApplyFreeze(2f);
            Assert.AreEqual(0f, model.MoveSpeed, "빙결 중에는 가속해도 못 움직인다");
        }
    }
}
