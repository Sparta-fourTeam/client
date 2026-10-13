using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>화살 보조 화살, 화염구 불꽃, 벼락 전기 구체를 자식 스킬로 옮긴 뒤에도 카탈로그에 선언된 상속·분할 규칙이 실제 적중에 적용되는지 본다.</summary>
    public sealed class ShardMigrationTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private CatalogWorld world;
        private EffectDef Effect(int id, string card, string kind) => world.Data[id].upgrades.Find(c => c.id == card).effects.First(e => e.kind == kind);

        [TearDown]
        public void TearDown() => world?.Dispose();

        private static IEnemyTarget Ignored(Projectile p) => (IEnemyTarget)typeof(Projectile).GetField("ignoredTarget", Flags).GetValue(p);

        // ── 화살 보조 화살 ────────────────────────────────────────────

        [Test]
        public void Arrow_SpreadAndBarrageShootSixShardsWithSeparateDamageBonus()
        {
            world = new CatalogWorld(data => data[1].baseStats.cast.baseDamage = 100);
            var arrow = world.Create(1);
            world.Take(arrow, "arrow_sharp", "arrow_spread", "arrow_barrage");
            var parentDamage = arrow.Stats.Cast.Damage;
            world.Take(arrow, "arrow_aux_damage");
            var hit = world.AddEnemy(0, 0);
            for (int i = 1; i <= 7; i++) { world.AddEnemy(i * 2, i % 2 * 3); }

            CatalogWorld.Hit(arrow.Config.Reactions, hit);

            var shards = CatalogWorld.Active();
            var link = Effect(1, "arrow_spread", "onEvent");
            int count = link.count > 1 ? link.count : world.Data[link.skillId].baseStats.cast.projectileCount;
            Assert.AreEqual(count + (int)Effect(1, "arrow_barrage", "projectileCount").value, shards.Count);
            foreach (var shard in shards)
            {
                Assert.AreEqual((int)(parentDamage * link.inherit["damage"] * (1 + Effect(1, "arrow_aux_damage", "damage").value / 100)), HitRecorder.Hit(shard).Damage);
                Assert.AreSame(hit, Ignored(shard));
            }
            Assert.AreEqual(parentDamage, arrow.Stats.Cast.Damage, "보조 피해 강화는 본체에 적용하지 않는다");
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
            foreach (var shard in CatalogWorld.Active()) { Assert.AreEqual((int)(damage * Effect(1, "arrow_spread", "onEvent").inherit["damage"] * (1 + Effect(1, "arrow_aux_damage", "damage").value / 100)), HitRecorder.Hit(shard).Damage); }
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
            Assert.AreEqual(Effect(1, "arrow_spread", "onEvent").count, shards.Count);

            var nearShardHit = world.AddEnemy(20, 20);
            var bystander = world.AddEnemy(20 + arrow.Stats.Explosion.Radius * .25f, 20);
            HitRecorder.HitAt(shards[0], nearShardHit);
            Assert.AreEqual(withCard ? (int)(arrow.Stats.Explosion.Damage * Effect(1, "arrow_aux_explosion", "inherit").inherit["explosionDamage"]) : 0, bystander.Damage, "조각 카드의 폭발 상속은 카드가 있을 때만 적용된다");
            Assert.AreEqual(shards.Count, CatalogWorld.Active().Count, "조각이 맞아도 새 조각이 생기지 않는다");
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
            var link = Effect(2, "fireball_flames", "onEvent");
            Assert.AreEqual(link.count, flames.Count);
            foreach (var flame in flames)
            {
                var r = HitRecorder.Hit(flame);
                Assert.AreEqual((int)(fireball.Stats.Cast.Damage * link.inherit["damage"]), r.Damage);
                Assert.AreEqual(0, r.BurnDuration, "점화는 상속하지 않는다");
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
            var link = Effect(3, "lightning_split", "onEvent");
            Assert.AreEqual(link.count, orbs.Count);
            int expected = (int)(lightning.Stats.Cast.Damage * link.inherit["damage"] * (voltage ? 1 + Effect(3, "lightning_particle_voltage", "damage").value / 100 : 1));
            foreach (var orb in orbs)
            {
                var r = HitRecorder.Hit(orb);
                Assert.AreEqual(expected, r.Damage, "선언된 본체 피해 상속 × 입자 피해 보정");
                Assert.AreEqual(voltage ? Effect(3, "lightning_particle_voltage", "paralysis").value : 0, r.Paralysis);
                Assert.AreSame(struck, Ignored(orb), "맞은 적은 지나친다");
                Assert.AreEqual(new Vector3(5, 5, 0), orb.transform.position, "타격 지점에서 퍼진다");
            }
            var angles = orbs.Select(o => Mathf.RoundToInt(Mathf.Atan2(CatalogWorld.Direction(o).y, CatalogWorld.Direction(o).x) * 100)).Distinct().Count();
            Assert.AreEqual(orbs.Count, angles, "각 구체는 서로 다른 방사 방향으로 나간다");
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
