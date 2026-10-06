using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Newtonsoft.Json;
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

        private sealed class FakeCaster : IChildSkillCaster
        {
            public readonly List<(int id, Vector2 position, float scale, int count)> Casts = new List<(int, Vector2, float, int)>();
            public void Cast(ChildCast cast, AttackContext context) => Casts.Add((cast.SkillId, context.Position, cast.DamageScale, cast.Count));
        }

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) { if (go != null) { Object.DestroyImmediate(go); } }
            created.Clear();
            foreach (var zone in Object.FindObjectsByType<AreaZone>(FindObjectsSortMode.None)) { Object.DestroyImmediate(zone.gameObject); }
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
            return SkillFactory.Create(data, asset, owner.transform, provider);
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
            Assert.AreEqual(8, target.Damage, "기본 피해 8");
            Assert.AreEqual(.4f, target.SlowRatio, .001f);
            Assert.AreEqual(1f, target.SlowDuration);
        }

        [Test]
        public void Cards_ExpandRadiusAndAddCastCount()
        {
            var data = FrostPrison();
            var start = SkillConfig.FromDefinition(data);
            var builder = new SkillConfigBuilder(start);
            Assert.IsTrue(builder.TryApplyCatalog(data.upgrades.Find(c => c.id == "cold_zone_expand").effects));
            Assert.IsTrue(builder.TryApplyCatalog(data.upgrades.Find(c => c.id == "cold_zone_cast").effects));
            var stats = builder.Build().Stats;
            Assert.AreEqual(start.Stats.Area.Radius * 2, stats.Area.Radius, .001f);
            Assert.AreEqual(2, stats.Cast.Count);
        }

        [Test]
        public void ShatterCard_CastsSmallFrostCrystalsWhenZoneExpires()
        {
            var data = FrostPrison();
            var target = new HitRecorder { Position = new Vector2(1, 1) };
            var provider = new Provider(); provider.Targets.Add(target);
            var caster = CreateCaster(data, provider, out _);
            var fake = new FakeCaster();
            caster.UseChildCaster(fake);
            Assert.IsTrue(caster.LevelUp(data.upgrades.Find(c => c.id == "cold_zone_shatter")));

            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(caster, null);
            var zone = Zones().Single();
            Tick(zone, 1f);
            Assert.AreEqual(0, fake.Casts.Count, "지속 시간 중에는 시전하지 않는다");
            Tick(zone, 5f);

            Assert.AreEqual(1, fake.Casts.Count, "한 번 시전하고 그 안에서 2발을 쏜다");
            var cast = fake.Casts[0];
            Assert.AreEqual(4, cast.id, "서리 결정(얼음창)을 자식으로 시전한다");
            Assert.AreEqual(new Vector2(1, 1), cast.position, "영역이 사라진 자리에서 시전한다");
            Assert.AreEqual(.5f, cast.scale);
            Assert.AreEqual(2, cast.count);
        }

        [Test]
        public void ShatterCard_EndToEnd_SpawnsRealFrostCrystalProjectilesFromExpiredZone()
        {
            const string IceSpearPrefab = "Assets/_Project/Prefabs/Skills/IceSpear.prefab";
            var data = FrostPrison();
            var all = new DefaultSkillDataProvider(new GameDataStore()).LoadAll();
            var iceData = all.Find(w => w.id == 4);
            var icePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IceSpearPrefab);
            Assert.IsNotNull(icePrefab, IceSpearPrefab);

            var target = new HitRecorder { Position = new Vector2(1, 1) };
            var provider = new Provider(); provider.Targets.Add(target);
            var caster = CreateCaster(data, provider, out _);
            var children = new ChildSkillCaster(id =>
            {
                Assert.AreEqual(4, id);
                var owner = new GameObject("ChildOwner"); created.Add(owner);
                return SkillFactory.Create(iceData, icePrefab, owner.transform, provider);
            });
            try
            {
                caster.UseChildCaster(children);
                Assert.IsTrue(caster.LevelUp(data.upgrades.Find(c => c.id == "cold_zone_shatter")));
                typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(caster, null);
                Assert.AreEqual(0, ActiveProjectiles().Count, "영역이 유지되는 동안에는 얼음창이 없다");
                Tick(Zones().Single(), 10f);

                Assert.AreEqual(2, ActiveProjectiles().Count, "소형 서리 결정 2개가 실제로 발사된다");
            }
            finally
            {
                children.Dispose();
                foreach (var projectile in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) { Object.DestroyImmediate(projectile.gameObject); }
            }
        }

        [Test]
        public void ShatterCard_FailsWithoutChildCaster()
        {
            var data = FrostPrison();
            var caster = CreateCaster(data, new Provider(), out _);
            Assert.IsFalse(caster.LevelUp(data.upgrades.Find(c => c.id == "cold_zone_shatter")),
                "시전기가 연결되지 않으면 조용히 무시하지 않고 실패한다");
        }

        [Test]
        public void ShippedCatalog_DisablesCardsWithUnimplementedEffectsAndExplainsWhy()
        {
            var disabled = FrostPrison().upgrades.Where(c => !c.enabled).ToList();
            Assert.AreEqual(4, disabled.Count);
            foreach (var card in disabled) { Assert.IsNotEmpty(card.disabledReason, card.id); }
        }

        [Test]
        public void Registry_AreaEffectsAreAreaOnlyAndSharedEffectsIncludeArea()
        {
            Assert.IsTrue(EffectRegistry.Supports(CastType.Area, "areaRadius"));
            Assert.IsFalse(EffectRegistry.Supports(CastType.Projectile, "areaRadius"));
            Assert.IsFalse(EffectRegistry.Supports(CastType.Area, "pierceCount"));
            foreach (var kind in new[] { "damage", "attackSpeed", "castCount", "freezeDuration", "onEvent", "periodic" })
            {
                Assert.IsTrue(EffectRegistry.Supports(CastType.Area, kind), kind);
            }
        }

        [Test]
        public void Validator_RejectsAreaEffectOnProjectileWeapon()
        {
            var catalog = JsonConvert.DeserializeObject<List<SkillData>>(Resources.Load<TextAsset>("MockData/Skills").text);
            var weapon = catalog.Find(w => w.castType == CastType.Projectile);
            weapon.upgrades[0].effects.Add(new EffectDef { kind = "areaRadius", value = 10 });
            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(catalog));
            StringAssert.Contains("areaRadius", error.Message);
        }

        [Test]
        public void DamageScale_ScalesDirectAndExplosionDamageOnly()
        {
            var config = SkillConfig.FromDefinition(new SkillData
            {
                baseStats = new SkillBaseStats { cast = { baseDamage = 10, cooldown = 2 }, explosion = { radius = 1, damageRatio = 1 } }
            });
            var scaled = config.WithDamageScale(.5f);
            Assert.AreEqual(5, scaled.Stats.Cast.Damage, .001f);
            Assert.AreEqual(5, scaled.Stats.Explosion.Damage, .001f);
            Assert.AreEqual(2, scaled.Stats.Cast.Cooldown);
            Assert.AreEqual(10, config.Stats.Cast.Damage, "원본 설정은 바뀌지 않는다");
        }

        // ── 전기 구름: 이동, 끌어당김, 적중 시 확률 자식 시전 ─────────────

        private const int LightningCloudId = 7;
        private const int LightningId = 3;
        private const string LightningPrefab = "Assets/_Project/Prefabs/Skills/Lightning_Lv1.prefab";

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
            Assert.Less(inside.KnockbackDirection.x, 0, "중심 쪽으로 향한다");
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
        public void CloudCards_ScaleMoveSpeedPullDamageAndAddParalysis()
        {
            var data = Cloud();
            var start = SkillConfig.FromDefinition(data);
            SkillConfig Apply(string id)
            {
                var b = new SkillConfigBuilder(start);
                Assert.IsTrue(b.TryApplyCatalog(data.upgrades.Find(c => c.id == id).effects), id);
                return b.Build();
            }
            var speed = Apply("lightning_cloud_speed").Stats;
            Assert.AreEqual(start.Stats.Area.MoveSpeed * 1.2f, speed.Area.MoveSpeed, .001f);
            Assert.AreEqual(start.Stats.Cast.Damage * 1.4f, speed.Cast.Damage, .001f);
            var magnet = Apply("lightning_cloud_magnet").Stats;
            Assert.AreEqual(start.Stats.Area.Pull * 1.25f, magnet.Area.Pull, .001f);
            Assert.AreEqual(2f, Apply("lightning_cloud_voltage").Stats.Status.ParalysisDuration);
            Assert.AreEqual(start.Stats.Area.Duration * 1.6f, Apply("lightning_cloud_duration").Stats.Area.Duration, .001f);
            Assert.AreEqual(start.Stats.Area.Radius * 1.5f, Apply("lightning_cloud_expand").Stats.Area.Radius, .001f);
        }

        [Test]
        public void GuideCard_CastsLightningOnHitWithFivePercentChance()
        {
            var data = Cloud();
            var fake = new FakeCaster();
            var builder = new SkillConfigBuilder(SkillConfig.FromDefinition(data).WithChildCaster(fake));
            Assert.IsTrue(builder.TryApplyCatalog(data.upgrades.Find(c => c.id == "lightning_cloud_guide").effects));
            var reactions = builder.Build().Reactions;

            var target = new HitRecorder { Position = new Vector2(1, 1) };
            var provider = new Provider(); provider.Targets.Add(target);
            var pool = ZonePool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(3, 10, 1), AttackReactions.Empty, reactions, () => .04f);
            Tick(zone, .1f);
            Assert.AreEqual(1, fake.Casts.Count, "5% 미만이면 시전한다");
            Assert.AreEqual(LightningId, fake.Casts[0].id);

            var zone2 = pool.Get();
            zone2.Init(pool, provider, Vector2.zero, new AreaSettings(3, 10, 1), AttackReactions.Empty, reactions, () => .05f);
            Tick(zone2, .1f);
            Assert.AreEqual(1, fake.Casts.Count, "5% 이상이면 시전하지 않는다");
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
                Assert.IsTrue(builder.TryApplyCatalog(data.upgrades.Find(c => c.id == "lightning_cloud_guide").effects));
                var pool = ZonePool();
                var zone = pool.Get();
                zone.Init(pool, provider, Vector2.zero, new AreaSettings(3, 10, 1), AttackReactions.Empty, builder.Build().Reactions, () => 0f);
                Assert.AreEqual(0, ActiveStrikes().Count);
                Tick(zone, .1f);
                Assert.AreEqual(1, ActiveStrikes().Count, "벼락이 실제로 하나 떨어진다");
                Assert.AreEqual(new Vector3(1, 1, 0), ActiveStrikes()[0].transform.position, "맞은 적의 위치에 떨어진다");
            }
            finally
            {
                children.Dispose();
                foreach (var strike in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None)) { Object.DestroyImmediate(strike.gameObject); }
            }
        }

        private static List<HitscanEffect> ActiveStrikes() => Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None)
            .Where(e => e.gameObject.scene.IsValid() && e.gameObject.activeInHierarchy).ToList();

        [Test]
        public void ShippedCatalog_CloudDisablesCardsWithUnknownEffectsAndRequiresLightningForGuide()
        {
            var data = Cloud();
            Assert.AreEqual(CastType.Area, data.castType);
            var disabled = data.upgrades.Where(c => !c.enabled).ToList();
            Assert.AreEqual(3, disabled.Count);
            foreach (var card in disabled) { Assert.IsNotEmpty(card.disabledReason, card.id); }
            var guide = data.upgrades.Find(c => c.id == "lightning_cloud_guide");
            CollectionAssert.AreEqual(new[] { LightningId }, guide.requiredWeaponIds);
            Assert.AreEqual(13, guide.minPermanentLevel);
        }
    }
}
