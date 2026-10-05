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
    /// <summary>광선(태양 광선): 선 모양 판정, 공격 횟수, 겨눈 적 추적, 카드 효과, 자식 광선</summary>
    public sealed class BeamSkillTests
    {
        private const int SunBeamId = 16;
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
            foreach (var zone in Object.FindObjectsByType<AreaZone>(FindObjectsSortMode.None)) { Object.DestroyImmediate(zone.gameObject); }
        }

        private ObjectPool<AreaZone> Pool() => new ObjectPool<AreaZone>(
            () => { var go = new GameObject("BeamZoneTest"); created.Add(go); return go.AddComponent<AreaZone>(); },
            z => z.gameObject.SetActive(true), z => z.gameObject.SetActive(false));

        private static void Tick(AreaZone zone, float dt) => typeof(AreaZone).GetMethod("Tick", Flags).Invoke(zone, new object[] { dt });

        private static AttackReactions Damage(float d) => new HitReactionBuilder().Damage(d).Build();

        // ── 선 모양 판정 ──────────────────────────────────────────────

        [Test]
        public void Beam_HitsEveryEnemyTouchingTheLineAndNoOthers()
        {
            var aim = new HitRecorder { Position = new Vector2(4, 0) };
            var beyondAim = new HitRecorder { Position = new Vector2(9, .3f) };       // 대상 뒤쪽, 선 위
            var offLine = new HitRecorder { Position = new Vector2(4, 2) };           // 폭 밖
            var behind = new HitRecorder { Position = new Vector2(-3, 0) };           // 시전 위치 뒤
            var tooFar = new HitRecorder { Position = new Vector2(14, 0) };           // 길이 밖
            var provider = new CatalogWorld.Targets();
            provider.All.AddRange(new[] { aim, beyondAim, offLine, behind, tooFar });
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 1, 1, length: 10, width: 1), Damage(10), AttackReactions.Empty, aimTarget: aim);

            Tick(zone, .1f);

            Assert.AreEqual(10, aim.Damage);
            Assert.AreEqual(10, beyondAim.Damage, "광선에 닿는 모든 적을 공격한다");
            Assert.AreEqual(0, offLine.Damage);
            Assert.AreEqual(0, behind.Damage);
            Assert.AreEqual(0, tooFar.Damage);
        }

        [Test]
        public void Beam_PulsesTheConfiguredNumberOfTimesOverItsDuration()
        {
            var aim = new HitRecorder { Position = new Vector2(3, 0) };
            var provider = new CatalogWorld.Targets(); provider.All.Add(aim);
            var pool = Pool();
            var zone = pool.Get();
            var settings = AreaSettings.From(WeaponStats.FromDefinition(new WeaponBaseStats
            {
                beam = { length = 10, width = 1, duration = 2, pulses = 4 }
            }).Beam);
            zone.Init(pool, provider, Vector2.zero, settings, Damage(1), AttackReactions.Empty, aimTarget: aim);
            for (int i = 0; i < 20; i++) { Tick(zone, .1f); }
            Assert.AreEqual(4, aim.Damage, "2초 동안 4번");
            Assert.IsFalse(zone.gameObject.activeSelf, "지속 시간이 끝나면 사라진다");
        }

        [Test]
        public void Beam_FollowsTheAimedEnemyWhileAlive()
        {
            var aim = new HitRecorder { Position = new Vector2(5, 0) };
            var bystander = new HitRecorder { Position = new Vector2(0, 5) };
            var provider = new CatalogWorld.Targets(); provider.All.Add(aim); provider.All.Add(bystander);
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 5, 1, length: 10, width: 1), Damage(1), AttackReactions.Empty, aimTarget: aim);
            Tick(zone, .1f);
            Assert.AreEqual(0, bystander.Damage);
            aim.Position = new Vector2(0, 6);          // 겨눈 적이 위쪽으로 옮겨 간다
            Tick(zone, 1f);
            Assert.AreEqual(1, bystander.Damage, "광선이 겨눈 적을 따라가 그 방향의 적도 맞는다");
        }

        [Test]
        public void Beam_RaisesStartHitAndExpiredWithTheBeamDirection()
        {
            var aim = new HitRecorder { Position = new Vector2(0, 4) };
            var provider = new CatalogWorld.Targets(); provider.All.Add(aim);
            var directions = new List<Vector3>();
            var events = new List<AttackEvent>();
            var reactions = new HitReactionBuilder()
                .On(AttackEvent.Start, new CastSkillReaction(c => { events.Add(AttackEvent.Start); directions.Add(c.Direction); }))
                .On(AttackEvent.Hit, new CastSkillReaction(c => { events.Add(AttackEvent.Hit); directions.Add(c.Direction); }))
                .On(AttackEvent.Expired, new CastSkillReaction(c => events.Add(AttackEvent.Expired))).Build();
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 1, 2, length: 10, width: 1), AttackReactions.Empty, reactions, aimTarget: aim);
            Tick(zone, .5f); Tick(zone, .5f);
            Assert.AreEqual(new[] { AttackEvent.Start, AttackEvent.Hit, AttackEvent.Expired }, events);
            foreach (var d in directions) { Assert.AreEqual(0f, Vector3.Angle(Vector3.up, d), .5f); }
        }

        // ── 태양 광선 카탈로그 ────────────────────────────────────────

        private static List<AreaZone> Zones() => Object.FindObjectsByType<AreaZone>(FindObjectsSortMode.None)
            .Where(z => z.gameObject.scene.IsValid() && z.gameObject.activeInHierarchy).ToList();

        private SkillCaster SunBeam(params string[] cards)
        {
            world = new CatalogWorld();
            var beam = world.Create(SunBeamId);
            world.Take(beam, cards);
            return beam;
        }

        [Test]
        public void SunBeam_IsABeamSkillThatHitsEnemiesAlongTheLine()
        {
            var beam = SunBeam();
            Assert.AreEqual(CastType.Beam, world.Data[SunBeamId].castType);
            var near = world.AddEnemy(4, 0);
            var farther = world.AddEnemy(10, 0);
            var side = world.AddEnemy(4, 3);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(beam, null);
            var zone = Zones().Single();
            for (int i = 0; i < 20; i++) { Tick(zone, .1f); }
            Assert.AreEqual(4 * 5, near.Damage, "공격 횟수 4번 × 기본 피해 5");
            Assert.AreEqual(4 * 5, farther.Damage, "대상 뒤쪽의 적도 맞는다");
            Assert.AreEqual(0, side.Damage);
        }

        [Test]
        public void Cards_OverloadDoublesPulsesAndRaisesCooldown_StableAddsFlatPulses_SpreadWidensBeam()
        {
            var start = SkillConfig.FromDefinition(new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == SunBeamId)).Stats;
            var beam = SunBeam("sun_beam_overload", "sun_beam_stable", "sun_beam_amplify", "sun_beam_spread");
            var stats = beam.Stats;
            Assert.AreEqual((start.Beam.Pulses * 2) + 5, stats.Beam.Pulses, .001f, "공격 횟수 +100% 뒤 +5");
            Assert.AreEqual(start.Cast.Cooldown * 1.5f, stats.Cast.Cooldown, .001f, "쿨타임 +50%");
            Assert.AreEqual(start.Beam.Width * 2.5f, stats.Beam.Width, .001f, "범위 +150%");
            Assert.AreEqual(start.Cast.Damage * 1.6f, stats.Cast.Damage, .001f);
        }

        [Test]
        public void Cards_StatusEffectsReachBeamTargets()
        {
            var beam = SunBeam("sun_beam_split", "sun_beam_dazzle", "sun_beam_weaken", "sun_beam_freeze");
            var aim = world.AddEnemy(4, 0);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(beam, null);
            var zone = Zones().Single();
            var probe = new HitRecorder { Position = new Vector2(5, 0) };
            world.Provider.All.Add(probe);
            Tick(zone, .1f);
            Assert.AreEqual(.25f, probe.VulnerabilityRatio, .001f);
            Assert.AreEqual(5, probe.VulnerabilityDuration);
            Assert.AreEqual(.8f, probe.SlowRatio, .001f);
            Assert.AreEqual(1, probe.SlowDuration);
            Assert.AreEqual(.5f, probe.Freeze, .001f);
        }

        [Test]
        public void DazzleCard_StunsWithFivePercentChance()
        {
            var beam = SunBeam("sun_beam_dazzle");
            world.AddEnemy(4, 0);
            var config = beam.Config.Stats.Status;
            Assert.AreEqual(.05f, config.StunChance, .0001f);
            Assert.AreEqual(1f, config.StunDuration);
        }

        [Test]
        public void FireCard_CastsAnAuxiliaryBeamAtTheStartOfTheMainBeam()
        {
            var beam = SunBeam("sun_beam_overload", "sun_beam_amplify", "sun_beam_fire");
            world.AddEnemy(4, 0);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(beam, null);
            Assert.AreEqual(2, Zones().Count, "본 광선과 보조 광선이 함께 나간다");
        }

        [Test]
        public void ShippedCatalog_SunBeamHasFiveDisabledCardsWithReasons()
        {
            var data = new DefaultWeaponDataProvider().LoadAll().Find(w => w.id == SunBeamId);
            var disabled = data.upgrades.Where(c => !c.enabled).ToList();
            Assert.AreEqual(5, disabled.Count);
            foreach (var card in disabled) { Assert.IsNotEmpty(card.disabledReason, card.id); }
            CollectionAssert.AreEqual(new[] { 4 }, data.upgrades.Find(c => c.id == "sun_beam_freeze").requiredWeaponIds);
        }

        [Test]
        public void Registry_BeamEffectsAreBeamOnlyAndSharedEffectsIncludeBeam()
        {
            Assert.IsTrue(EffectRegistry.Supports(CastType.Beam, "beamWidth"));
            Assert.IsFalse(EffectRegistry.Supports(CastType.Area, "beamWidth"));
            Assert.IsFalse(EffectRegistry.Supports(CastType.Beam, "areaRadius"));
            Assert.IsFalse(EffectRegistry.Supports(CastType.Beam, "pierceCount"));
            foreach (var kind in new[] { "damage", "attackSpeed", "freezeDuration", "slowDuration", "vulnerabilityRatio", "stunDuration", "stunChance", "onEvent" })
            {
                Assert.IsTrue(EffectRegistry.Supports(CastType.Beam, kind), kind);
            }
            Assert.IsTrue(WeaponFactory.IsRegistered(CastType.Beam));
        }
    }
}
