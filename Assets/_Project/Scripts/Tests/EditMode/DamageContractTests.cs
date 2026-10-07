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

        // 피해량만 받는 대상. DamageInfo 오버로드를 구현하지 않는다
        private sealed class LegacyTarget : IDamageable
        {
            public int Total;
            public void TakeDamage(int amount) => Total += amount;
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

        // ───────── DamageAttribution이 속성과 시전 형태를 싣는다 ─────────

        [Test]
        public void Scope_CarriesSkillElementAndCastType()
        {
            using (DamageAttribution.Begin(new DamageSource(5, Element.Earth, CastType.Area)))
            {
                var info = DamageInfo.FromCurrent(10, isImpact: true);

                Assert.AreEqual(5, DamageAttribution.Current);
                Assert.AreEqual(10, info.Amount);
                Assert.AreEqual(5, info.SkillId);
                Assert.AreEqual(Element.Earth, info.Element);
                Assert.AreEqual(CastType.Area, info.CastType);
                Assert.IsTrue(info.IsImpact);
            }
        }

        [Test(Description = "스킬 밖의 피해는 스킬 정보가 없다")]
        public void OutsideScope_HasNoSkill()
        {
            var info = DamageInfo.FromCurrent(3);

            Assert.AreEqual(0, info.SkillId);
            Assert.IsFalse(info.IsImpact);
            Assert.AreEqual(0, DamageAttribution.Current);
        }

        [Test(Description = "자식 스킬의 피해는 가장 바깥 스킬(부모)의 속성과 시전 형태를 이어받는다")]
        public void Outermost_WinsForElementAndCastType()
        {
            using (DamageAttribution.BeginOutermost(new DamageSource(4, Element.Ice, CastType.Projectile)))
            using (DamageAttribution.BeginOutermost(new DamageSource(8, Element.Fire, CastType.Chain)))
            {
                var info = DamageInfo.FromCurrent(1);

                Assert.AreEqual(4, info.SkillId);
                Assert.AreEqual(Element.Ice, info.Element);
                Assert.AreEqual(CastType.Projectile, info.CastType);
            }
        }

        [Test(Description = "범위가 끝나면 이전 출처로 돌아간다")]
        public void Scope_RestoresPreviousSource()
        {
            using (DamageAttribution.Begin(new DamageSource(1, Element.Fire)))
            {
                using (DamageAttribution.Begin(new DamageSource(2, Element.Lightning)))
                {
                    Assert.AreEqual(Element.Lightning, DamageAttribution.CurrentSource.Element);
                }

                Assert.AreEqual(Element.Fire, DamageAttribution.CurrentSource.Element);
            }

            Assert.IsFalse(DamageAttribution.CurrentSource.IsSkill);
        }

        [Test(Description = "ID만 넘기는 기존 방식도 그대로 동작한다")]
        public void IdOnlyOverloads_StillWork()
        {
            using (DamageAttribution.Begin(7))
            {
                Assert.AreEqual(7, DamageAttribution.Current);
                Assert.AreEqual(Element.Neutral, DamageAttribution.CurrentSource.Element);
            }
        }

        // ───────── DamageInfo 오버로드 ─────────

        [Test(Description = "DamageInfo를 모르는 대상은 피해량만 받는다")]
        public void LegacyTarget_ReceivesAmountOnly()
        {
            var target = new LegacyTarget();

            ((IDamageable)target).TakeDamage(new DamageInfo(7, 3, Element.Fire, CastType.Projectile, true));

            Assert.AreEqual(7, target.Total);
        }

        [Test(Description = "실제 Enemy도 DamageInfo로 피해를 받는다 (아직 속성은 계산에 쓰지 않는다)")]
        public void Enemy_TakesDamageInfoAsAmount()
        {
            var model = new EnemyModel(1, 0f, EnemyType.Normal, 10, new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f),
                new NullPublisher<Game.Core.Messages.EnemyHpChanged>(), new NullPublisher<Game.Core.Messages.EnemyDied>());
            var enemy = TestEnemy.Create(model);

            ((IDamageable)enemy).TakeDamage(new DamageInfo(4, 1, Element.Ice, CastType.Projectile, true));

            Assert.AreEqual(6, model.Hp);
        }

        private sealed class NullPublisher<T> : MessagePipe.IPublisher<T>
        {
            public void Publish(T message) { }
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
            var source = new DamageSource(3, Element.Lightning, CastType.Hitscan);

            var impact = RaiseReaction(new DamageReaction(5, isImpact: true), new FakeTarget(), source);
            var extra = RaiseReaction(new DamageReaction(5), new FakeTarget(), source);

            Assert.IsTrue(impact.IsImpact);
            Assert.IsFalse(extra.IsImpact);
            Assert.AreEqual(Element.Lightning, impact.Element);
            Assert.AreEqual(CastType.Hitscan, impact.CastType);
        }

        [Test(Description = "빌더의 Damage는 직접 적중 피해(충격)이고 추가 번개 같은 부가 피해는 아니다")]
        public void Builder_DamageIsImpact()
        {
            var target = new FakeTarget();
            var reactions = new HitReactionBuilder().Damage(10).Build();

            reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up, target));

            Assert.IsTrue(target.Infos.Single().IsImpact);
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

        [Test(Description = "관통을 막지 않으면 기존처럼 관통한다")]
        public void NormalTarget_StillPierces()
        {
            var go = new GameObject("PierceNormalTest");
            try
            {
                var first = new FakeTarget { Position = Vector2.right * 0.5f };
                var behind = new FakeTarget { Position = Vector2.right * 0.7f };
                var provider = new Provider();
                provider.Targets.Add(first);
                provider.Targets.Add(behind);
                var projectile = CreateProjectile(go, provider, 2, default);

                Tick(projectile, 0.1f);

                Assert.AreEqual(1, first.Infos.Count);
                Assert.AreEqual(1, behind.Infos.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Ledger_ExhaustEndsRemainingHits()
        {
            var ledger = new ProjectileHitLedger();
            ledger.Reset(3);

            ledger.Exhaust();

            Assert.IsTrue(ledger.Exhausted);
            Assert.IsFalse(ledger.TryHit(new object()));
        }

        // ───────── 스킬 데이터 ─────────

        private static List<SkillData> LoadSkills() => new DefaultSkillDataProvider(new GameDataStore()).LoadAll();

        [Test]
        public void SkillData_BuildsDamageSource()
        {
            var data = new SkillData { id = 9, element = Element.Wind, castType = CastType.Beam };

            var source = data.ToDamageSource();

            Assert.AreEqual(9, source.SkillId);
            Assert.AreEqual(Element.Wind, source.Element);
            Assert.AreEqual(CastType.Beam, source.CastType);
        }

        // 스킬 ID → 속성 (docs/ninjutsu/README.md의 용어 대응과 원문 분류)
        private static readonly Dictionary<int, Element> ExpectedElements = new()
        {
            { 1, Element.Neutral }, { 2, Element.Fire }, { 3, Element.Lightning }, { 4, Element.Ice }, { 5, Element.Earth },
            { 6, Element.Ice }, { 7, Element.Lightning }, { 8, Element.Ice }, { 12, Element.Ice }, { 13, Element.Neutral },
            { 14, Element.Fire }, { 15, Element.Lightning }, { 16, Element.Neutral }, { 17, Element.Neutral }, { 18, Element.Lightning },
        };

        [Test]
        public void SkillsJson_EverySkillHasItsElement()
        {
            var skills = LoadSkills();

            Assert.AreEqual(ExpectedElements.Count, skills.Count);
            foreach (var skill in skills)
            {
                Assert.IsTrue(ExpectedElements.TryGetValue(skill.id, out var expected), $"스킬 {skill.id}의 기대 속성이 없습니다");
                Assert.AreEqual(expected, skill.element, $"스킬 {skill.id}({skill.name})");
            }
        }

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
