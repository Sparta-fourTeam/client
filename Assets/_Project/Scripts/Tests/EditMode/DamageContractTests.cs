using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // 스킬의 속성과 시전 형태가 피해에 실리는 계약: SkillData.element, DamageSource, DamageInfo, 관통 차단 접점
    public sealed class DamageContractTests
    {
        // 피해 정보를 기록하는 대상. 관통을 막을 수 있다
        private sealed class FakeTarget : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public bool BlocksPierce { get; set; }
            public readonly List<DamageInfo> Infos = new();

            public void TakeDamage(int amount) => Infos.Add(new DamageInfo(amount));
            public void TakeDamage(DamageInfo info) => Infos.Add(info);
        }

        private sealed class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new();

            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.AddRange(Targets);
                return results.Count;
            }
        }

        private static void Tick(Projectile projectile, float deltaTime) =>
            typeof(Projectile).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(projectile, new object[] { deltaTime });

        // ───────── DamageInfo 오버로드 ─────────

        [TearDown]
        public void ResetCriticalHit() => CriticalHit.Reset();

        [Test(Description = "스킬 피해는 확률에 걸리면 배율만큼 늘고 치명타 표시가 남아 피해 파이프라인을 지나도 유지된다. 걸리지 않으면 그대로다")]
        public void CriticalHit_ScalesSkillDamageOnlyWhenRollSucceeds()
        {
            CriticalHit.Configure(0.25f, 2f);
            using (DamageAttribution.Begin(new DamageSource(7)))
            {
                CriticalHit.Roller = () => 0.24f; // 확률 안
                var hit = DamageInfo.FromCurrent(100);
                Assert.IsTrue(hit.IsCritical);
                Assert.AreEqual(200, hit.Amount);
                Assert.IsTrue(hit.WithAmount(150).IsCritical, "저항으로 피해가 줄어도 치명타 표시는 남아야 팝업이 구분한다");

                CriticalHit.Roller = () => 0.25f; // 확률 밖(경계)
                var normal = DamageInfo.FromCurrent(100);
                Assert.IsFalse(normal.IsCritical);
                Assert.AreEqual(100, normal.Amount);
            }
        }

        [Test(Description = "스킬 밖의 피해(상태이상 지속 피해 등)는 확률이 100%여도 치명타가 나지 않는다")]
        public void CriticalHit_NeverAppliesOutsideSkillDamage()
        {
            CriticalHit.Configure(1f, 3f);
            CriticalHit.Roller = () => 0f;

            var dot = DamageInfo.FromCurrent(40);

            Assert.IsFalse(dot.IsCritical);
            Assert.AreEqual(40, dot.Amount);
        }

        [Test(Description = "데미지 숫자 종류: 치명타 > 스킬 일반 > 스킬 밖 상태이상")]
        public void DamagePopupKinds_SeparatesCriticalNormalAndStatus()
        {
            Assert.AreEqual(DamagePopupKind.Critical, DamagePopupKinds.Of(new DamageInfo(10, skillId: 3, isCritical: true)));
            Assert.AreEqual(DamagePopupKind.Normal, DamagePopupKinds.Of(new DamageInfo(10, skillId: 3)));
            Assert.AreEqual(DamagePopupKind.Status, DamagePopupKinds.Of(new DamageInfo(10)));
        }

        // ───────── 피해 반응이 충격 여부를 싣는다 ─────────

        private static DamageInfo RaiseReaction(DamageReaction reaction, FakeTarget target, DamageSource source)
        {
            var reactions = new HitReactionBuilder().On(AttackEvent.Hit, reaction).Build();
            using (DamageAttribution.Begin(source))
            {
                reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up, target));
            }

            return target.Infos.Single();
        }

        [Test]
        public void DamageReaction_MarksImpactOnlyWhenAsked()
        {
            var source = new DamageSource(3, Element.Lightning, CastType.Instant);

            var impact = RaiseReaction(new DamageReaction(5, isImpact: true), new FakeTarget(), source);
            var extra = RaiseReaction(new DamageReaction(5), new FakeTarget(), source);

            Assert.IsTrue(impact.IsImpact);
            Assert.IsFalse(extra.IsImpact);
            Assert.AreEqual(Element.Lightning, impact.Element);
            Assert.AreEqual(CastType.Instant, impact.CastType);
        }

        // ───────── 투사체 적중이 속성을 싣는다 ─────────

        private static Projectile CreateProjectile(GameObject go, IEnemyTargetProvider provider, int pierceCount, DamageSource source)
        {
            var projectile = go.AddComponent<Projectile>();
            var pool = new UnityEngine.Pool.ObjectPool<Projectile>(() => projectile);
            using (DamageAttribution.Begin(source))
            {
                pool.Get().Init(pool, Vector3.zero, Vector3.right, 10, 10, 3, provider, pierceCount: pierceCount);
            }

            return projectile;
        }

        [Test]
        public void Projectile_HitCarriesSkillElementAndImpact()
        {
            var go = new GameObject("ProjectileElementTest");
            try
            {
                var target = new FakeTarget { Position = Vector2.right * 0.5f };
                var provider = new Provider();
                provider.Targets.Add(target);
                var projectile = CreateProjectile(go, provider, 0, new DamageSource(2, Element.Fire, CastType.Projectile));

                Tick(projectile, 0.1f);

                var info = target.Infos.Single();
                Assert.AreEqual(10, info.Amount);
                Assert.AreEqual(Element.Fire, info.Element);
                Assert.AreEqual(CastType.Projectile, info.CastType);
                Assert.IsTrue(info.IsImpact);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ───────── 관통 차단 ─────────

        [Test(Description = "관통을 막는 대상에 맞으면 남은 관통이 있어도 그 대상에서 멈춘다")]
        public void BlockingTarget_StopsPiercingProjectile()
        {
            var go = new GameObject("PierceBlockTest");
            try
            {
                var blocker = new FakeTarget { Position = Vector2.right * 0.5f, BlocksPierce = true };
                var behind = new FakeTarget { Position = Vector2.right * 0.7f };
                var provider = new Provider();
                provider.Targets.Add(blocker);
                provider.Targets.Add(behind);
                var projectile = CreateProjectile(go, provider, 2, default);

                Tick(projectile, 0.1f);

                Assert.AreEqual(1, blocker.Infos.Count, "막는 대상은 맞는다");
                Assert.IsEmpty(behind.Infos, "관통하지 못해 뒤의 적은 맞지 않는다");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ───────── 스킬 데이터 ─────────

        private static List<SkillData> LoadSkills() => new DefaultSkillDataProvider(new GameDataStore()).LoadAll();

        [Test]
        public void Validator_RejectsUndefinedElement()
        {
            var skills = LoadSkills();
            skills[0].element = (Element)99;

            var error = Assert.Throws<InvalidOperationException>(() => SkillCatalogValidator.Validate(skills));

            StringAssert.Contains("속성", error.Message);
        }
    }
}
