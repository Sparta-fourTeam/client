using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>선언된 자식 연결의 피해·상태 상속, 비재귀 분할, 카드 획득 순서 독립성을 검증한다.</summary>
    public sealed class IceSplitMigrationTests
    {
        private const int IceId = 4;
        private CatalogWorld world;
        private EffectDef Effect(string card, string kind, int target = 0) => world.Data[IceId].upgrades.Find(c => c.id == card).effects.First(e => e.kind == kind && e.target == target);

        [SetUp]
        public void SetUp() => world = new CatalogWorld(data => data[IceId].baseStats.cast.baseDamage = 100);

        [TearDown]
        public void TearDown() => world.Dispose();

        private static float Aim(Projectile shot, Vector2 origin, HitRecorder enemy) =>
            Vector2.Angle(CatalogWorld.Direction(shot), enemy.Position - origin);

        [Test]
        public void SplitCard_ShootsThreeShardsAtOtherEnemiesAtHalfDamageTimesShardBonus()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "56");
            var parentDamage = ice.Stats.Cast.Damage;
            world.Take(ice, "57");
            var hit = world.AddEnemy(0, 0);
            var a = world.AddEnemy(4, 0); var b = world.AddEnemy(0, 5); var c = world.AddEnemy(-6, 0);

            CatalogWorld.Hit(ice.Config.Reactions, hit);

            var shards = CatalogWorld.Active();
            Assert.AreEqual(Effect("56", "onEvent").count, shards.Count);
            foreach (var shard in shards)
            {
                Assert.AreEqual((int)(parentDamage * Effect("56", "onEvent").inherit["damage"] * (1 + Effect("57", "damage", 8).value / 100)), HitRecorder.Hit(shard).Damage);
                Assert.AreSame(hit, typeof(Projectile).GetField("ignoredTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(shard));
            }
            var aimed = shards.Select(s => new[] { a, b, c }.OrderBy(e => Aim(s, Vector2.zero, e)).First()).Distinct().Count();
            Assert.AreEqual(System.Math.Min(3, shards.Count), aimed, "발 수만큼 서로 다른 적을 우선한다");
            Assert.AreEqual(parentDamage, ice.Stats.Cast.Damage, "조각 피해 강화는 본체에 적용하지 않는다");
        }

        [Test]
        public void ShardsDoNotSplitAgain()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "56");
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(4, 0);
            CatalogWorld.Hit(ice.Config.Reactions, hit);
            var shard = CatalogWorld.Active().First();
            var before = CatalogWorld.Active().Count;
            CatalogWorld.Hit(CatalogWorld.ReactionsOf(shard), world.Provider.All[1]);
            Assert.AreEqual(before, CatalogWorld.Active().Count, "조각이 맞아도 더 쪼개지지 않는다");
        }

        [Test]
        public void ShardFrostbiteCard_AppliesToShardsOnly()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "56", "60");
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(4, 0);
            CatalogWorld.Hit(ice.Config.Reactions, hit);
            var result = HitRecorder.Hit(CatalogWorld.Active().First());
            Assert.AreEqual(result.Damage * Effect("60", "frostbite", 8).value / 100, result.Frostbite, .001f, "조각 피해에 선언된 동상 비율을 적용한다");
            Assert.AreEqual(0, ice.Stats.Status.FrostbiteRatio, "본체는 동상을 얻지 않는다");
        }

        [Test]
        public void Triangle_ShootsThreeNormalSpearsInheritingParentStatsAtHalfDamage()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "52", "59", "61");
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(4, 0); world.AddEnemy(0, 5); world.AddEnemy(-6, 0);

            CatalogWorld.Hit(ice.Config.Reactions, hit);

            var normals = CatalogWorld.Active();
            Assert.AreEqual(Effect("61", "onEvent").count, normals.Count);
            foreach (var normal in normals)
            {
                var r = HitRecorder.Hit(normal);
                var inherit = Effect("61", "onEvent").inherit;
                Assert.AreEqual((int)(ice.Stats.Cast.Damage * inherit["damage"]), r.Damage);
                Assert.AreEqual(ice.Stats.Status.FreezeDuration * inherit["freezeDuration"], r.Freeze, .001f);
                Assert.AreEqual(ice.Stats.Projectile.KnockbackDistance * inherit["knockbackDistance"], r.KnockbackDistance, .001f);
                Assert.AreEqual(r.Damage * ice.Stats.Status.FrostbiteRatio * inherit["frostbiteRatio"], r.Frostbite, .001f);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TriangleThenSplit_NormalsSplitIntoSmallShards_InEitherCardOrder(bool splitFirst)
        {
            var ice = world.Create(IceId);
            if (splitFirst) { world.Take(ice, "56", "57", "61"); }
            else { world.Take(ice, "61", "56", "57"); }
            var hit = world.AddEnemy(0, 0);
            var second = world.AddEnemy(4, 0); world.AddEnemy(0, 5); world.AddEnemy(-6, 0);

            CatalogWorld.Hit(ice.Config.Reactions, hit);
            var normals = CatalogWorld.Active();
            Assert.AreEqual(Effect("61", "onEvent").count, normals.Count, "삼각 형태에서는 일반 얼음창만 나간다");

            CatalogWorld.Hit(CatalogWorld.ReactionsOf(normals[0]), second);
            var shards = CatalogWorld.Active().Except(normals).ToList();
            Assert.AreEqual(Effect("56", "onEvent", 9).count, shards.Count);
            foreach (var shard in shards)
            {
                int inheritedDamage = (int)(ice.Stats.Cast.Damage * Effect("61", "onEvent").inherit["damage"]);
                Assert.AreEqual((int)(inheritedDamage * Effect("56", "onEvent", 9).inherit["damage"] * (1 + Effect("57", "damage", 8).value / 100)), HitRecorder.Hit(shard).Damage);
            }
        }

        [Test]
        public void TriangleWithoutSplitCard_NormalsDoNotSplit()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "61");
            var hit = world.AddEnemy(0, 0);
            var second = world.AddEnemy(4, 0); world.AddEnemy(0, 5);
            CatalogWorld.Hit(ice.Config.Reactions, hit);
            var normals = CatalogWorld.Active();
            Assert.AreEqual(Effect("61", "onEvent").count, normals.Count);
            CatalogWorld.Hit(CatalogWorld.ReactionsOf(normals[0]), second);
            Assert.AreEqual(normals.Count, CatalogWorld.Active().Count, "분열 카드가 없으면 일반 얼음창은 쪼개지지 않는다");
        }
    }
}
