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
    /// <summary>영역 공격(서리 감옥): 영역 동작, 전략, 카드 효과, 자식 스킬 시전</summary>
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
            public readonly List<(int id, Vector2 position, float scale)> Casts = new List<(int, Vector2, float)>();
            public void Cast(int skillId, AttackContext context, float damageScale) => Casts.Add((skillId, context.Position, damageScale));
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
            zone.Init(pool, provider, Vector2.zero, 2, 3, 1, Damage(10), AttackReactions.Empty);

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
            zone.Init(pool, provider, Vector2.zero, 2, 1, 2, AttackReactions.Empty, reactions);

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
            zone.Init(pool, provider, Vector2.zero, 2, 3, 1, AttackReactions.Empty, reactions);
            for (int i = 0; i < 10; i++) { Tick(zone, .25f); }
            Assert.AreEqual(2, pulses, "2.5초 동안 1초 주기 반응은 두 번 난다");
        }

        // ── 전략과 카탈로그 ───────────────────────────────────────────

        private static WeaponData FrostPrison() =>
            new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == FrostPrisonId);

        private SkillCaster CreateCaster(WeaponData data, Provider provider, out GameObject prefab)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FrostPrisonPrefab);
            Assert.IsNotNull(asset, FrostPrisonPrefab);
            prefab = asset;
            var owner = new GameObject("AreaCasterOwner");
            created.Add(owner);
            return WeaponFactory.Create(data, asset, owner.transform, provider);
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
            Assert.IsTrue(builder.TryApplyCatalog(data.upgrades.Find(c => c.id == "frost_prison_expand").effects));
            Assert.IsTrue(builder.TryApplyCatalog(data.upgrades.Find(c => c.id == "frost_prison_cast").effects));
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
            Assert.IsTrue(caster.LevelUp(data.upgrades.Find(c => c.id == "frost_prison_shatter")));

            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(caster, null);
            var zone = Zones().Single();
            Tick(zone, 1f);
            Assert.AreEqual(0, fake.Casts.Count, "지속 시간 중에는 시전하지 않는다");
            Tick(zone, 5f);

            Assert.AreEqual(2, fake.Casts.Count);
            foreach (var cast in fake.Casts)
            {
                Assert.AreEqual(4, cast.id, "서리 결정(얼음창)을 자식으로 시전한다");
                Assert.AreEqual(new Vector2(1, 1), cast.position, "영역이 사라진 자리에서 시전한다");
                Assert.AreEqual(.5f, cast.scale);
            }
        }

        [Test]
        public void ShatterCard_EndToEnd_SpawnsRealFrostCrystalProjectilesFromExpiredZone()
        {
            const string IceSpearPrefab = "Assets/_Project/Prefabs/Skills/IceSpear.prefab";
            var data = FrostPrison();
            var all = new DefaultWeaponDataProvider().LoadAll();
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
                return WeaponFactory.Create(iceData, icePrefab, owner.transform, provider);
            });
            try
            {
                caster.UseChildCaster(children);
                Assert.IsTrue(caster.LevelUp(data.upgrades.Find(c => c.id == "frost_prison_shatter")));
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
            Assert.IsFalse(caster.LevelUp(data.upgrades.Find(c => c.id == "frost_prison_shatter")),
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
            var catalog = JsonConvert.DeserializeObject<List<WeaponData>>(Resources.Load<TextAsset>("MockData/Weapons").text);
            var weapon = catalog.Find(w => w.castType == CastType.Projectile);
            weapon.upgrades[0].effects.Add(new EffectDef { kind = "areaRadius", value = 10 });
            var error = Assert.Throws<InvalidOperationException>(() => WeaponCatalogValidator.Validate(catalog));
            StringAssert.Contains("areaRadius", error.Message);
        }

        [Test]
        public void DamageScale_ScalesDirectAndExplosionDamageOnly()
        {
            var config = SkillConfig.FromDefinition(new WeaponData
            {
                baseStats = new WeaponBaseStats { cast = { baseDamage = 10, cooldown = 2 }, explosion = { radius = 1, damageRatio = 1 } }
            });
            var scaled = config.WithDamageScale(.5f);
            Assert.AreEqual(5, scaled.Stats.Cast.Damage, .001f);
            Assert.AreEqual(5, scaled.Stats.Explosion.Damage, .001f);
            Assert.AreEqual(2, scaled.Stats.Cast.Cooldown);
            Assert.AreEqual(10, config.Stats.Cast.Damage, "원본 설정은 바뀌지 않는다");
        }
    }
}
