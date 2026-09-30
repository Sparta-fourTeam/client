using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class EnemyAttackTests
    {
        // Wall.Construct에 넣을 기록용 Publisher (Buffered 겸용)
        private class FakePublisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public readonly List<T> Published = new();
            public void Publish(T message) => Published.Add(message);
        }

        private const int WallMaxHp = 100;
        private const float Speed = EnemyProjectileSystem.ProjectileSpeed;


        private GameObject _wallGo;
        private Wall _wall;
        private FakePublisher<WallDestroyed> _wallDestroyed;
        private EnemyProjectileSystem _projectiles;

        [SetUp]
        public void SetUp()
        {
            _wallGo = new GameObject("Wall");
            _wallGo.transform.position = Vector3.zero; // AttackLineY = 0 (_attackLineOffset 기본값 0)
            _wall = _wallGo.AddComponent<Wall>();
            _wallDestroyed = new FakePublisher<WallDestroyed>();
            _wall.Construct(new FakePublisher<WallHpChanged>(), _wallDestroyed);
            _wall.Initialize(WallMaxHp); // EditMode에서는 Start가 호출되지 않음

            _projectiles = new EnemyProjectileSystem();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wallGo);
        }

        private static EnemyModel CreateEnemy(
            float y, AttackType type = AttackType.Melee,
            int damage = 10, float interval = 1f, float range = 0f,
            float x = 0f, float speed = 5f, int maxHp = 10)
        {
            return new EnemyModel(1, new Vector2(x, y), speed, EnemyType.Normal, maxHp,
                new EnemyAttackStats(type, damage, interval, range),
                new FakePublisher<EnemyHpChanged>(), new FakePublisher<EnemyDied>());
        }

        // ───────── 공격 값 검증 ─────────

        [TestCase(0f)]
        [TestCase(-1f)]
        public void AttackStats_NonPositiveInterval_Throws(float interval)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyAttackStats(AttackType.Melee, 10, interval, 0f));
        }

        [Test]
        public void AttackStats_NegativeRangeOrDamage_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyAttackStats(AttackType.Ranged, 10, 1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyAttackStats(AttackType.Melee, -1, 1f, 0f));
        }

        // ───────── 사거리 판정 ─────────

        [Test]
        public void IsInAttackRange_OutsideRange_False()
        {
            var enemy = CreateEnemy(y: 5f, range: 3f); // 거리 5 > 3

            Assert.IsFalse(enemy.IsInAttackRange(_wall));
        }

        [Test]
        public void IsInAttackRange_DistanceEqualsRange_True()
        {
            var enemy = CreateEnemy(y: 3f, range: 3f); // 거리 3 == 3 (경계값)

            Assert.IsTrue(enemy.IsInAttackRange(_wall));
        }

        [Test]
        public void IsInAttackRange_MeleeAtWallLine_True()
        {
            var enemy = CreateEnemy(y: 0f, range: 0f);

            Assert.IsTrue(enemy.IsInAttackRange(_wall));
        }

        [Test]
        public void IsInAttackRange_UsesOnlyY()
        {
            var left = CreateEnemy(y: 3f, range: 3f, x: -10f);
            var right = CreateEnemy(y: 3f, range: 3f, x: 10f);

            Assert.IsTrue(left.IsInAttackRange(_wall));  // x가 달라도 y 거리만으로 판정
            Assert.IsTrue(right.IsInAttackRange(_wall));
        }

        [Test]
        public void IsInAttackRange_BecomesTrueAfterMoving()
        {
            var enemy = CreateEnemy(y: 5f, range: 0f, speed: 5f);
            Assert.IsFalse(enemy.IsInAttackRange(_wall));

            enemy.Move(1f); // 5 → 0

            Assert.IsTrue(enemy.IsInAttackRange(_wall));
        }

        // ───────── 근거리 공격 ─────────

        [Test]
        public void Melee_BeforeInterval_NoDamage()
        {
            var enemy = CreateEnemy(y: 0f, damage: 10, interval: 1f);

            enemy.Attack(0.9f, _wall, _projectiles);

            Assert.AreEqual(WallMaxHp, _wall.CurrentHp);
        }

        [Test]
        public void Melee_DamagesWallExactlyEveryInterval()
        {
            var enemy = CreateEnemy(y: 0f, damage: 10, interval: 1f);

            enemy.Attack(1f, _wall, _projectiles);
            enemy.Attack(1f, _wall, _projectiles);
            enemy.Attack(1f, _wall, _projectiles);

            Assert.AreEqual(WallMaxHp - 30, _wall.CurrentHp);
        }

        [Test]
        public void Melee_PartialDeltaTimes_Accumulate()
        {
            var enemy = CreateEnemy(y: 0f, damage: 10, interval: 1f);

            enemy.Attack(0.5f, _wall, _projectiles);
            Assert.AreEqual(WallMaxHp, _wall.CurrentHp);

            enemy.Attack(0.5f, _wall, _projectiles); // 누적 1초
            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp);
        }

        [Test]
        public void Melee_DoesNotFireProjectile()
        {
            var enemy = CreateEnemy(y: 0f);
            int fired = 0;
            enemy.ProjectileFired += _ => fired++;

            enemy.Attack(1f, _wall, _projectiles);

            Assert.AreEqual(0, fired);
            Assert.AreEqual(0, _projectiles.ActiveCount);
        }

        [Test]
        public void Melee_WhenDead_DoesNotAttack()
        {
            var enemy = CreateEnemy(y: 0f, maxHp: 10);
            enemy.TakeDamage(10);

            enemy.Attack(5f, _wall, _projectiles);

            Assert.AreEqual(WallMaxHp, _wall.CurrentHp);
        }

        [Test]
        public void Melee_WallDestroyed_NoMoreDamage_DestroyedPublishedOnce()
        {
            _wall.Initialize(15);
            var enemy = CreateEnemy(y: 0f, damage: 10, interval: 1f);

            enemy.Attack(1f, _wall, _projectiles); // 15 → 5
            enemy.Attack(1f, _wall, _projectiles); // 5 → 0, 파괴
            enemy.Attack(1f, _wall, _projectiles); // 파괴 후 무시

            Assert.AreEqual(0, _wall.CurrentHp);
            Assert.IsTrue(_wall.IsDestroyed);
            Assert.AreEqual(1, _wallDestroyed.Published.Count);
        }

        // ───────── 원거리 공격 ─────────

        [Test]
        public void Ranged_Attack_FiresProjectile_WithoutImmediateDamage()
        {
            var enemy = CreateEnemy(y: 4f, AttackType.Ranged, damage: 10, interval: 1f, range: 4f);
            EnemyProjectileModel fired = null;
            enemy.ProjectileFired += p => fired = p;

            enemy.Attack(1f, _wall, _projectiles);

            Assert.IsNotNull(fired);
            Assert.AreEqual(4f, fired.Position.y, 0.0001f); // 적 위치에서 발사
            Assert.AreEqual(1, _projectiles.ActiveCount);
            Assert.AreEqual(WallMaxHp, _wall.CurrentHp);     // 아직 안 맞음
        }

        [Test]
        public void Ranged_ProjectileReachesWall_DamagesOnce()
        {
            var enemy = CreateEnemy(y: 4f, AttackType.Ranged, damage: 10, interval: 1f, range: 4f);
            enemy.Attack(1f, _wall, _projectiles);

            _projectiles.Tick(4f / Speed, _wall); // 거리 4 ÷ 탄속 8 = 0.5초
            _projectiles.Tick(1f, _wall);         // 이미 사라진 탄

            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp);
            Assert.AreEqual(0, _projectiles.ActiveCount);
        }

        [Test]
        public void Ranged_EnemyDiesAfterFiring_InFlightProjectileStillHits()
        {
            var enemy = CreateEnemy(y: 4f, AttackType.Ranged, damage: 10, interval: 1f, range: 4f);
            enemy.Attack(1f, _wall, _projectiles); // 발사

            enemy.TakeDamage(999);                  // 발사한 적 사망
            enemy.Attack(5f, _wall, _projectiles);  // 새 발사 없음
            Assert.AreEqual(1, _projectiles.ActiveCount);

            _projectiles.Tick(1f, _wall);           // 이미 쏜 탄은 명중

            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp);
        }

        // ───────── 투사체 모델 ─────────

        [Test]
        public void Projectile_MovesDownWithoutDamage_BeforeWall()
        {
            var projectile = new EnemyProjectileModel(new Vector2(0f, 4f), Speed, 10);

            projectile.Tick(0.25f, _wall); // 4 → 2

            Assert.AreEqual(2f, projectile.Position.y, 0.0001f);
            Assert.IsFalse(projectile.IsDone);
            Assert.AreEqual(WallMaxHp, _wall.CurrentHp);
        }

        [Test]
        public void Projectile_LargeDeltaTime_OvershootsButHitsOnce()
        {
            var projectile = new EnemyProjectileModel(new Vector2(0f, 4f), Speed, 10);

            projectile.Tick(2f, _wall);  // 한 번에 벽을 지나침
            projectile.Tick(1f, _wall);

            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp);
        }

        [Test]
        public void Projectile_NonPositiveLifetime_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyProjectileModel(Vector2.zero, Speed, 10));
        }

        // ───────── 투사체 수명 (3초) ─────────

        [Test]
        public void Projectile_LifetimeExpires_WithoutHittingWall_NoDamage()
        {
            // 벽까지 100 → 탄속 8로 3초 동안 24만 이동해서 닿지 않음
            var projectile = new EnemyProjectileModel(new Vector2(0f, 100f), Speed, 10);

            projectile.Tick(2.9f, _wall);
            Assert.IsFalse(projectile.IsDone);

            projectile.Tick(0.2f, _wall); // 누적 3.1초

            Assert.IsTrue(projectile.IsDone);
            Assert.AreEqual(WallMaxHp, _wall.CurrentHp);
        }

        [Test]
        public void Projectile_HitsOnLastFrameOfLifetime_StillDamages()
        {
            // 거리 24 = 탄속 8 × 3초 → 수명이 끝나는 프레임에 딱 닿음
            var projectile = new EnemyProjectileModel(new Vector2(0f, 24f), Speed, 10);

            projectile.Tick(3f, _wall);

            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp);
        }

        [Test]
        public void ProjectileSystem_RemovesExpiredProjectile()
        {
            _projectiles.Fire(new Vector2(0f, 100f), 10); // 벽에 닿지 않는 거리

            _projectiles.Tick(1f, _wall);
            Assert.AreEqual(1, _projectiles.ActiveCount);

            _projectiles.Tick(4f, _wall); // 누적 4초

            Assert.AreEqual(0, _projectiles.ActiveCount);
            Assert.AreEqual(WallMaxHp, _wall.CurrentHp);
        }

        [Test]
        public void ProjectileSystem_MultipleProjectiles_SumDamage()
        {
            _projectiles.Fire(new Vector2(0f, 4f), 10);
            _projectiles.Fire(new Vector2(3f, 8f), 5);

            _projectiles.Tick(1f, _wall); // 둘 다 명중

            Assert.AreEqual(WallMaxHp - 15, _wall.CurrentHp);
            Assert.AreEqual(0, _projectiles.ActiveCount);
        }
    }
}