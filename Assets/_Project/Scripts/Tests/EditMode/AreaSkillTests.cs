using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>영역 공격(냉기 지대): 영역 동작, 전략, 카드 효과, 자식 스킬 시전</summary>
    public sealed class AreaSkillTests
    {
        private const string FrostPrisonPrefab = "Assets/_Project/Prefabs/Skills/FrostPrison.prefab";
        private const int FrostPrisonId = 6;
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private sealed class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new List<IEnemyTarget>();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }

        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<SkillCaster> casters = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var caster in casters) { caster.Dispose(); }
            casters.Clear();
            foreach (var go in created) { if (go != null) { Object.DestroyImmediate(go); } }
            created.Clear();

        }

        private ObjectPool<AreaZone> ZonePool()
        {
            return new ObjectPool<AreaZone>(
                () => { var go = new GameObject("AreaZoneTest"); created.Add(go); return go.AddComponent<AreaZone>(); },
                z => z.gameObject.SetActive(true), z => z.gameObject.SetActive(false));
        }

        private static void Tick(AreaZone zone, float dt) => typeof(AreaZone).GetMethod("Tick", Flags).Invoke(zone, new object[] { dt });

        private static AttackReactions Damage(float damage) => new HitReactionBuilder().Damage(damage).Build();

        [Test]
        public void Zone_PulsesDamageOnlyToEnemiesInsideRadius()
        {
            var inside = new HitRecorder { Position = new Vector2(1, 0) };
            var outside = new HitRecorder { Position = new Vector2(5, 0) };
            var provider = new Provider();
            provider.Targets.Add(inside); provider.Targets.Add(outside);
            var pool = ZonePool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(2, 3, 1), Damage(10), AttackReactions.Empty);

            Tick(zone, .1f);
            Assert.AreEqual(10, inside.Damage, "첫 틱에 바로 첫 펄스가 난다");
            Assert.AreEqual(0, outside.Damage);
            Tick(zone, .5f);
            Assert.AreEqual(10, inside.Damage, "펄스 간격 안에서는 다시 때리지 않는다");
            Tick(zone, .5f);
            Assert.AreEqual(20, inside.Damage);
        }

        [Test]
        public void Zone_RaisesStartHitAndExpiredAndReleasesAfterDuration()
        {
            var target = new HitRecorder { Position = Vector2.zero };
            var provider = new Provider();
            provider.Targets.Add(target);
            var events = new List<AttackEvent>();
            var reactions = new HitReactionBuilder()
                .On(AttackEvent.Start, new CastSkillReaction(_ => events.Add(AttackEvent.Start)))
                .On(AttackEvent.Hit, new CastSkillReaction(_ => events.Add(AttackEvent.Hit)))
                .On(AttackEvent.Expired, new CastSkillReaction(_ => events.Add(AttackEvent.Expired)))
                .Build();
            var pool = ZonePool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(2, 1, 2), AttackReactions.Empty, reactions);

            Tick(zone, .5f);
            Tick(zone, .5f);
            Assert.AreEqual(new[] { AttackEvent.Start, AttackEvent.Hit, AttackEvent.Expired }, events);
            Assert.IsFalse(zone.gameObject.activeSelf, "지속 시간이 끝나면 풀로 돌아간다");
            Tick(zone, 1f);
            Assert.AreEqual(3, events.Count, "풀로 돌아간 뒤에는 반응이 더 나지 않는다");
        }

        [Test]
        public void Zone_RaisesPeriodicTickWithAttackLocalTime()
        {
            var provider = new Provider();
            int pulses = 0;
            var reactions = new HitReactionBuilder()
                .On(AttackEvent.Tick, new PeriodicReaction(1, new CastSkillReaction(_ => pulses++))).Build();
            var pool = ZonePool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(2, 3, 1), AttackReactions.Empty, reactions);
            for (int i = 0; i < 10; i++) { Tick(zone, .25f); }
            Assert.AreEqual(2, pulses, "2.5초 동안 1초 주기 반응은 두 번 난다");
        }

        // ── 전략과 카탈로그 ───────────────────────────────────────────

        private static SkillData FrostPrison() =>
            new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == FrostPrisonId);

        private SkillCaster CreateCaster(SkillData data, Provider provider, out GameObject prefab)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FrostPrisonPrefab);
            Assert.IsNotNull(asset, FrostPrisonPrefab);
            prefab = asset;
            var owner = new GameObject("AreaCasterOwner");
            created.Add(owner);
            var caster = SkillFactory.Create(data, asset, owner.transform, provider);
            casters.Add(caster);
            return caster;
        }

        private static List<Projectile> ActiveProjectiles() => Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
            .Where(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy).ToList();

        private static List<AreaZone> Zones() => Object.FindObjectsByType<AreaZone>(FindObjectsSortMode.None)
            .Where(z => z.gameObject.scene.IsValid() && z.gameObject.activeInHierarchy).ToList();

        [Test]
        public void Strategy_PlacesZoneAtTargetWithCatalogRadiusAndSlow()
        {
            var data = FrostPrison();
            Assert.AreEqual(CastType.Area, data.castType);
            var target = new HitRecorder { Position = new Vector2(2, 3) };
            var provider = new Provider(); provider.Targets.Add(target);
            var caster = CreateCaster(data, provider, out _);

            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(caster, null);
            var zones = Zones();
            Assert.AreEqual(1, zones.Count);
            Assert.AreEqual(new Vector3(2, 3, 0), zones[0].transform.position);
            Tick(zones[0], .1f);
            Assert.AreEqual((int)caster.Stats.Cast.Damage, target.Damage);
            Assert.AreEqual(caster.Stats.Status.SlowRatio, target.SlowRatio, .001f);
            Assert.AreEqual(caster.Stats.Status.SlowDuration, target.SlowDuration);
        }

        // 시전 수가 늘면 연발의 각 시전이 서로 다른 적에게 영역을 깐다 (같은 적에게 겹쳐 깔지 않는다)
        [Test]
        public void CastCountCard_EachCastInBurstTargetsDifferentEnemy()
        {
            var data = FrostPrison();
            var near = new HitRecorder { Position = new Vector2(0, 1) };
            var far = new HitRecorder { Position = new Vector2(1, 3) };
            var provider = new Provider(); provider.Targets.Add(near); provider.Targets.Add(far);
            var caster = CreateCaster(data, provider, out _);
            Assert.IsTrue(caster.LevelUp(data.upgrades.Find(c => c.id == "73")));
            Assert.AreEqual(2, caster.Stats.Cast.Count);

            caster.Tick(0);
            caster.Tick(caster.Stats.Cast.Interval);

            var positions = Zones().Select(z => (Vector2)z.transform.position).ToList();
            CollectionAssert.AreEquivalent(new[] { near.Position, far.Position }, positions);
        }

        [Test]
        public void ShatterCard_EndToEnd_SpawnsRealFrostCrystalProjectilesFromExpiredZone()
        {
            const string IceSpearPrefab = "Assets/_Project/Prefabs/Skills/IceSpear.prefab";
            var data = FrostPrison();
            var all = new DefaultSkillDataProvider(new GameDataStore()).LoadAll();
            var link = data.upgrades.Find(c => c.id == "79").effects.Single(e => e.kind == "onEvent");
            var iceData = all.Find(w => w.id == link.skillId);
            var icePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IceSpearPrefab);
            Assert.IsNotNull(icePrefab, IceSpearPrefab);

            var target = new HitRecorder { Position = new Vector2(1, 1) };
            var provider = new Provider(); provider.Targets.Add(target);
            var caster = CreateCaster(data, provider, out _);
            var children = new ChildSkillCaster(id =>
            {
                Assert.AreEqual(link.skillId, id);
                var owner = new GameObject("ChildOwner"); created.Add(owner);
                return SkillFactory.Create(iceData, icePrefab, owner.transform, provider);
            });
            try
            {
                caster.UseChildCaster(children);
                Assert.IsTrue(caster.LevelUp(data.upgrades.Find(c => c.id == "79")));
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(caster, null);
                Assert.AreEqual(0, ActiveProjectiles().Count, "영역이 유지되는 동안에는 얼음창이 없다");
                var zone = Zones().Single();
                Tick(zone, caster.Stats.Area.Duration * .5f);
                Assert.IsEmpty(ActiveProjectiles(), "영역 만료 전에는 자식 발사가 없다");
                Tick(zone, caster.Stats.Area.Duration);
                var shots = ActiveProjectiles();
                Assert.AreEqual(link.count > 1 ? link.count : iceData.baseStats.cast.projectileCount, shots.Count);
                foreach (var shot in shots)
                {
                    Assert.AreEqual(new Vector3(1, 1, 0), shot.transform.position);
                    Assert.AreEqual((int)(iceData.baseStats.cast.baseDamage * link.damageScale), HitRecorder.Hit(shot).Damage);
                }
            }
            finally
            {
                children.Dispose();

            }
        }

        [Test]
        public void ShatterCard_FailsWithoutChildCaster()
        {
            var data = FrostPrison();
            var caster = CreateCaster(data, new Provider(), out _);
            Assert.IsFalse(caster.LevelUp(data.upgrades.Find(c => c.id == "79")),
                "시전기가 연결되지 않으면 조용히 무시하지 않고 실패한다");
        }

        [Test]
        public void Validator_RejectsAreaEffectOnProjectileWeapon()
        {
            var catalog = new GameDataStore().LoadSkills();
            var weapon = catalog.Find(w => w.castType == CastType.Projectile);
            weapon.upgrades[0].effects.Add(new EffectDef { kind = "areaRadius", value = 10 });
            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains("areaRadius", error.Message);
        }

        // ── 전기 구름: 이동, 끌어당김, 적중 시 확률 자식 시전 ─────────────

        private const int LightningCloudId = 7;
        private const int LightningId = 3;
        private const string LightningPrefab = "Assets/_Project/Prefabs/Skills/Lightning.prefab";

        private static SkillData Cloud() => new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == LightningCloudId);

        [Test]
        public void Zone_PullsEnemiesInsideTowardCenterWithoutOvershootAndIgnoresOutside()
        {
            var near = new HitRecorder { Position = new Vector2(.3f, 0) };
            var inside = new HitRecorder { Position = new Vector2(1.5f, 0) };
            var outside = new HitRecorder { Position = new Vector2(6, 0) };
            var provider = new Provider();
            provider.Targets.AddRange(new[] { near, inside, outside });
            var pool = ZonePool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(2, 5, 1, pull: .5f), AttackReactions.Empty, AttackReactions.Empty);

            Tick(zone, .1f);
            Assert.AreEqual(0f, near.Position.x, .001f, "중심까지 거리보다 더 끌어당기지 않는다");
            Assert.AreEqual(1f, inside.Position.x, .001f);
            Assert.AreEqual(6f, outside.Position.x, .001f);
            Assert.Less(inside.PullDirection.x, 0, "중심 쪽으로 향한다(끌어당기기는 좌우도 움직인다)");
        }

        [Test]
        public void Zone_DriftsTowardNearestEnemyAtMoveSpeed()
        {
            var enemy = new HitRecorder { Position = new Vector2(5, 0) };
            var provider = new Provider(); provider.Targets.Add(enemy);
            var pool = ZonePool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(1, 10, 1, moveSpeed: 1), AttackReactions.Empty, AttackReactions.Empty);

            Tick(zone, .1f); // 첫 펄스가 가장 가까운 적을 찾는다
            Tick(zone, 1f);
            Assert.AreEqual(1f, zone.transform.position.x, .01f, "초당 1 거리만큼 이동한다");
            Tick(zone, 10f);
            Assert.AreEqual(5f, zone.transform.position.x, .01f, "목표를 지나치지 않는다");
        }

        [Test]
        public void Zone_StaysPutWithoutMoveSpeed()
        {
            var provider = new Provider(); provider.Targets.Add(new HitRecorder { Position = new Vector2(5, 0) });
            var pool = ZonePool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(1, 10, 1), AttackReactions.Empty, AttackReactions.Empty);
            Tick(zone, 1f);
            Assert.AreEqual(Vector3.zero, zone.transform.position);
        }

        [Test]
        public void GuideCard_EndToEnd_SpawnsRealLightningStrikeFromAreaHit()
        {
            var data = Cloud();
            var lightningData = new DefaultSkillDataProvider(new GameDataStore()).LoadAll().Find(w => w.id == LightningId);
            var lightningPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LightningPrefab);
            Assert.IsNotNull(lightningPrefab, LightningPrefab);
            var target = new HitRecorder { Position = new Vector2(1, 1) };
            var provider = new Provider(); provider.Targets.Add(target);
            var children = new ChildSkillCaster(id =>
            {
                Assert.AreEqual(LightningId, id);
                var owner = new GameObject("LightningChildOwner"); created.Add(owner);
                return SkillFactory.Create(lightningData, lightningPrefab, owner.transform, provider);
            });
            try
            {
                var builder = new SkillConfigBuilder(SkillConfig.FromDefinition(data).WithChildCaster(children));
                var effects = data.upgrades.Find(c => c.id == "90").effects;
                var chance = effects.Single(e => e.kind == "onEvent").chance;
                Assert.IsTrue(builder.TryApplyCatalog(effects));
                var pool = ZonePool();
                var zone = pool.Get();
                var reactions = builder.Build().Reactions;
                zone.Init(pool, provider, Vector2.zero, new AreaSettings(3, 10, 1), AttackReactions.Empty, reactions, () => chance);
                Tick(zone, .1f);
                Assert.IsEmpty(ActiveStrikes(), "확률의 정확한 경계는 발사하지 않는다");
                zone = pool.Get();
                zone.Init(pool, provider, Vector2.zero, new AreaSettings(3, 10, 1), AttackReactions.Empty, reactions, () => chance * .5f);
                Assert.AreEqual(0, ActiveStrikes().Count);
                Tick(zone, .1f);
                Assert.AreEqual(1, ActiveStrikes().Count, "벼락이 실제로 하나 떨어진다");
                Assert.AreEqual(new Vector3(1, 1, 0), ActiveStrikes()[0].transform.position, "맞은 적의 위치에 떨어진다");
            }
            finally
            {
                children.Dispose();

            }
        }

        private static List<HitscanEffect> ActiveStrikes() => Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None)
            .Where(e => e.gameObject.scene.IsValid() && e.gameObject.activeInHierarchy).ToList();
    }
}
