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
    /// <summary>광선(에너지 빔): 선 모양 판정, 공격 횟수, 겨눈 적 추적, 카드 효과, 자식 광선</summary>
    public sealed class BeamSkillTests
    {
        private const int SunBeamId = 16;
        private const int FocusBeamId = 19;
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
            var settings = AreaSettings.From(SkillStats.FromDefinition(new SkillBaseStats
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

        // ── 에너지 빔 카탈로그 ────────────────────────────────────────

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

        // ── 집중 광선 ─────────────────────────────────────────────────

        [Test(Description = "광선이 겨눈 메인 대상만 공격마다 추가 피해를 받고 선 위의 다른 적은 그대로다")]
        public void FocusBonus_OnlyTheAimedMainTargetTakesExtraDamage()
        {
            var main = new HitRecorder { Position = new Vector2(4, 0) };
            var other = new HitRecorder { Position = new Vector2(7, 0) };
            var provider = new CatalogWorld.Targets();
            provider.All.AddRange(new[] { main, other });
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 1, 1, length: 10, width: 1, focusBonus: 1f, baseDamage: 10), Damage(10),
                AttackReactions.Empty, aimTarget: main);

            Tick(zone, .1f);

            Assert.AreEqual(20, main.Damage, "공격력 10 + 메인 추가 100%");
            Assert.AreEqual(10, other.Damage);
        }

        private sealed class ImpactCounter : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public int Impacts, Total;
            public void TakeDamage(int amount) => Total += amount;
            public void TakeDamage(Game.Core.Combat.DamageInfo info) { Total += info.Amount; if (info.IsImpact) { Impacts++; } }
        }

        [Test(Description = "메인 대상 추가 피해는 같은 적중의 일부라 충격 피해로 세지 않는다 (타격 횟수형 방어막이 두 번으로 세지 않게)")]
        public void FocusBonus_IsNotCountedAsASecondImpactHit()
        {
            var main = new ImpactCounter { Position = new Vector2(4, 0) };
            var provider = new CatalogWorld.Targets();
            provider.All.Add(main);
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 1, 1, length: 10, width: 1, focusBonus: 1f, baseDamage: 10), Damage(10),
                AttackReactions.Empty, aimTarget: main);

            Tick(zone, .1f);

            Assert.AreEqual(20, main.Total, "공격력 10 + 메인 추가 100%");
            Assert.AreEqual(1, main.Impacts, "광선 한 번의 적중은 충격 한 번이다");
        }

        [Test(Description = "집중 광선: 초점 카드를 얻으면 메인 대상이 공격력의 두 배를 받는다")]
        public void FocusBeam_FocusCardDoublesDamageOnTheMainTarget()
        {
            world = new CatalogWorld();
            var beam = world.Create(FocusBeamId);
            world.Take(beam, "focus_beam_focus");
            var main = world.AddEnemy(4, 0);
            var other = world.AddEnemy(8, 0);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(beam, null);
            var zone = Zones().Single();

            Tick(zone, .1f);

            Assert.AreEqual(CastType.Beam, world.Data[FocusBeamId].castType);
            Assert.AreEqual(6, main.Damage, "기본 피해 3 + 메인 추가 100%");
            Assert.AreEqual(0, other.Damage, "집중 광선은 메인 대상에서 끝나 그 뒤쪽은 맞지 않는다");
        }

        [Test(Description = "초점 조정: 메인 대상을 맞힐 때마다 추가 피해가 광선이 끝날 때 최대값까지 선형으로 늘고 다른 적은 그대로다")]
        public void FocusRamp_MainTargetExtraDamageGrowsEachPulse()
        {
            var main = new HitRecorder { Position = new Vector2(4, 0) };
            var other = new HitRecorder { Position = new Vector2(7, 0) };
            var provider = new CatalogWorld.Targets();
            provider.All.AddRange(new[] { main, other });
            var pool = Pool();
            var zone = pool.Get();
            // 지속 3초에 1초 간격 3번, 최대 +150%: 추가 피해는 50% → 100% → 150%
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 3, 1, length: 10, width: 1, baseDamage: 10, focusRampMax: 1.5f), Damage(10),
                AttackReactions.Empty, aimTarget: main);

            Tick(zone, .1f);
            Assert.AreEqual(10 + 5, main.Damage);
            Tick(zone, 1f);
            Assert.AreEqual(15 + 10 + 10, main.Damage);
            Tick(zone, 1f);
            Assert.AreEqual(35 + 10 + 15, main.Damage);
            Assert.AreEqual(30, other.Damage, "메인 대상이 아닌 적은 기본 피해만 받는다");
        }

        [Test(Description = "집중 폭파: 메인 대상을 맞힐 때마다 주변 적이 폭발 피해를 받고 반경 밖은 받지 않는다")]
        public void FocusBlast_ExplodesAroundTheMainTargetOnly()
        {
            var main = new HitRecorder { Position = new Vector2(4, 0) };
            var neighbor = new HitRecorder { Position = new Vector2(4, 1.5f) };   // 광선 폭 밖, 폭발 반경 안
            var far = new HitRecorder { Position = new Vector2(4, 5) };
            var provider = new CatalogWorld.Targets();
            provider.All.AddRange(new[] { main, neighbor, far });
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 1, 1, length: 10, width: 1, baseDamage: 10, focusBlastRadius: 2, focusBlastRatio: .5f), Damage(10),
                AttackReactions.Empty, aimTarget: main);

            Tick(zone, .1f);

            Assert.AreEqual(5, neighbor.Damage, "공격 한 번 피해 10의 50%");
            Assert.AreEqual(0, far.Damage);
            Assert.AreEqual(10 + 5, main.Damage, "메인 대상도 폭발 안에 있다");
        }

        [Test(Description = "집중 광선: 집중 폭파와 초점 조정 카드는 초점을 선행으로 하고 스탯을 켠다")]
        public void FocusBeam_BlastAndAdjustCardsRequireFocusAndEnableTheirStats()
        {
            world = new CatalogWorld();
            var beam = world.Create(FocusBeamId);
            var cards = world.Data[FocusBeamId].upgrades;
            foreach (var id in new[] { "focus_beam_blast", "focus_beam_adjust" })
            {
                Assert.AreEqual("focus_beam_focus", cards.Find(c => c.id == id).requiredCardCounts.Single().cardId);
            }
            Assert.AreEqual(13, cards.Find(c => c.id == "focus_beam_adjust").minPermanentLevel);

            world.Take(beam, "focus_beam_focus", "focus_beam_blast", "focus_beam_adjust");

            Assert.AreEqual(1.5f, beam.Stats.Beam.FocusBlastRadius);
            Assert.AreEqual(1.5f, beam.Stats.Beam.FocusRampMax, .0001f);
        }

        private (AreaZone zone, HitRecorder main, HitRecorder bend, HitRecorder second, HitRecorder beyond) Refracted(int refractions)
        {
            var main = new HitRecorder { Position = new Vector2(4, 0) };
            var bend = new HitRecorder { Position = new Vector2(4, 2.5f) };       // 메인 대상에서 가장 가까운 다른 적: 첫 굴절 끝
            var second = new HitRecorder { Position = new Vector2(8, 2.5f) };      // 첫 굴절 끝에서 가장 가까운 적: 두 번째 굴절 끝
            var beyond = new HitRecorder { Position = new Vector2(9, 0) };         // 곧게 뻗었다면 맞았을 메인 대상 뒤쪽
            var provider = new CatalogWorld.Targets();
            provider.All.AddRange(new[] { main, bend, second, beyond });
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 1, 1, length: 14, width: 1, refractions: refractions), Damage(10),
                AttackReactions.Empty, aimTarget: main);
            return (zone, main, bend, second, beyond);
        }

        [Test(Description = "굴절 1: 광선이 메인 대상에서 끝나 가까운 다른 적으로 꺾이고, 메인 대상 뒤쪽과 굴절 횟수를 넘은 적은 맞지 않는다")]
        public void Refraction_BendsAtTheMainTargetTowardTheNearestOtherEnemy()
        {
            var (zone, main, bend, second, beyond) = Refracted(1);

            Tick(zone, .1f);

            Assert.AreEqual(10, main.Damage);
            Assert.AreEqual(10, bend.Damage);
            Assert.AreEqual(0, second.Damage, "굴절 횟수를 넘은 적은 맞지 않는다");
            Assert.AreEqual(0, beyond.Damage, "굴절하면 메인 대상 뒤로 뻗지 않는다");
        }

        [Test(Description = "굴절 2: 두 번째 굴절 끝의 적까지 이어진다")]
        public void Refraction_TwoBendsReachTheSecondEnemy()
        {
            var (zone, main, bend, second, beyond) = Refracted(2);

            Tick(zone, .1f);

            Assert.AreEqual(10, main.Damage);
            Assert.AreEqual(10, bend.Damage);
            Assert.AreEqual(10, second.Damage);
            Assert.AreEqual(0, beyond.Damage);
        }

        [Test(Description = "겨눈 적이 죽으면 굴절 없이 마지막 방향으로 곧게 뻗는다")]
        public void Refraction_FallsBackToStraightWhenTheMainTargetIsDead()
        {
            var (zone, main, bend, _, beyond) = Refracted(1);
            main.Dead = true;

            Tick(zone, .1f);

            Assert.AreEqual(10, beyond.Damage, "곧게 뻗어 선 위의 적을 맞힌다");
            Assert.AreEqual(0, bend.Damage);
        }

        [Test(Description = "굴절이 없으면 지금처럼 곧게 뻗어 메인 대상 뒤쪽도 맞는다")]
        public void Refraction_NoneKeepsTheStraightBeam()
        {
            var (zone, _, bend, _, beyond) = Refracted(0);

            Tick(zone, .1f);

            Assert.AreEqual(10, beyond.Damage);
            Assert.AreEqual(0, bend.Damage);
        }

        [Test(Description = "집중 굴절은 집중 증폭을 선행으로 하고 굴절 횟수를 늘린다 (최대 2)")]
        public void FocusBeam_RefractCardRequiresAmplifyAndAddsRefraction()
        {
            world = new CatalogWorld();
            var beam = world.Create(FocusBeamId);
            var card = world.Data[FocusBeamId].upgrades.Find(c => c.id == "focus_beam_refract");
            Assert.AreEqual("focus_beam_amplify", card.requiredCardCounts.Single().cardId);
            Assert.AreEqual(2, card.maxPickCount);

            world.Take(beam, "focus_beam_amplify", "focus_beam_refract", "focus_beam_refract");

            Assert.AreEqual(2, beam.Stats.Beam.Refractions);
        }

        // ── 집중 광선 방식: 메인까지가 길이, 메인이 죽으면 가장 가까운 적으로 교체 ───────────────

        private (AreaZone zone, CatalogWorld.Targets provider) FocusZone(float rampMax = 0, params HitRecorder[] enemies)
        {
            var provider = new CatalogWorld.Targets();
            provider.All.AddRange(enemies);
            var pool = Pool();
            var zone = pool.Get();
            zone.Init(pool, provider, Vector2.zero, new AreaSettings(0, 3, 1, length: 14, width: 1, baseDamage: 10, focusRampMax: rampMax, focusAim: true), Damage(10),
                AttackReactions.Empty, aimTarget: enemies.FirstOrDefault());
            return (zone, provider);
        }

        [Test(Description = "광선 길이는 메인 대상까지의 거리다: 메인에서 끝나고 뒤쪽은 맞지 않는다")]
        public void FocusAim_BeamEndsAtTheMainTarget()
        {
            var main = new HitRecorder { Position = new Vector2(4, 0) };
            var behind = new HitRecorder { Position = new Vector2(9, 0) };
            var between = new HitRecorder { Position = new Vector2(2, 0) };
            var (zone, _) = FocusZone(0, main, behind, between);

            Tick(zone, .1f);

            Assert.AreEqual(10, main.Damage);
            Assert.AreEqual(10, between.Damage, "메인까지의 선 위의 적은 맞는다");
            Assert.AreEqual(0, behind.Damage);
        }

        [Test(Description = "메인이 죽으면 거리와 상관없이 가장 가까운 살아 있는 적이 새 메인이 되고 광선이 그쪽으로 간다")]
        public void FocusAim_MainDiesAndTheNearestAliveEnemyBecomesMain()
        {
            var first = new HitRecorder { Position = new Vector2(4, 0) };
            var next = new HitRecorder { Position = new Vector2(4, 30) };          // 광선 길이(14) 밖이어도 새 메인이 된다
            var farther = new HitRecorder { Position = new Vector2(-6, 40) };
            var (zone, _) = FocusZone(0, first, next, farther);
            Tick(zone, .1f);
            first.Dead = true;

            Tick(zone, 1f);

            Assert.AreEqual(10, next.Damage, "가장 가까운 살아 있는 적이 새 메인이다");
            Assert.AreEqual(0, farther.Damage);
            Assert.AreEqual(10, first.Damage, "죽은 적은 더 맞지 않는다");
        }

        [Test(Description = "살아 있는 적이 없으면 광선이 사라지고 시전 시간은 흐르며, 적이 나타나면 다시 생겨 그 적이 메인이 된다")]
        public void FocusAim_NoEnemyMakesTheBeamDisappearAndItComesBackWhenOneAppears()
        {
            var only = new HitRecorder { Position = new Vector2(4, 0) };
            var (zone, provider) = FocusZone(0, only);
            Tick(zone, .1f);
            only.Dead = true;

            Tick(zone, 1f);
            Assert.AreEqual(10, only.Damage, "광선이 사라진 동안에는 아무도 맞지 않는다");

            var arrived = new HitRecorder { Position = new Vector2(3, 3) };
            provider.All.Add(arrived);
            Tick(zone, 1f);

            Assert.AreEqual(10, arrived.Damage, "나타난 적이 새 메인이 되어 맞는다");
        }

        [Test(Description = "새 메인은 초점 조정의 누적을 처음부터 쌓는다")]
        public void FocusAim_NewMainRestartsTheRamp()
        {
            var first = new HitRecorder { Position = new Vector2(4, 0) };
            var second = new HitRecorder { Position = new Vector2(4, 8) };
            var (zone, _) = FocusZone(1.5f, first, second);   // 지속 3초 / 1초 간격 3번: 추가 피해 +50% → +100% → +150%

            Tick(zone, .1f);
            Assert.AreEqual(15, first.Damage, "첫 공격: 10 + 50%");
            first.Dead = true;
            Tick(zone, 1f);

            Assert.AreEqual(15, second.Damage, "새 메인의 첫 공격도 +50%부터다 (이어받으면 +100%인 20)");
        }

        [Test(Description = "집중 광선(데이터)은 집중 광선 방식이라 메인 대상 뒤쪽을 맞히지 않고, 에너지 빔은 그대로 뒤쪽까지 뚫는다")]
        public void FocusBeam_EndsAtTheMainTargetWhileSunBeamKeepsPiercing()
        {
            world = new CatalogWorld();
            var focus = world.Create(FocusBeamId);
            var sun = world.Create(SunBeamId);
            var main = world.AddEnemy(4, 0);
            var behind = world.AddEnemy(9, 0);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(focus, null);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(sun, null);
            foreach (var zone in Zones()) { Tick(zone, .1f); }

            Assert.IsTrue(world.Data[FocusBeamId].baseStats.beam.focusAim > 0);
            Assert.AreEqual(3 + 5, main.Damage, "집중 광선 3 + 에너지 빔 5");
            Assert.AreEqual(5, behind.Damage, "뒤쪽은 에너지 빔만 맞는다");
        }

        [Test(Description = "연속 광선은 공격 횟수를 15 늘린다 (지속 시간은 그대로)")]
        public void FocusBeam_ContinuousCardAddsPulses()
        {
            world = new CatalogWorld();
            var beam = world.Create(FocusBeamId);
            var before = beam.Stats.Beam.Pulses;
            world.Take(beam, "focus_beam_continuous");

            Assert.AreEqual(before + 15, beam.Stats.Beam.Pulses);
            Assert.AreEqual(2f, beam.Stats.Beam.Duration);
        }

        [Test]
        public void Cards_StatusEffectsReachBeamTargets()
        {
            var beam = SunBeam("energy_beam_split", "energy_beam_dazzle", "energy_beam_weaken", "energy_beam_freeze");
            var aim = world.AddEnemy(4, 0);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(beam, null);
            var zone = Zones().Single();
            var probe = new HitRecorder { Position = new Vector2(5, 0) };
            world.Provider.All.Add(probe);
            Tick(zone, .1f);
            Assert.AreEqual(.25f, probe.VulnerabilityRatio, .001f);
            Assert.AreEqual(5, probe.VulnerabilityDuration);
            Assert.AreEqual(.35f, probe.SlowRatio, .001f);
            Assert.AreEqual(1, probe.SlowDuration);
            Assert.AreEqual(.5f, probe.Freeze, .001f);
        }

        [Test]
        public void DazzleCard_StunsWithFivePercentChance()
        {
            var beam = SunBeam("energy_beam_dazzle");
            world.AddEnemy(4, 0);
            var config = beam.Config.Stats.Status;
            Assert.AreEqual(.05f, config.StunChance, .0001f);
            Assert.AreEqual(1f, config.StunDuration);
        }

        [Test]
        public void FireCard_CastsAnAuxiliaryBeamAtTheStartOfTheMainBeam()
        {
            var beam = SunBeam("energy_beam_overload", "energy_beam_amplify", "energy_beam_fire");
            world.AddEnemy(4, 0);
            typeof(SkillCaster).GetMethod("OnFire", Flags).Invoke(beam, null);
            Assert.AreEqual(2, Zones().Count, "본 광선과 보조 광선이 함께 나간다");
        }
    }
}
