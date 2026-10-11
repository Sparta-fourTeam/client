using System.Collections.Generic;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Tests
{
    public sealed class SkillTargetSelectorTests
    {
        private sealed class Target : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public void TakeDamage(int damage) { }
        }

        private sealed class Mortal : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public int CurrentHp { get; set; }
            public bool IsDead => CurrentHp <= 0;
            public bool BlocksPierce { get; set; }
            public void TakeDamage(int damage) => CurrentHp -= damage;
        }

        private sealed class Dead : IEnemyTarget
        {
            public Vector2 Position { get; set; }
            public bool IsDead => true;
            public void TakeDamage(int damage) { }
        }

        private sealed class Targets : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Values = new();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            {
                results.Clear();
                results.AddRange(Values);
                results.Sort((a, b) => (a.Position - from).sqrMagnitude.CompareTo((b.Position - from).sqrMagnitude));
                if (results.Count > count) { results.RemoveRange(count, results.Count - count); }
                return results.Count;
            }
        }

        [Test]
        public void TargetSelection_FiltersRangeAndKeepsWallPriority()
        {
            var provider = new Targets();
            var nearWall = new Target { Position = new Vector2(2, 1) };
            var nearCaster = new Target { Position = new Vector2(0, 2) };
            provider.Values.AddRange(new IEnemyTarget[] { nearCaster, new Target { Position = new Vector2(100, 0) }, nearWall });
            var selector = new SkillTargetSelector(provider);
            var result = selector.Select(Vector2.zero, 3);
            CollectionAssert.AreEqual(new IEnemyTarget[] { nearWall, nearCaster }, result);
            provider.Values.Clear();
            Assert.IsEmpty(selector.Select(Vector2.zero, 3));
        }

        [Test(Description = "굴러가는 공격은 벽 앞선에서 위로 높이(y)만 재고 가로 위치(x)는 보지 않는다. 벽에 가까운 순이다")]
        public void SelectLane_UsesOnlyTheHeightAboveTheWallLineAndSortsByProximity()
        {
            var provider = new Targets();
            var near = new Target { Position = new Vector2(-40, 1) };         // 가로로 아주 멀어도 높이가 닿으면 고른다
            var far = new Target { Position = new Vector2(30, 5.5f) };
            var tooHigh = new Target { Position = new Vector2(0, 6.5f) };      // 벽 앞선(0)에서 6 이내가 아니다
            provider.Values.AddRange(new IEnemyTarget[] { far, tooHigh, near });
            var selector = new SkillTargetSelector(provider);

            var result = selector.SelectLane(0, 6);

            CollectionAssert.AreEqual(new IEnemyTarget[] { near, far }, result);
        }

        [Test(Description = "굴러가는 공격의 후보는 죽은 적을 빼고 벽에 가까운 8마리까지다")]
        public void SelectLane_SkipsDeadAndKeepsTheEightClosestToTheWall()
        {
            var provider = new Targets();
            var dead = new Dead { Position = new Vector2(0, .1f) };
            provider.Values.Add(dead);
            for (int i = 0; i < 10; i++) { provider.Values.Add(new Target { Position = new Vector2(i, 1 + i * .1f) }); }
            var selector = new SkillTargetSelector(provider);

            var result = selector.SelectLane(0, 6);

            Assert.AreEqual(8, result.Count);
            CollectionAssert.DoesNotContain(result, dead);
            Assert.AreEqual(1f, result[0].Position.y, .0001f);
        }

        [TestCase(4, Description = "예약 피해가 남은 체력과 정확히 같아도 뒤로 밀리고, 만료되면 벽에 가까운 순으로 돌아온다")]
        [TestCase(6, Description = "예약 피해가 남은 체력을 넘어도 뒤로 밀리고, 만료되면 벽에 가까운 순으로 돌아온다")]
        public void Select_SkipsEnemiesAlreadyReservedToDieUntilTheReservationExpires(int additionalDamage)
        {
            var provider = new Targets();
            var front = new Mortal { Position = new Vector2(0, 1), CurrentHp = 10 };
            var back = new Mortal { Position = new Vector2(0, 2), CurrentHp = 10 };
            provider.Values.AddRange(new IEnemyTarget[] { back, front });
            var reservations = new TargetReservations();
            var selector = new SkillTargetSelector(provider, reservations);

            reservations.Reserve(front, 6, 1f);
            Assert.AreSame(front, selector.Select(Vector2.zero, 5)[0], "피해가 남은 체력에 못 미치면 그대로 앞 적을 노린다");

            reservations.Reserve(front, additionalDamage, 1f);
            Assert.AreSame(back, selector.Select(Vector2.zero, 5)[0], "예약 피해가 남은 체력 이상이면 다른 적을 노린다");

            reservations.Advance(1.1f);
            Assert.AreSame(front, selector.Select(Vector2.zero, 5)[0], "빗나가 만료된 예약은 사라진다");
        }

        [TestCase(10, Description = "모든 후보가 죽을 예정이면 정확히 죽는 적을 넘치는 피해가 있는 적보다 먼저 고른다")]
        [TestCase(12, Description = "모든 후보가 죽을 예정이어도 대상이 없어지지 않고, 넘치는 양이 가장 적은 적부터 고른다")]
        public void Select_WhenEveryoneIsReserved_PrefersTheLeastOverkilled(int backDamage)
        {
            var provider = new Targets();
            var front = new Mortal { Position = new Vector2(0, 1), CurrentHp = 10 };
            var back = new Mortal { Position = new Vector2(0, 2), CurrentHp = 10 };
            provider.Values.AddRange(new IEnemyTarget[] { front, back });
            var reservations = new TargetReservations();
            reservations.Reserve(front, 30, 1f);
            reservations.Reserve(back, backDamage, 1f);

            var result = new SkillTargetSelector(provider, reservations).Select(Vector2.zero, 5);

            Assert.AreEqual(2, result.Count);
            Assert.AreSame(back, result[0]);
        }

        [TestCase(11, false, Description = "5.9 피해 두 발은 각각 버림한 5 + 5이므로 체력 11인 적은 살아남는다")]
        [TestCase(10, true, Description = "각각 버림한 5 + 5로 체력 10인 적은 정확히 죽는다")]
        public void Select_UsesIntegerDamagePerReservedHit(int hp, bool lethal)
        {
            var provider = new Targets();
            var front = new Mortal { Position = new Vector2(0, 1), CurrentHp = hp };
            var back = new Mortal { Position = new Vector2(0, 2), CurrentHp = 10 };
            provider.Values.AddRange(new IEnemyTarget[] { front, back });
            var reservations = new TargetReservations();
            reservations.Reserve(front, 5.9f, 1f);
            reservations.Reserve(front, 5.9f, 1f);

            var result = new SkillTargetSelector(provider, reservations).Select(Vector2.zero, 5);

            Assert.AreSame(lethal ? back : front, result[0]);
        }

        [Test(Description = "가까운 여덟 적이 죽을 예정이어도 사거리 안의 아홉 번째 생존 후보를 먼저 고르고 결과는 여덟으로 제한한다")]
        public void Select_RanksReservationsBeforeLimitingToEightTargets()
        {
            var provider = new Targets();
            var reservations = new TargetReservations();
            for (int i = 1; i <= 8; i++)
            {
                var target = new Mortal { Position = new Vector2(0, i), CurrentHp = 10 };
                provider.Values.Add(target);
                reservations.Reserve(target, 10, 1f);
            }
            var survivor = new Mortal { Position = new Vector2(0, 9), CurrentHp = 10 };
            var outside = new Mortal { Position = new Vector2(11, 0), CurrentHp = 10 };
            provider.Values.AddRange(new IEnemyTarget[] { survivor, outside });

            var result = new SkillTargetSelector(provider, reservations).Select(Vector2.zero, 10);

            Assert.AreEqual(8, result.Count);
            Assert.AreSame(survivor, result[0]);
            CollectionAssert.DoesNotContain(result, outside);
        }

        private static void TickProjectile(Projectile projectile, float deltaTime) =>
            typeof(Projectile).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(projectile, new object[] { deltaTime });

        [TestCase(10)]
        [TestCase(15)]
        public void Projectile_RegistersBeforeStartAndClearsItsHitBeforeChildSelection(int hp)
        {
            var prefab = new GameObject("ReservedHitTest");
            prefab.SetActive(false);
            prefab.AddComponent<Projectile>();
            try
            {
                var targets = new Targets();
                var front = new Mortal { Position = Vector2.up, CurrentHp = hp };
                var back = new Mortal { Position = Vector2.up * 2, CurrentHp = 20 };
                targets.Values.AddRange(new IEnemyTarget[] { front, back });
                var reservations = new TargetReservations();
                var selector = new SkillTargetSelector(targets, reservations);
                var data = new SkillData { baseStats = new SkillBaseStats
                { cast = { baseDamage = 10, range = 5, projectileCount = 1 }, projectile = { speed = 10, pierceCount = 1 } } };
                var builder = new SkillConfigBuilder(SkillConfig.FromDefinition(data));
                bool started = false, hit = false;
                builder.AddReaction(AttackEvent.Start, new CastSkillReaction(_ =>
                {
                    started = true;
                    Assert.AreEqual(hp == 10, reservations.IsLethal(front), "Start 자식 시전보다 먼저 예약해야 한다");
                }));
                builder.AddReaction(AttackEvent.Hit, new CastSkillReaction(context =>
                {
                    hit = true;
                    Assert.AreSame(front, context.Target);
                    Assert.AreEqual(hp - 10, front.CurrentHp);
                    Assert.IsFalse(reservations.IsLethal(front), "적중 피해를 줄어든 HP에 다시 세면 안 된다");
                    if (hp > 10) { Assert.AreSame(front, selector.Select(Vector2.zero, 5)[0]); }
                }));
                using var strategy = new ProjectileStrategy(prefab);
                Assert.IsTrue(strategy.Fire(builder.Build(), new AttackEnvironment(prefab.transform, targets, selector.Select, reservations: reservations)));
                var projectile = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
                var launched = System.Array.Find(projectile, p => p.name == "ReservedHitTest(Clone)");
                Assert.IsNotNull(launched);
                TickProjectile(launched, .1f);
                Assert.IsTrue(started);
                Assert.IsTrue(hit);
                Assert.IsTrue(launched.gameObject.activeSelf, "관통 중에도 적중한 예약은 즉시 없어져야 한다");
            }
            finally { Object.DestroyImmediate(prefab); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Projectile_InterceptionKeepsItsAimReservationUnlessItCannotContinue(bool blocksPierce)
        {
            var go = new GameObject("ReservationInterceptionTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var targets = new Targets();
                var aim = new Mortal { Position = Vector2.up * 2, CurrentHp = 15 };
                var interceptor = new Mortal { Position = Vector2.up, CurrentHp = 20, BlocksPierce = blocksPierce };
                targets.Values.AddRange(new IEnemyTarget[] { interceptor, aim });
                var reservations = new TargetReservations();
                var own = reservations.Reserve(aim, 10, 1);
                var sibling = reservations.Reserve(aim, 6, 1);
                var pool = new ObjectPool<Projectile>(() => projectile, actionOnRelease: p => p.gameObject.SetActive(false));
                pool.Get().Init(pool, new ProjectileSpawnSettings
                {
                    StartPos = Vector3.zero, Direction = Vector3.up, Speed = 10, Lifetime = 1,
                    TargetProvider = targets, PierceCount = 1, Reservation = own,
                    HitReactions = new HitReactionBuilder().Damage(10).Build()
                });
                TickProjectile(projectile, .1f);
                Assert.AreEqual(10, interceptor.CurrentHp);
                Assert.AreEqual(!blocksPierce, reservations.IsLethal(aim));
                if (!blocksPierce)
                {
                    TickProjectile(projectile, .1f);
                    Assert.AreEqual(5, aim.CurrentHp);
                    Assert.AreEqual(1, reservations.Overkill(aim), "별도 투사체의 6 피해 예약은 보존해야 한다");
                }
                own.Dispose();
                sibling.Dispose();
                Assert.IsFalse(reservations.IsLethal(aim));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase("expire")]
        [TestCase("disable")]
        [TestCase("destroy")]
        [TestCase("reuse")]
        public void Projectile_CleanupReleasesOnlyItsOwnReservationAndIsRepeatable(string cleanup)
        {
            var go = new GameObject("ReservationCleanupTest");
            try
            {
                var projectile = go.AddComponent<Projectile>();
                var target = new Mortal { Position = Vector2.up, CurrentHp = 10 };
                var reservations = new TargetReservations();
                var own = reservations.Reserve(target, 6, 1);
                var sibling = reservations.Reserve(target, 4, 1);
                var pool = new ObjectPool<Projectile>(() => projectile, actionOnRelease: p => p.gameObject.SetActive(false));
                var settings = new ProjectileSpawnSettings
                { TargetProvider = new Targets(), Lifetime = .1f, Reservation = own };
                bool expired = false;
                settings.Reactions = new AttackReactions(new[] { new ReactionBinding(AttackEvent.Expired,
                    new CastSkillReaction(_ => { expired = true; Assert.IsFalse(reservations.IsLethal(target)); })) });
                pool.Get().Init(pool, settings);
                Assert.IsTrue(reservations.IsLethal(target));
                TargetReservations.Reservation reused = null;
                switch (cleanup)
                {
                    case "expire": TickProjectile(projectile, .2f); break;
                    case "disable": go.SetActive(false); break;
                    case "destroy": Object.DestroyImmediate(go); break;
                    case "reuse":
                        reused = reservations.Reserve(target, 6, 1);
                        projectile.Init(pool, new ProjectileSpawnSettings { TargetProvider = new Targets(), Lifetime = 1, Reservation = reused });
                        break;
                }
                own.Dispose();
                own.Dispose();
                Assert.AreEqual(cleanup == "expire", expired);
                Assert.AreEqual(cleanup == "reuse", reservations.IsLethal(target));
                Assert.AreEqual(0, reservations.Overkill(target));
                reused?.Dispose();
                Assert.IsFalse(reservations.IsLethal(target));
                using var next = reservations.Reserve(target, 6, 1);
                own.Dispose();
                Assert.IsTrue(reservations.IsLethal(target), "만료/해제된 토큰이 재사용된 공격이나 다른 예약을 지우면 안 된다");
                sibling.Dispose();
                Assert.IsFalse(reservations.IsLethal(target));
            }
            finally { if (go != null) { Object.DestroyImmediate(go); } }
        }
    }
}
