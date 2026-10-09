using System;
using System.Collections.Generic;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>공통 시전기가 공격 전략에 위임하는 방식과 전략 등록 검증</summary>
    public sealed class AttackStrategyTests
    {
        private sealed class FakeStrategy : IAttackStrategy
        {
            public int Fires, Ticks, Disposed;
            public readonly List<Vector3> Origins = new List<Vector3>();
            public readonly List<float> Damages = new List<float>();
            public void Fire(SkillConfig config, AttackEnvironment environment) { Fires++; Origins.Add(environment.Origin); Damages.Add(config.Stats.Cast.Damage); }
            public void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime) => Ticks++;
            public void Dispose() => Disposed++;
        }

        private static SkillData Data() => new SkillData
        {
            baseStats = new SkillBaseStats { cast = { cooldown = 1, baseDamage = 10, projectileCount = 1 } },
            maxLevel = 10
        };

        private sealed class PoseTarget : IEnemyTarget
        {
            public Vector2 Position { get; set; } = Vector2.up;
            public bool IsDead { get; set; }
            public void TakeDamage(int amount) { }
        }

        private sealed class PoseTargets : IEnemyTargetProvider
        {
            public readonly PoseTarget Target = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.Add(Target);
                return 1;
            }
        }

        [Test]
        public void FiredPose_RequiresLivingTargetInRangeAndSkipsChildCasts()
        {
            var go = new GameObject("CastPoseTest");
            var targets = new PoseTargets();
            var strategy = new FakeStrategy();
            var data = Data();
            data.baseStats.cast.range = 10;
            var caster = SkillFactory.Create(data, go.transform, targets, strategy);
            try
            {
                int poses = 0;
                caster.Fired += () => poses++;
                caster.Tick(.1f);
                Assert.AreEqual(1, poses);
                caster.FireAt(Vector3.zero);
                Assert.AreEqual(1, poses, "자식 공격은 발사 자세를 중복 재생하지 않는다");
                targets.Target.IsDead = true;
                caster.Tick(1f);
                targets.Target.IsDead = false;
                targets.Target.Position = Vector2.up * 100;
                caster.Tick(1f);
                Assert.AreEqual(1, poses, "죽었거나 범위 밖인 적에게는 자세를 재생하지 않는다");
                Assert.AreEqual(4, strategy.Fires, "전략의 기존 시전 주기는 유지한다");
                caster.Dispose();
                caster.Tick(1f);
                Assert.AreEqual(1, poses);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void SkillCaster_FiresThroughStrategyWhenCooldownElapses()
        {
            var go = new GameObject("StrategyCasterTest");
            try
            {
                var strategy = new FakeStrategy();
                var caster = SkillFactory.Create(Data(), go.transform, new NullEnemyTargetProvider(), strategy);
                caster.Tick(.1f);
                Assert.AreEqual(1, strategy.Ticks);
                Assert.AreEqual(1, strategy.Fires, "첫 틱에 쿨타임 0으로 시작해 한 번 발사한다");
                caster.Tick(.1f);
                Assert.AreEqual(1, strategy.Fires, "쿨타임 동안에는 발사하지 않는다");
                Assert.AreEqual(2, strategy.Ticks, "추가 처리는 매 프레임 호출된다");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void SkillCaster_DisposesStrategy()
        {
            var go = new GameObject("StrategyDisposeTest");
            try
            {
                var strategy = new FakeStrategy();
                var caster = SkillFactory.Create(Data(), go.transform, new NullEnemyTargetProvider(), strategy);
                caster.Dispose();
                Assert.AreEqual(1, strategy.Disposed);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FireAt_UsesGivenPositionAsOriginWithoutWaitingForCooldown()
        {
            var go = new GameObject("StrategyFireAtTest");
            go.transform.position = new Vector3(1, 2, 0);
            try
            {
                var strategy = new FakeStrategy();
                var caster = SkillFactory.Create(Data(), go.transform, new NullEnemyTargetProvider(), strategy);
                caster.FireAt(new Vector3(5, 6, 0));
                Assert.AreEqual(new Vector3(5, 6, 0), strategy.Origins[0]);
                caster.Tick(.1f);
                Assert.AreEqual(new Vector3(1, 2, 0), strategy.Origins[1], "일반 시전은 시전자 위치를 쓴다");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FireAt_AppliesDamageScaleForThisCastOnly()
        {
            var go = new GameObject("StrategyScaleTest");
            try
            {
                var strategy = new FakeStrategy();
                var caster = SkillFactory.Create(Data(), go.transform, new NullEnemyTargetProvider(), strategy);
                caster.FireAt(Vector3.zero, .5f);
                caster.FireAt(Vector3.zero);
                Assert.AreEqual(5, strategy.Damages[0], .001f);
                Assert.AreEqual(10, strategy.Damages[1], .001f, "배율은 그 시전에만 적용된다");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Factory_ThrowsClearErrorForUnregisteredCastType()
        {
            var go = new GameObject("StrategyUnregisteredTest");
            try
            {
                var data = Data();
                data.castType = (CastType)99;
                var error = Assert.Throws<NotSupportedException>(() =>
                    SkillFactory.Create(data, go, go.transform, new NullEnemyTargetProvider()));
                StringAssert.Contains("99", error.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Validator_RejectsCastTypeWithoutRegisteredStrategy()
        {
            var catalog = JsonConvert.DeserializeObject<List<SkillData>>(Resources.Load<TextAsset>("MockData/Skills").text);
            catalog[0].castType = (CastType)99;
            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains(catalog[0].name, error.Message);
        }

        // ── 자식 스킬 시전기 ─────────────────────────────────────────

        [Test]
        public void ChildSkillCaster_FiresChildAtEventPositionAndReusesIt()
        {
            var go = new GameObject("ChildCasterTest");
            try
            {
                var strategy = new FakeStrategy();
                int created = 0;
                var children = new ChildSkillCaster(id =>
                {
                    created++;
                    return SkillFactory.Create(Data(), go.transform, new NullEnemyTargetProvider(), strategy);
                });
                children.Cast(new ChildCast(7, 1f, null, null), new AttackContext(new Vector2(3, 4), Vector3.up));
                children.Cast(new ChildCast(7, 1f, null, null), new AttackContext(new Vector2(5, 6), Vector3.up));
                Assert.AreEqual(1, created, "자식은 한 번만 만든다");
                Assert.AreEqual(2, strategy.Fires, "요청마다 쿨타임 없이 시전한다");
                Assert.AreEqual(new Vector3(3, 4, 0), strategy.Origins[0]);
                Assert.AreEqual(new Vector3(5, 6, 0), strategy.Origins[1]);
                children.Dispose();
                Assert.AreEqual(1, strategy.Disposed);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ChildSkillCaster_SkipsWhenChildCannotBeCreated()
        {
            int created = 0;
            var children = new ChildSkillCaster(_ => { created++; return null; });
            Assert.DoesNotThrow(() =>
            {
                children.Cast(new ChildCast(7, 1f, null, null), new AttackContext(Vector2.zero, Vector3.up));
                children.Cast(new ChildCast(7, 1f, null, null), new AttackContext(Vector2.zero, Vector3.up));
            });
            Assert.AreEqual(1, created, "만들지 못한 자식도 반복해서 다시 만들지 않는다");
        }

        [Test]
        public void ChildOnlySkill_IsNeverOfferedAsNewSkillCard()
        {
            var normal = new SkillData { id = 1, name = "일반", baseStats = new SkillBaseStats(), maxLevel = 5, upgrades = new List<SkillUpgradeOption>() };
            var child = new SkillData { id = 2, name = "자식", baseStats = new SkillBaseStats(), maxLevel = 5, upgrades = new List<SkillUpgradeOption>(), childOnly = true };
            var choices = new SkillUpgradeChoices(new List<SkillBase>(), new[] { normal, child }, new EmptyState(), _ => true);
            var offered = choices.Select(5, upper => 0);
            Assert.AreEqual(1, offered.Count);
            Assert.AreEqual(1, offered[0].newSkillData.id);
        }

        private sealed class EmptyState : IUpgradeState
        {
            public int GetWeaponLevel(int weaponId) => 0;
            public int GetPermanentWeaponLevel(int weaponId) => 0;
            public int GetAcquiredCount(int weaponId, string cardId) => 0;
        }
    }
}
