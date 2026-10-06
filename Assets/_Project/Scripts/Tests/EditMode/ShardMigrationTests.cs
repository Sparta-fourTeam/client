using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>화살 보조 화살, 화염구 불꽃, 벼락 전기 구체를 자식 스킬로 옮긴 뒤에도 기존 수치와 규칙이 같은지 본다.</summary>
    public sealed class ShardMigrationTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private CatalogWorld world;

        [TearDown]
        public void TearDown() => world?.Dispose();

        private static IEnemyTarget Ignored(Projectile p) => (IEnemyTarget)typeof(Projectile).GetField("ignoredTarget", Flags).GetValue(p);

        // ── 화살 보조 화살 ────────────────────────────────────────────

        [Test]
        public void Arrow_SpreadAndBarrageShootSixShardsWithSeparateDamageBonus()
        {
            world = new CatalogWorld(data => data[1].baseStats.cast.baseDamage = 100);
            var arrow = world.Create(1);
            world.Take(arrow, "arrow_sharp", "arrow_spread", "arrow_barrage", "arrow_aux_damage");
            var hit = world.AddEnemy(0, 0);
            for (int i = 1; i <= 7; i++) { world.AddEnemy(i * 2, i % 2 * 3); }

            CatalogWorld.Hit(arrow.Config.Reactions, hit);

            var shards = CatalogWorld.Active();
            Assert.AreEqual(6, shards.Count, "확산 2 + 난사 4");
            foreach (var shard in shards)
            {
                Assert.AreEqual(160, HitRecorder.Hit(shard).Damage, "본체 160 × 0.5 × 보조 피해 +100%");
                Assert.AreEqual(.5f, shard.transform.localScale.x, "화살 프리팹의 절반 크기");
                Assert.AreSame(hit, Ignored(shard));
            }
            Assert.AreEqual(160, arrow.Stats.Cast.Damage, "보조 피해 강화는 본체에 적용하지 않는다");
        }

        [Test]
        public void Arrow_ShardsFollowParentDamageUpgradesTakenLater()
        {
            world = new CatalogWorld(data => data[1].baseStats.cast.baseDamage = 100);
            var arrow = world.Create(1);
            world.Take(arrow, "arrow_sharp", "arrow_spread", "arrow_aux_damage");
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(4, 0); world.AddEnemy(0, 5);
            world.Take(arrow, "arrow_amplify");

            CatalogWorld.Hit(arrow.Config.Reactions, hit);
            var damage = arrow.Stats.Cast.Damage;
            foreach (var shard in CatalogWorld.Active()) { Assert.AreEqual((int)(damage * .5f * 2), HitRecorder.Hit(shard).Damage); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Arrow_ShardExplosionRequiresItsCardAndNeverSplitsAgain(bool withCard)
        {
            world = new CatalogWorld(data => data[1].baseStats.cast.baseDamage = 100);
            var arrow = world.Create(1);
            world.Take(arrow, "arrow_explosion", "arrow_spread");
            if (withCard) { world.Take(arrow, "arrow_aux_explosion"); }
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(6, 0); world.AddEnemy(0, 6);
            CatalogWorld.Hit(arrow.Config.Reactions, hit);
            var shards = CatalogWorld.Active();
            Assert.AreEqual(2, shards.Count);

            var nearShardHit = world.AddEnemy(20, 20);
            var bystander = world.AddEnemy(20.3f, 20);
            HitRecorder.HitAt(shards[0], nearShardHit);
            Assert.AreEqual(withCard ? 50 : 0, bystander.Damage, "조각 폭발 = 본체 폭발 100 × 0.5. 카드가 없으면 폭발하지 않는다");
            Assert.AreEqual(2, CatalogWorld.Active().Count, "조각이 맞아도 새 조각이 생기지 않는다");
        }

        // ── 화염구 불꽃 ───────────────────────────────────────────────

        [Test]
        public void Fireball_FlamesAreThreeHalfDamageChildrenWithoutInheritingBurn()
        {
            world = new CatalogWorld(data => data[2].baseStats.cast.baseDamage = 100);
            var fireball = world.Create(2);
            world.Take(fireball, "fireball_flames");
            var hit = world.AddEnemy(0, 0);
            world.AddEnemy(4, 0); world.AddEnemy(0, 5); world.AddEnemy(-6, 0);

            CatalogWorld.Hit(fireball.Config.Reactions, hit);

            var flames = CatalogWorld.Active();
            Assert.AreEqual(3, flames.Count);
            foreach (var flame in flames)
            {
                var r = HitRecorder.Hit(flame);
                Assert.AreEqual(50, r.Damage);
                Assert.AreEqual(0, r.BurnDuration, "점화는 상속하지 않는다");
                Assert.AreEqual(.5f, flame.transform.localScale.x);
            }
        }

        // ── 벼락 전기 구체 ────────────────────────────────────────────

        private static HitscanEffect Strike() => Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None)
            .Single(e => e.gameObject.scene.IsValid() && e.gameObject.activeInHierarchy && e.name.Contains("Clone"));

        [TestCase(false)]
        [TestCase(true)]
        public void Lightning_ElectronSplitShootsSixRadialOrbsAndParticleVoltageAddsDamageAndParalysis(bool voltage)
        {
            world = new CatalogWorld();
            var lightning = world.Create(3);
            world.Take(lightning, "lightning_voltage", "lightning_damage", "lightning_split");
            if (voltage) { world.Take(lightning, "lightning_particle_voltage"); }
            var struck = world.AddEnemy(5, 5);

            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(lightning, null);
            typeof(HitscanEffect).GetMethod("Hit", Flags).Invoke(Strike(), null);

            var orbs = CatalogWorld.Active();
            Assert.AreEqual(6, orbs.Count);
            int expected = (int)(lightning.Stats.Cast.Damage * .5f * (voltage ? 1.4f : 1f));
            foreach (var orb in orbs)
            {
                var r = HitRecorder.Hit(orb);
                Assert.AreEqual(expected, r.Damage, "본체 피해 × 0.5 × 입자 피해 보정");
                Assert.AreEqual(voltage ? .5f : 0, r.Paralysis);
                Assert.AreSame(struck, Ignored(orb), "맞은 적은 지나친다");
                Assert.AreEqual(new Vector3(5, 5, 0), orb.transform.position, "타격 지점에서 퍼진다");
            }
            var angles = orbs.Select(o => Mathf.RoundToInt(Mathf.Atan2(CatalogWorld.Direction(o).y, CatalogWorld.Direction(o).x) * 100)).Distinct().Count();
            Assert.AreEqual(6, angles, "사방 6방향");
        }

        [Test]
        public void Lightning_WithoutSplitCardFiresNoOrbs()
        {
            world = new CatalogWorld();
            var lightning = world.Create(3);
            world.AddEnemy(5, 5);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(lightning, null);
            typeof(HitscanEffect).GetMethod("Hit", Flags).Invoke(Strike(), null);
            Assert.AreEqual(0, CatalogWorld.Active().Count);
        }
    }
}
