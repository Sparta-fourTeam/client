using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>냉기 지대의 빙결핵·절대 영도(중심부 추가 공격), 매서운 서리(빙결 중 최대 HP 비례 피해), 빙속성 연마(서리 결정과 공유)</summary>
    public sealed class ColdZoneCardTests
    {
        private const int FrostCrystalId = 4, ColdZoneId = 6;
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
        }

        private ObjectPool<AreaZone> Pool() => new ObjectPool<AreaZone>(
            () => { var go = new GameObject("ColdZoneTest"); created.Add(go); return go.AddComponent<AreaZone>(); },
            z => z.gameObject.SetActive(true), z => z.gameObject.SetActive(false));

        private static void Tick(AreaZone zone, float dt) => typeof(AreaZone).GetMethod("Tick", Flags).Invoke(zone, new object[] { dt });

        private static AttackReactions DamageAndFreeze() => new HitReactionBuilder().Damage(10).Freeze(2).Build();

        private (AreaZone zone, HitRecorder center, HitRecorder edge) Zone(AreaSettings settings)
        {
            var center = new HitRecorder { Position = new Vector2(.5f, 0) };
            var edge = new HitRecorder { Position = new Vector2(2.5f, 0) };
            var provider = new CatalogWorld.Targets();
            provider.All.AddRange(new[] { center, edge });
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, settings, DamageAndFreeze(), AttackReactions.Empty);
            return (zone, center, edge);
        }

        // ── 빙결핵·절대 영도: 영역이 시작될 때 중심부에 같은 적중을 한 번 더 ─────────────

        [Test(Description = "빙결핵: 첫 펄스 뒤 중심부(반경 1.2 이내)의 적만 같은 적중을 한 번 더 받는다")]
        public void Core_HitsOnlyTheCenterOnceMore()
        {
            var (zone, center, edge) = Zone(new AreaSettings(3, 3, 1, coreHits: 1, coreRadius: 1.2f));

            Tick(zone, .1f);

            Assert.AreEqual(20, center.Damage, "펄스 한 번 + 중심부 추가 한 번");
            Assert.AreEqual(10, edge.Damage, "중심부 밖은 펄스만 받는다");
        }

        [Test(Description = "빙결핵의 추가 공격은 영역이 시작될 때 한 번뿐이고 이후 펄스에는 붙지 않는다")]
        public void Core_HappensOnlyAtTheStart()
        {
            var (zone, center, _) = Zone(new AreaSettings(3, 3, 1, coreHits: 1, coreRadius: 1.2f));
            Tick(zone, .1f);

            Tick(zone, 1f);

            Assert.AreEqual(30, center.Damage);
        }

        [Test(Description = "절대 영도: 중심부 추가 공격의 빙결 지속이 2배다 (펄스의 빙결은 그대로)")]
        public void CoreFreeze_DoublesTheFreezeOfTheCoreHitOnly()
        {
            var (zone, center, edge) = Zone(new AreaSettings(3, 3, 1, coreHits: 1, coreRadius: 1.2f, coreFreezeScale: 2));

            Tick(zone, .1f);

            Assert.AreEqual(4f, center.Freeze, .0001f, "펄스의 빙결 2초 뒤 중심부 추가 공격이 4초로 덮는다");
            Assert.AreEqual(2f, edge.Freeze, .0001f);
        }

        [Test(Description = "빙결핵이 없으면(코어 횟수 0) 중심부 추가 공격이 없다")]
        public void NoCore_NoExtraHit()
        {
            var (zone, center, _) = Zone(new AreaSettings(3, 3, 1));

            Tick(zone, .1f);

            Assert.AreEqual(10, center.Damage);
        }

        // ── 매서운 서리: 빙결이 유지되는 동안 매초 최대 HP 비례 피해 ───────────────────

        private static EnemyStatus Status(out List<int> damages, StatusImmunity immunities = StatusImmunity.None, int maxHp = 1000)
        {
            var dealt = new List<int>();
            damages = dealt;
            return new EnemyStatus(maxHp, immunities, () => false, dealt.Add);
        }

        [Test(Description = "빙결 3초 동안 매초 최대 HP 1%(1000의 10)를 받고 빙결이 끝나면 멈춘다")]
        public void FreezeDot_DealsMaxHpRatioEverySecondWhileFrozen()
        {
            var status = Status(out var damages);
            status.ApplyFreeze(3);
            status.ApplyFreezeMaxHpDot(.01f);

            for (int i = 0; i < 5; i++) { status.Tick(1f); }

            CollectionAssert.AreEqual(new[] { 10, 10, 10 }, damages);
        }

        [Test(Description = "빙결이 걸려 있지 않으면 매서운 서리도 걸리지 않는다")]
        public void FreezeDot_IsIgnoredWhenNotFrozen()
        {
            var status = Status(out var damages);
            status.ApplyFreezeMaxHpDot(.01f);

            status.Tick(5f);

            Assert.IsEmpty(damages);
        }

        [Test(Description = "빙결에 면역이면 빙결이 걸리지 않으니 매서운 서리도 없다")]
        public void FreezeDot_IsIgnoredForFreezeImmuneTargets()
        {
            var status = Status(out var damages, StatusImmunity.Freeze);
            status.ApplyFreeze(3);
            status.ApplyFreezeMaxHpDot(.01f);

            status.Tick(3f);

            Assert.IsEmpty(damages);
        }

        [Test(Description = "한 번에 긴 시간이 지나도 빙결이 유지된 시간만큼만 피해를 준다")]
        public void FreezeDot_CountsOnlyTheFrozenTimeInOneLongTick()
        {
            var status = Status(out var damages);
            status.ApplyFreeze(2);
            status.ApplyFreezeMaxHpDot(.01f);

            status.Tick(10f);

            Assert.AreEqual(2, damages.Count);
        }

        // ── 카드 데이터 ────────────────────────────────────────────────

        [Test(Description = "냉기 지대 카드: 빙결핵은 중심부 추가 공격 1회, 절대 영도는 빙결 2배, 매서운 서리는 최대 HP 1%를 켠다")]
        public void Cards_EnableCoreFreezeAndMaxHpDotStats()
        {
            world = new CatalogWorld();
            var cold = world.Create(ColdZoneId);

            world.Take(cold, "cold_zone_extreme", "cold_zone_core", "cold_zone_zero", "cold_zone_bitter");

            Assert.AreEqual(1, cold.Stats.Area.CoreHits);
            Assert.AreEqual(2f, cold.Stats.Area.CoreFreezeScale);
            Assert.AreEqual(.01f, cold.Stats.Status.FreezeMaxHpRatio, .0001f);
        }

        [Test(Description = "빙속성 연마는 냉기 지대와 서리 결정에 같은 공유 카드로 있고, 한 번 고르면 두 스킬의 쿨타임이 25% 줄어든다")]
        public void Polish_IsASharedCardThatCutsBothCooldowns()
        {
            world = new CatalogWorld();
            var cold = world.Create(ColdZoneId);
            var frost = world.Create(FrostCrystalId);
            var coldBefore = cold.Stats.Cast.Cooldown;
            var frostBefore = frost.Stats.Cast.Cooldown;
            var option = cold.Data.upgrades.Find(c => c.id == "cold_zone_polish");

            Assert.IsTrue(option.enabled);
            Assert.AreEqual(option.sharedId, frost.Data.upgrades.Find(c => c.id == "frost_crystal_polish").sharedId);
            Assert.IsTrue(SkillUpgradeTransaction.TryApply(cold, option, new[] { (SkillBase)cold, frost }, 21));

            Assert.AreEqual(coldBefore * .75f, cold.Stats.Cast.Cooldown, .0001f);
            Assert.AreEqual(frostBefore * .75f, frost.Stats.Cast.Cooldown, .0001f);
        }
    }
}
