using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>자식 스킬 조준: 가장 가까운 적부터 서로 다른 적에게 나눠 쏘고, 필요하면 방금 맞은 적은 피한다</summary>
    public sealed class ChildAimingTests
    {
        private const int ChildId = 2;
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private sealed class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new List<IEnemyTarget>();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.AddRange(Targets.OrderBy(t => (t.Position - from).sqrMagnitude).Take(count));
                return results.Count;
            }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private Provider provider;
        private ChildSkillCaster children;

        [SetUp]
        public void SetUp()
        {
            provider = new Provider();
            var prefab = new GameObject("ChildAimingPrefab");
            prefab.AddComponent<Projectile>();
            created.Add(prefab);
            var owner = new GameObject("ChildAimingOwner");
            created.Add(owner);
            var childData = new SkillData
            {
                id = ChildId,
                name = "자식",
                maxLevel = 5,
                upgrades = new List<SkillUpgradeOption>(),
                baseStats = new SkillBaseStats { cast = { baseDamage = 5, cooldown = 1, range = 20, projectileCount = 1 }, projectile = { speed = 10 } }
            };
            children = new ChildSkillCaster(_ => Casters.Projectile(childData, prefab, owner.transform, provider));
        }

        [TearDown]
        public void TearDown()
        {
            children.Dispose();
            foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) { Object.DestroyImmediate(projectile.gameObject); }
            foreach (var go in created) { if (go != null) { Object.DestroyImmediate(go); } }
        }

        private SkillConfig ParentWithLink(int count, bool excludeHit)
        {
            var parent = SkillConfig.FromDefinition(new SkillData { baseStats = new SkillBaseStats { cast = { baseDamage = 100 } } }).WithChildCaster(children);
            var builder = new SkillConfigBuilder(parent);
            Assert.IsTrue(builder.TryApplyCatalog(new[]
            {
                new EffectDef { kind = "onEvent", trigger = AttackEvent.Hit, skillId = ChildId, count = count, excludeHit = excludeHit }
            }));
            return builder.Build();
        }

        private static HitRecorder Enemy(float x, float y) => new HitRecorder { Position = new Vector2(x, y) };

        private static List<Projectile> Active() => Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
            .Where(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.gameObject.name.Contains("Clone")).ToList();

        private static Vector3 Direction(Projectile p) => (Vector3)typeof(Projectile).GetField("direction", Flags).GetValue(p);

        private static IEnemyTarget Ignored(Projectile p) => (IEnemyTarget)typeof(Projectile).GetField("ignoredTarget", Flags).GetValue(p);

        private static HitRecorder Aimed(Projectile p, Vector2 origin, IEnumerable<HitRecorder> enemies) =>
            enemies.OrderBy(e => Vector2.Angle(Direction(p), e.Position - origin)).First();

        [Test]
        public void ThreeShots_GoToThreeDifferentEnemies()
        {
            var hit = Enemy(0, 0);
            var a = Enemy(3, 0); var b = Enemy(0, 4); var c = Enemy(-5, 0);
            provider.Targets.AddRange(new IEnemyTarget[] { hit, a, b, c });
            ParentWithLink(3, excludeHit: true).Reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up, hit));

            var shots = Active();
            Assert.AreEqual(3, shots.Count);
            var aimed = shots.Select(p => Aimed(p, Vector2.zero, new[] { a, b, c })).Distinct().Count();
            Assert.AreEqual(3, aimed, "세 발이 서로 다른 적을 향한다");
        }

        [Test]
        public void MoreShotsThanEnemies_SpreadsRoundRobinAcrossAll()
        {
            var hit = Enemy(0, 0);
            var a = Enemy(3, 0); var b = Enemy(0, 4);
            provider.Targets.AddRange(new IEnemyTarget[] { hit, a, b });
            ParentWithLink(3, excludeHit: true).Reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up, hit));

            var shots = Active();
            Assert.AreEqual(3, shots.Count);
            var counts = shots.GroupBy(p => Aimed(p, Vector2.zero, new[] { a, b })).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(2, counts.Count, "두 적 모두 노린다");
            Assert.AreEqual(3, counts.Values.Sum());
            Assert.LessOrEqual(counts.Values.Max(), 2, "한 적에게 모두 몰리지 않는다");
        }

        [Test]
        public void ExcludeHit_SkipsTheEnemyThatWasJustHitAndMarksItIgnored()
        {
            var hit = Enemy(1, 0);
            var other = Enemy(0, 5);
            provider.Targets.AddRange(new IEnemyTarget[] { hit, other });
            ParentWithLink(2, excludeHit: true).Reactions.Raise(AttackEvent.Hit, new AttackContext(hit.Position, Vector3.up, hit));

            var shots = Active();
            Assert.AreEqual(2, shots.Count);
            foreach (var shot in shots)
            {
                Vector2 expected = (other.Position - hit.Position).normalized;
                Assert.AreEqual(0f, Vector2.Angle(Direction(shot), expected), .5f, "맞은 적이 아니라 다른 적을 향한다");
                Assert.AreSame(hit, Ignored(shot), "맞은 적은 다시 맞지 않고 지나친다");
            }
        }

        [Test]
        public void ExcludeHit_FansOutAroundTheIncomingDirectionWhenTheHitEnemyIsTheOnlyOne()
        {
            var hit = Enemy(1, 0);
            provider.Targets.Add(hit);
            var incoming = Vector3.right;
            ParentWithLink(3, excludeHit: true).Reactions.Raise(AttackEvent.Hit, new AttackContext(hit.Position, incoming, hit));

            var shots = Active();
            Assert.AreEqual(3, shots.Count, "노릴 다른 적이 없어도 쏜다");
            var angles = shots.Select(p => Vector3.SignedAngle(incoming, Direction(p), Vector3.forward)).OrderBy(a => a).ToList();
            Assert.AreEqual(-30f, angles[0], .5f);
            Assert.AreEqual(0f, angles[1], .5f);
            Assert.AreEqual(30f, angles[2], .5f);
            foreach (var shot in shots) { Assert.AreSame(hit, Ignored(shot), "맞은 적은 지나친다"); }
        }

        [Test]
        public void ExcludeHit_SingleShotGoesStraightAheadWhenNoOtherEnemy()
        {
            var hit = Enemy(1, 0);
            provider.Targets.Add(hit);
            ParentWithLink(1, excludeHit: true).Reactions.Raise(AttackEvent.Hit, new AttackContext(hit.Position, Vector3.up, hit));
            var shots = Active();
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.up, Direction(shots[0])), .5f);
        }

        [Test]
        public void ExcludeHit_FiresNothingWhenThereIsNoOtherEnemyAndNoIncomingDirection()
        {
            var hit = Enemy(1, 0);
            provider.Targets.Add(hit);
            ParentWithLink(3, excludeHit: true).Reactions.Raise(AttackEvent.Hit, new AttackContext(hit.Position, Vector3.zero, hit));
            Assert.AreEqual(0, Active().Count, "방향도 없으면 어디로 쏠지 알 수 없다");
        }

        [Test]
        public void OtherEnemiesPresent_AimAtThemInsteadOfFanning()
        {
            var hit = Enemy(0, 0);
            var other = Enemy(0, 5);
            provider.Targets.AddRange(new IEnemyTarget[] { hit, other });
            ParentWithLink(3, excludeHit: true).Reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.right, hit));
            foreach (var shot in Active()) { Assert.AreEqual(0f, Vector3.Angle(Direction(shot), Vector3.up), .5f, "다른 적이 있으면 그 적을 노린다"); }
        }

        [Test]
        public void WithoutExcludeHit_TheHitEnemyCanBeAimedAt()
        {
            var hit = Enemy(1, 0);
            provider.Targets.Add(hit);
            ParentWithLink(1, excludeHit: false).Reactions.Raise(AttackEvent.Hit, new AttackContext(hit.Position, Vector3.up, hit));
            Assert.AreEqual(1, Active().Count);
        }

    }
}
