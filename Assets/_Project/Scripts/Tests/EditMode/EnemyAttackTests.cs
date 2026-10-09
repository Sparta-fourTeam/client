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
        private const float ProjectileSpeed = 8f;
        private const float Speed = ProjectileSpeed;

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
            _wall.Construct(new FakePublisher<WallHpChanged>(), _wallDestroyed, null);
            _wall.Initialize(WallMaxHp); // EditMode에서는 Start가 호출되지 않음

            _projectiles = new EnemyProjectileSystem();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wallGo);
        }

        private static Enemy CreateEnemy(
            float y, AttackType type = AttackType.Melee,
            int damage = 10, float interval = 1f, float range = 0f,
            float x = 0f, float speed = 5f, int maxHp = 10)
        {
            var model = new EnemyModel(1, speed, EnemyType.Normal, maxHp,
                new EnemyAttackStats(type, damage, interval, range, type == AttackType.Ranged ? ProjectileSpeed : 0f),
                new FakePublisher<EnemyHpChanged>(), new FakePublisher<EnemyDied>());
            return TestEnemy.Create(model, new Vector2(x, y));
        }

        // ───────── 근거리 공격 ─────────

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
            enemy.Model.ProjectileFired += p => fired = p;

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
        public void Ranged_FiresOncePerInterval()
        {
            var enemy = CreateEnemy(y: 4f, AttackType.Ranged, damage: 10, interval: 1f, range: 4f);
            int fired = 0;
            enemy.Model.ProjectileFired += _ => fired++;

            enemy.Attack(0.5f, _wall, _projectiles); // 간격 전
            enemy.Attack(0.5f, _wall, _projectiles); // 1발
            enemy.Attack(1f, _wall, _projectiles);   // 2발

            Assert.AreEqual(2, fired);
            Assert.AreEqual(2, _projectiles.ActiveCount);
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
        public void Projectile_ReachesAttackLineExactly_Hits()
        {
            // 거리 8 = 탄속 8 × 1초 → 공격선에 딱 닿음 (<= 경계값)
            var projectile = new EnemyProjectileModel(new Vector2(0f, 8f), Speed, 10);

            projectile.Tick(1f, _wall);

            Assert.IsTrue(projectile.IsDone);
            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp);
        }

        [Test]
        public void Projectile_LargeDeltaTime_OvershootsButHitsOnce()
        {
            var projectile = new EnemyProjectileModel(new Vector2(0f, 4f), Speed, 10);

            projectile.Tick(2f, _wall);  // 한 번에 벽을 지나침
            projectile.Tick(1f, _wall);  // 이미 끝난 투사체

            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp);
        }

        [Test]
        public void Projectile_UsesOnlyY_ForHit()
        {
            var projectile = new EnemyProjectileModel(new Vector2(50f, 4f), Speed, 10); // x가 멀어도

            projectile.Tick(1f, _wall);

            Assert.AreEqual(WallMaxHp - 10, _wall.CurrentHp); // y만으로 명중 판정
        }

        // ───────── 투사체 시스템 ─────────

        [Test]
        public void ProjectileSystem_MultipleProjectiles_SumDamage()
        {
            _projectiles.Fire(new Vector2(0f, 4f), 10, ProjectileSpeed);
            _projectiles.Fire(new Vector2(3f, 8f), 5, ProjectileSpeed);

            _projectiles.Tick(1f, _wall); // 둘 다 명중

            Assert.AreEqual(WallMaxHp - 15, _wall.CurrentHp);
            Assert.AreEqual(0, _projectiles.ActiveCount);
        }

        [Test]
        public void ProjectileSystem_WallDestroyed_ProjectileStillRemoved_NoDamage()
        {
            _wall.Initialize(10);
            _wall.TakeDamage(10); // 벽 파괴
            _projectiles.Fire(new Vector2(0f, 4f), 10, ProjectileSpeed);

            _projectiles.Tick(1f, _wall);

            Assert.AreEqual(0, _wall.CurrentHp);
            Assert.AreEqual(0, _projectiles.ActiveCount); // 파괴된 벽에 닿아도 정리됨
        }
    }
}
