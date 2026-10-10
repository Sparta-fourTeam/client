using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class SkillLifecycleTests
    {
        private sealed class StartingSkills : IStartingSkills
        {
            public IReadOnlyList<int> GetSkillIds() => new[] { 1, 2 };
        }

        private sealed class SkillPublisher : IBufferedPublisher<SkillChanged>
        {
            public SkillChanged Last;
            public void Publish(SkillChanged message) => Last = message;
        }

        [UnityTest]
        public IEnumerator Start_UsesInjectedSkillsAndPublishesInitialSnapshot()
        {
            var root = new GameObject("StartingSkillsTest");
            root.SetActive(false);
            var prefabs = new List<GameObject>();
            SkillAssetTable assets = null;
            try
            {
                var provider = new DefaultSkillDataProvider(new GameDataStore());
                var entries = new List<SkillAssetEntry>();
                foreach (int id in new[] { 1, 2, 3 })
                {
                    var prefab = new GameObject("StartingSkillPrefab" + id);
                    prefab.SetActive(false);
                    prefabs.Add(prefab);
                    var data = provider.LoadAll().Find(s => s.id == id);
                    entries.Add(new SkillAssetEntry { key = data.assetKey, prefab = prefab });
                }
                assets = SkillAssetTable.Create(entries);
                var controller = root.AddComponent<SkillController>();
                typeof(SkillController).GetField("assets", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, assets);
                var publisher = new SkillPublisher();
                controller.Construct(new NullEnemyTargetProvider(), publisher, provider, startingSkills: new StartingSkills());
                root.SetActive(true);
                float deadline = Time.realtimeSinceStartup + 3f;
                while (!controller.IsReady)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "실제 Start가 완료되지 않았다");
                    yield return null;
                }
                controller.enabled = false;
                Assert.That(controller.GetWeaponLevel(1), Is.EqualTo(1));
                Assert.That(controller.GetWeaponLevel(2), Is.EqualTo(1));
                Assert.That(controller.GetWeaponLevel(3), Is.Zero, "기본 시작 스킬이 섞이면 안 된다");
                Assert.That(publisher.Last.Skills.Count, Is.EqualTo(2));
                CollectionAssert.AreEquivalent(new[] { 1, 2 }, new[] { publisher.Last.Skills[0].Id, publisher.Last.Skills[1].Id });
                foreach (var skill in publisher.Last.Skills)
                {
                    Assert.That(skill.Level, Is.EqualTo(1));
                    Assert.That(skill.AssetKey, Is.Not.Empty);
                }
            }
            finally
            {
                Object.Destroy(root);
                foreach (var prefab in prefabs) { Object.Destroy(prefab); }
                if (assets != null) { Object.Destroy(assets); }
            }
            yield return null;
        }

        private sealed class SlowTarget : IEnemyTarget, IAreaSlowTarget
        {
            public Vector2 Position { get; set; }
            public readonly Dictionary<object, float> Slows = new();
            public void TakeDamage(int damage) { }
            public void SetAreaSlow(object source, float ratio) => Slows[source] = ratio;
            public void RemoveAreaSlow(object source) => Slows.Remove(source);
        }

        private sealed class SlowTargets : IEnemyTargetProvider
        {
            public SlowTarget Target;
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.Add(Target);
                return 1;
            }
        }

        private static void TickField(ElectromagneticField field) =>
            typeof(ElectromagneticField).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(field, new object[] { 0f });

        [UnityTest]
        public IEnumerator Field_EntryExitOverlapAndDisableRemoveOnlyOwnSlow()
        {
            var firstGo = new GameObject("FieldFirstTest");
            var secondGo = new GameObject("FieldSecondTest");
            try
            {
                var first = firstGo.AddComponent<ElectromagneticField>();
                var second = secondGo.AddComponent<ElectromagneticField>();
                var target = new SlowTarget { Position = Vector2.right * 2 };
                var provider = new SlowTargets { Target = target };
                var poolA = new ObjectPool<ElectromagneticField>(() => first);
                var poolB = new ObjectPool<ElectromagneticField>(() => second);
                poolA.Get().Init(poolA, provider, Vector2.zero, 1, 10, 3, .3f);
                poolB.Get().Init(poolB, provider, Vector2.zero, 1, 10, 5, .6f);
                target.Position = Vector2.zero;
                TickField(first);
                TickField(second);
                Assert.AreEqual(2, target.Slows.Count);

                firstGo.SetActive(false);
                yield return null;
                Assert.AreEqual(1, target.Slows.Count);
                Assert.IsTrue(target.Slows.ContainsKey(second));
                target.Position = Vector2.right * 2;
                TickField(second);
                Assert.AreEqual(0, target.Slows.Count);
                target.Position = Vector2.zero;
                TickField(second);
                Assert.AreEqual(1, target.Slows.Count);

                Object.Destroy(secondGo);
                yield return null;
                Assert.IsTrue(secondGo == null);
                Assert.AreEqual(0, target.Slows.Count);
            }
            finally
            {
                if (firstGo != null) { Object.Destroy(firstGo); }
                if (secondGo != null) { Object.Destroy(secondGo); }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispose_DestroysInactiveAndInFlightEffects_AndIsRepeatable()
        {
            foreach (bool hitscan in new[] { false, true })
            {
                var prefab = new GameObject("PoolLifecyclePrefab");
                prefab.SetActive(false);
                SkillCaster weapon = null;
                GameObject active = null, inactive = null;
                try
                {
                    var data = new SkillData { baseStats = new SkillBaseStats { cast = { cooldown = 1, baseDamage = 10, projectileCount = 1 } }, maxLevel = 10 };
                    IAttackStrategy strategy;
                    if (hitscan)
                    {
                        prefab.AddComponent<HitscanEffect>();
                        var hitscanStrategy = new HitscanStrategy(prefab);
                        strategy = hitscanStrategy;
                        var first = hitscanStrategy.Pool.Get();
                        var second = hitscanStrategy.Pool.Get();
                        active = first.gameObject;
                        inactive = second.gameObject;
                        hitscanStrategy.Pool.Release(second);
                    }
                    else
                    {
                        prefab.AddComponent<Projectile>();
                        var projectileStrategy = new ProjectileStrategy(prefab);
                        strategy = projectileStrategy;
                        var first = projectileStrategy.Pool.Get();
                        var second = projectileStrategy.Pool.Get();
                        active = first.gameObject;
                        inactive = second.gameObject;
                        projectileStrategy.Pool.Release(second);
                    }
                    weapon = SkillFactory.Create(data, prefab.transform, new NullEnemyTargetProvider(), strategy);
                    weapon.Dispose();
                    weapon.Dispose();
                    weapon.Tick(1);
                    yield return null;
                    Assert.IsTrue(active == null, "In-flight effect must be destroyed: " + hitscan);
                    Assert.IsTrue(inactive == null, "Idle pooled effect must be destroyed: " + hitscan);
                }
                finally
                {
                    weapon?.Dispose();
                    if (active != null) { Object.Destroy(active); }
                    if (inactive != null) { Object.Destroy(inactive); }
                    Object.Destroy(prefab);
                }
                yield return null;
            }
        }
    }
}
