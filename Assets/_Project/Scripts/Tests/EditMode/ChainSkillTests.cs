using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>연쇄(연쇄 번개): 튕김 규칙, 이벤트, 경로 피해, 카드 효과</summary>
    public sealed class ChainSkillTests
    {
        private const int ChainLightningId = 18;
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<GameObject> created = new List<GameObject>();
        private CatalogWorld world;

        [TearDown]
        public void TearDown()
        {
            world?.Dispose();
            world = null;
            foreach (var go in created) { if (go != null) { Object.DestroyImmediate(go); } }
            created.Clear();
            foreach (var bolt in Object.FindObjectsByType<ChainBolt>(FindObjectsSortMode.None)) { Object.DestroyImmediate(bolt.gameObject); }
        }

        private ObjectPool<ChainBolt> Pool() => new ObjectPool<ChainBolt>(
            () => { var go = new GameObject("ChainBoltTest"); created.Add(go); return go.AddComponent<ChainBolt>(); },
            b => b.gameObject.SetActive(true), b => b.gameObject.SetActive(false));

        private static void Tick(ChainBolt bolt, float dt) => typeof(ChainBolt).GetMethod("Tick", Flags).Invoke(bolt, new object[] { dt });

        private static HitRecorder Enemy(float x, float y) => new HitRecorder { Position = new Vector2(x, y) };

        private ChainBolt Start(CatalogWorld.Targets provider, HitRecorder first, ChainSettings settings,
            AttackReactions hit = null, AttackReactions reactions = null)
        {
            var pool = Pool();
            var bolt = pool.Get();
            bolt.Init(pool, provider, Vector2.zero, first, settings, hit ?? new HitReactionBuilder().Damage(10).Build(), reactions ?? AttackReactions.Empty);
            return bolt;
        }

        private static void RunAll(ChainBolt bolt) { for (int i = 0; i < 40; i++) { Tick(bolt, .1f); } }

        // ── 튕김 규칙 ─────────────────────────────────────────────────

        [Test]
        public void Chain_HitsFirstTargetThenBouncesToNearestUnhitEnemiesWithinJumpRange()
        {
            var a = Enemy(3, 0); var b = Enemy(5, 0); var c = Enemy(7, 0); var far = Enemy(30, 0);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, b, c, far });
            var bolt = Start(provider, a, new ChainSettings(2, 4, .1f));
            RunAll(bolt);
            Assert.AreEqual(10, a.Damage); Assert.AreEqual(10, b.Damage); Assert.AreEqual(10, c.Damage);
            Assert.AreEqual(0, far.Damage, "튕김 거리 밖의 적은 맞지 않는다");
            Assert.IsFalse(bolt.gameObject.activeSelf, "끝나면 풀로 돌아간다");
        }

        [Test]
        public void Chain_NeverHitsTheSameEnemyTwice()
        {
            var a = Enemy(3, 0); var b = Enemy(5, 0);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, b });
            RunAll(Start(provider, a, new ChainSettings(10, 4, .1f)));
            Assert.AreEqual(10, a.Damage);
            Assert.AreEqual(10, b.Damage);
        }

        [Test]
        public void Chain_StopsWhenNoEnemyIsWithinJumpRange()
        {
            var a = Enemy(3, 0); var near = Enemy(5, 0); var isolated = Enemy(20, 0);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, near, isolated });
            RunAll(Start(provider, a, new ChainSettings(5, 4, .1f)));
            Assert.AreEqual(10, near.Damage);
            Assert.AreEqual(0, isolated.Damage);
        }

        [Test]
        public void Chain_WithZeroBouncesHitsOnlyTheFirstTarget()
        {
            var a = Enemy(3, 0); var b = Enemy(5, 0);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, b });
            var bolt = Start(provider, a, new ChainSettings(0, 4, .1f));
            Tick(bolt, .01f);
            Assert.AreEqual(10, a.Damage);
            Assert.AreEqual(0, b.Damage);
            Assert.IsFalse(bolt.gameObject.activeSelf);
        }

        [Test]
        public void Chain_HopsOnePerIntervalAndTheFirstHitIsImmediate()
        {
            var a = Enemy(3, 0); var b = Enemy(5, 0); var c = Enemy(7, 0);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, b, c });
            var bolt = Start(provider, a, new ChainSettings(2, 4, 1f));
            Tick(bolt, .1f);
            Assert.AreEqual(10, a.Damage); Assert.AreEqual(0, b.Damage);
            Tick(bolt, .95f);
            Assert.AreEqual(10, b.Damage); Assert.AreEqual(0, c.Damage);
            Tick(bolt, 1f);
            Assert.AreEqual(10, c.Damage);
        }

        // ── 이벤트와 폭발 ─────────────────────────────────────────────

        [Test]
        public void Chain_RaisesBounceOnlyForArrivalsAfterTheFirstTargetAndExpiredAtTheEnd()
        {
            var a = Enemy(3, 0); var b = Enemy(5, 0); var c = Enemy(7, 0);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, b, c });
            var log = new List<string>();
            var reactions = new HitReactionBuilder()
                .On(AttackEvent.Start, new CastSkillReaction(_ => log.Add("start")))
                .On(AttackEvent.Hit, new CastSkillReaction(x => log.Add("hit@" + x.Position.x)))
                .On(AttackEvent.Bounce, new CastSkillReaction(x => log.Add("bounce@" + x.Position.x)))
                .On(AttackEvent.Expired, new CastSkillReaction(_ => log.Add("expired"))).Build();
            RunAll(Start(provider, a, new ChainSettings(2, 4, .1f), reactions: reactions));
            CollectionAssert.AreEqual(new[] { "start", "hit@3", "hit@5", "bounce@5", "hit@7", "bounce@7", "expired" }, log);
        }

        [Test]
        public void Explosion_FiresOnEachBounceArrivalButNotOnTheFirstHit()
        {
            var stats = SkillStats.FromDefinition(new SkillBaseStats { cast = { baseDamage = 20 }, explosion = { radius = 1, damageRatio = .5f } });
            var a = Enemy(3, 0); var b = Enemy(5, 0);
            // 폭발이 닿는 적은 연쇄 대상과 분리해 둔다(연쇄 대상이 되면 직접 맞아 구분이 안 된다)
            var bystanderA = Enemy(3, .5f); var bystanderB = Enemy(5, .5f);
            var chainTargets = new CatalogWorld.Targets(); chainTargets.All.AddRange(new[] { a, b });
            var blastTargets = new CatalogWorld.Targets(); blastTargets.All.AddRange(new[] { bystanderA, bystanderB });
            var hit = ReactionCompiler.ForChain(stats, blastTargets);
            RunAll(Start(chainTargets, a, new ChainSettings(1, 4, .1f), hit: hit));
            Assert.AreEqual(20, a.Damage); Assert.AreEqual(20, b.Damage);
            Assert.AreEqual(0, bystanderA.Damage, "첫 대상 주변은 터지지 않는다");
            Assert.AreEqual(10, bystanderB.Damage, "튕겨 도착한 자리에서 폭발(피해 × 비율)");
        }

        // ── 경로 피해 ─────────────────────────────────────────────────

        [Test]
        public void PathDamage_HitsEnemiesAlongTheHopSegmentOnly()
        {
            var a = Enemy(0, 3); var b = Enemy(10, 3);
            var onPath = Enemy(5, 3.2f);
            var offPath = Enemy(5, 6);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, b, onPath, offPath });
            RunAll(Start(provider, a, new ChainSettings(1, 12, .1f, pathWidth: 1)));
            Assert.AreEqual(10, onPath.Damage, "튕기는 경로 위의 적도 맞는다");
            Assert.AreEqual(0, offPath.Damage);
        }

        [Test]
        public void WithoutPathWidth_EnemiesBetweenTargetsAreNotHit()
        {
            var a = Enemy(0, 3); var b = Enemy(10, 3);
            var between = Enemy(5, 3.1f);
            var provider = new CatalogWorld.Targets(); provider.All.AddRange(new[] { a, b, between });
            RunAll(Start(provider, a, new ChainSettings(1, 12, .1f)));
            // between이 b보다 a에 더 가까워 먼저 튕겨 맞는다. 경로 피해가 아니라 다음 대상으로서 맞는 것이다.
            Assert.AreEqual(10, between.Damage);
            Assert.AreEqual(0, b.Damage, "반사가 1번뿐이라 거기서 끝난다");
        }

        // ── 연쇄 번개 카탈로그 ────────────────────────────────────────

        private static List<ChainBolt> Bolts() => Object.FindObjectsByType<ChainBolt>(FindObjectsSortMode.None)
            .Where(b => b.gameObject.scene.IsValid() && b.gameObject.activeInHierarchy).ToList();

        private SkillCaster ChainLightning(params string[] cards)
        {
            world = new CatalogWorld();
            var chain = world.Create(ChainLightningId);
            world.Take(chain, cards);
            return chain;
        }

        [Test]
        public void ChainLightning_IsAChainSkillThatBouncesAcrossEnemies()
        {
            var chain = ChainLightning();
            Assert.AreEqual(CastType.Chain, world.Data[ChainLightningId].castType);
            var a = world.AddEnemy(3, 0); var b = world.AddEnemy(5, 0); var c = world.AddEnemy(7, 0); var d = world.AddEnemy(9, 0); var e = world.AddEnemy(11, 0);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(chain, null);
            var bolt = Bolts().Single();
            for (int i = 0; i < 20; i++) { Tick(bolt, .1f); }
            Assert.AreEqual(12, a.Damage); Assert.AreEqual(12, b.Damage); Assert.AreEqual(12, c.Damage); Assert.AreEqual(0, d.Damage);
            Assert.AreEqual(0, e.Damage, "기본 반사 2번 → 첫 대상 포함 3명");
        }

        [Test]
        public void Cards_ContinuousAddsBouncesAndDamage_CrossAddsCasts_ConductionAddsPath()
        {
            var start = SkillConfig.FromDefinition(new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == ChainLightningId)).Stats;
            var chain = ChainLightning("chain_lightning_continuous", "chain_lightning_cross", "chain_lightning_voltage");
            Assert.AreEqual(start.Chain.Bounces + 2, chain.Stats.Chain.Bounces);
            Assert.AreEqual(2, chain.Stats.Cast.Count);
            Assert.AreEqual(start.Cast.Damage * 1.3f * 1.3f, chain.Stats.Cast.Damage, .001f);
            Assert.AreEqual(1f, chain.Stats.Status.ParalysisDuration);
            Assert.AreEqual(0, start.Chain.PathWidth);
            var conduction = ChainLightning("chain_lightning_conduction");
            Assert.Greater(conduction.Stats.Chain.PathWidth, 0);
            Assert.AreEqual(start.Cast.Damage * .4f, conduction.Stats.Cast.Damage, .001f, "공격력 -60%");
        }

        [Test]
        public void IonBurstCard_ExplodesOnBouncesWithRadiusAndSpreadCard()
        {
            var chain = ChainLightning("chain_lightning_ion_burst", "chain_lightning_ion_spread");
            Assert.AreEqual(.8f * 2, chain.Stats.Explosion.Radius, .001f, "이온 확산: 폭발 범위 +100%");
            var a = world.AddEnemy(3, 0); var b = world.AddEnemy(5, 0);
            var bystander = world.AddEnemy(5, 1.2f);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(chain, null);
            var bolt = Bolts().Single();
            for (int i = 0; i < 20; i++) { Tick(bolt, .1f); }
            Assert.Greater(bystander.Damage, 0, "튕겨 도착한 적 주변이 폭발한다");
        }
    }
}
