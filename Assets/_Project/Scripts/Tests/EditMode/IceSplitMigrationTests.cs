using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>얼음창 분열과 삼각 얼음창을 자식 스킬로 옮긴 뒤에도 기존 수치와 규칙이 같은지 본다.
    /// 기준값은 옮기기 전 테스트와 같다(본체 피해 100 → 일반 50, 조각 45/90).</summary>
    public sealed class IceSplitMigrationTests
    {
        private const int IceId = 4;
        // 얼음창 프리팹의 크기. 일반 얼음창은 같은 크기, 소형 조각은 절반이다 (옮기기 전 projectileScale과 같다)
        private const float SpearScale = 0.3f;
        private CatalogWorld world;

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
            world.Take(ice, "ice_split", "ice_shard_damage");
            var hit = world.AddEnemy(0, 0);
            var a = world.AddEnemy(4, 0); var b = world.AddEnemy(0, 5); var c = world.AddEnemy(-6, 0);

            CatalogWorld.Hit(ice.Config.Reactions, hit);

            var shards = CatalogWorld.Active();
            Assert.AreEqual(3, shards.Count);
            foreach (var shard in shards)
            {
                Assert.AreEqual(90, HitRecorder.Hit(shard).Damage, "본체 100 × 0.5 × 1.8");
                Assert.AreEqual(SpearScale * .5f, shard.transform.localScale.x, .0001f, "소형 조각은 얼음창의 절반 크기");
                Assert.AreSame(hit, typeof(Projectile).GetField("ignoredTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(shard));
            }
            var aimed = shards.Select(s => new[] { a, b, c }.OrderBy(e => Aim(s, Vector2.zero, e)).First()).Distinct().Count();
            Assert.AreEqual(3, aimed, "서로 다른 적 3명을 향한다");
            Assert.AreEqual(100, ice.Stats.Cast.Damage, "조각 피해 강화는 본체에 적용하지 않는다");
        }

        [Test]
        public void ShardsDoNotSplitAgain()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "ice_split");
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
            world.Take(ice, "ice_split", "ice_shard_frostbite");
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(4, 0);
            CatalogWorld.Hit(ice.Config.Reactions, hit);
            var result = HitRecorder.Hit(CatalogWorld.Active().First());
            Assert.AreEqual(result.Damage * .1f, result.Frostbite, .001f, "조각 동상 = 조각 피해 × 10%");
            Assert.AreEqual(0, ice.Stats.Status.FrostbiteRatio, "본체는 동상을 얻지 않는다");
        }

        [Test]
        public void Triangle_ShootsThreeNormalSpearsInheritingParentStatsAtHalfDamage()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "ice_extreme", "ice_frostbite", "ice_triangle");
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(4, 0); world.AddEnemy(0, 5); world.AddEnemy(-6, 0);

            CatalogWorld.Hit(ice.Config.Reactions, hit);

            var normals = CatalogWorld.Active();
            Assert.AreEqual(3, normals.Count);
            foreach (var normal in normals)
            {
                var r = HitRecorder.Hit(normal);
                Assert.AreEqual(65, r.Damage, "극한 서리 +30%가 반영된 본체 130의 절반");
                Assert.AreEqual(2, r.Freeze, "빙결 상속");
                Assert.AreEqual(.2f, r.KnockbackDistance, .001f, "밀치기 상속");
                Assert.AreEqual(r.Damage * .1f, r.Frostbite, .001f, "동상 상속");
                Assert.AreEqual(SpearScale, normal.transform.localScale.x, .0001f, "일반 얼음창은 얼음창 프리팹 크기");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TriangleThenSplit_NormalsSplitIntoSmallShards_InEitherCardOrder(bool splitFirst)
        {
            var ice = world.Create(IceId);
            if (splitFirst) { world.Take(ice, "ice_split", "ice_shard_damage", "ice_triangle"); }
            else { world.Take(ice, "ice_triangle", "ice_split", "ice_shard_damage"); }
            var hit = world.AddEnemy(0, 0);
            var second = world.AddEnemy(4, 0); world.AddEnemy(0, 5); world.AddEnemy(-6, 0);

            CatalogWorld.Hit(ice.Config.Reactions, hit);
            var normals = CatalogWorld.Active();
            Assert.AreEqual(3, normals.Count, "삼각 형태에서는 조각이 아니라 일반 얼음창만 나간다");

            CatalogWorld.Hit(CatalogWorld.ReactionsOf(normals[0]), second);
            var shards = CatalogWorld.Active().Except(normals).ToList();
            Assert.AreEqual(3, shards.Count, "일반 얼음창이 맞으면 소형 조각 3개");
            foreach (var shard in shards)
            {
                Assert.AreEqual(45, HitRecorder.Hit(shard).Damage, "일반 50 × 0.5 × 1.8");
                Assert.AreEqual(SpearScale * .5f, shard.transform.localScale.x, .0001f);
            }
        }

        [Test]
        public void TriangleWithoutSplitCard_NormalsDoNotSplit()
        {
            var ice = world.Create(IceId);
            world.Take(ice, "ice_triangle");
            var hit = world.AddEnemy(0, 0);
            var second = world.AddEnemy(4, 0); world.AddEnemy(0, 5);
            CatalogWorld.Hit(ice.Config.Reactions, hit);
            var normals = CatalogWorld.Active();
            Assert.AreEqual(3, normals.Count);
            CatalogWorld.Hit(CatalogWorld.ReactionsOf(normals[0]), second);
            Assert.AreEqual(3, CatalogWorld.Active().Count, "분열 카드가 없으면 일반 얼음창은 쪼개지지 않는다");
        }

        [Test]
        public void ChildSkillsAreHiddenFromCardChoices()
        {
            Assert.IsTrue(world.Data[8].childOnly);
            Assert.IsTrue(world.Data[12].childOnly);
            Assert.AreEqual(0, world.Data[8].upgrades.Count);
        }
    }
}
