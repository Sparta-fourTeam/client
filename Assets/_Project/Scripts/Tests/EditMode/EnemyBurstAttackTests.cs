using Game.Core;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    // 연속 공격: 공격 한 번에 BurstInterval 간격으로 BurstCount번 타격한다
    public sealed class EnemyBurstAttackTests
    {
        private sealed class FakePublisher<T> : IPublisher<T>, IBufferedPublisher<T>
        {
            public void Publish(T message) { }
        }

        private GameObject _wallGo;
        private Wall _wall;
        private EnemyProjectileSystem _projectiles;

        [SetUp]
        public void SetUp()
        {
            _wallGo = new GameObject("Wall");
            _wallGo.transform.position = Vector3.zero;
            _wall = _wallGo.AddComponent<Wall>();
            _wall.Construct(new FakePublisher<WallHpChanged>(), new FakePublisher<WallDestroyed>(), null);
            _wall.Initialize(1000);
            _projectiles = new EnemyProjectileSystem();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_wallGo);
        }

        private static EnemyModel Model(EnemyAttackStats attack) =>
            new EnemyModel(1, 0f, EnemyType.Normal, 100, attack, new FakePublisher<EnemyHpChanged>(), new FakePublisher<EnemyDied>());

        private static EnemyAttackStats Burst(int count, float burstInterval, AttackType type = AttackType.Melee, float interval = 1f) =>
            new EnemyAttackStats(type, 1, interval, 0f, type == AttackType.Ranged ? 8f : 0f, count, burstInterval);

        private void Tick(EnemyModel model, float deltaTime) => model.Attack(deltaTime, Vector2.zero, _wall, _projectiles);

        // ───────── 타격 시점 ─────────

        [Test(Description = "공격 간격이 되면 첫 타격이 나가고, 이후 BurstInterval마다 남은 타격이 나간다")]
        public void Burst_StrikesAtBurstInterval()
        {
            var model = Model(Burst(3, 0.2f));
            int strikes = 0;
            model.Attacked += () => strikes++;

            Tick(model, 0.9f);
            Assert.AreEqual(0, strikes, "공격 간격 전");
            Tick(model, 0.1f);
            Assert.AreEqual(1, strikes, "첫 타격");
            Tick(model, 0.1f);
            Assert.AreEqual(1, strikes, "연속 간격 전");
            Tick(model, 0.1f);
            Assert.AreEqual(2, strikes, "둘째 타격(0.2초 뒤)");
            Tick(model, 0.2f);
            Assert.AreEqual(3, strikes, "셋째 타격(0.4초 뒤)");
            Tick(model, 0.4f);
            Assert.AreEqual(3, strikes, "연속 공격이 끝나면 다음 공격 간격까지 더 없다");
        }

        [Test(Description = "한 번에 큰 시간이 흘러도 남은 타격을 모두 낸다")]
        public void Burst_LargeStepStrikesAllRemaining()
        {
            var model = Model(Burst(3, 0.2f));
            int strikes = 0;
            model.Attacked += () => strikes++;

            Tick(model, 1f); // 첫 타격
            Tick(model, 0.5f); // 남은 2번 모두

            Assert.AreEqual(3, strikes);
        }

        [Test(Description = "다음 공격 간격에는 다시 연속 공격을 한다")]
        public void Burst_RepeatsEachInterval()
        {
            var model = Model(Burst(2, 0.3f));
            int strikes = 0;
            model.Attacked += () => strikes++;

            Tick(model, 1f); // 1
            Tick(model, 0.3f); // 2
            Tick(model, 0.7f); // 다음 간격 첫 타격: 3
            Tick(model, 0.3f); // 4

            Assert.AreEqual(4, strikes);
        }

        [Test]
        public void NoBurst_IsASingleStrike()
        {
            var model = Model(new EnemyAttackStats(AttackType.Melee, 1, 1f, 0f));
            int strikes = 0;
            model.Attacked += () => strikes++;

            Tick(model, 1f);
            Tick(model, 0.5f);

            Assert.AreEqual(1, strikes);
        }

        [Test(Description = "원거리는 타격마다 투사체를 쏜다")]
        public void Ranged_FiresProjectilePerStrike()
        {
            var model = Model(Burst(3, 0.2f, AttackType.Ranged));
            int fired = 0;
            model.ProjectileFired += _ => fired++;

            Tick(model, 1f);
            Tick(model, 0.4f);

            Assert.AreEqual(3, fired);
        }

        [Test(Description = "기절하면 남은 타격이 멈추고, 풀리면 이어서 나간다")]
        public void Burst_PausesWhileStunned()
        {
            var model = Model(Burst(3, 0.2f));
            int strikes = 0;
            model.Attacked += () => strikes++;

            Tick(model, 1f); // 첫 타격
            model.ApplyStun(5f);
            Tick(model, 1f);
            Assert.AreEqual(1, strikes, "기절 중에는 안 나간다");

            model.TickStatus(5f);
            Tick(model, 0.4f);
            Assert.AreEqual(3, strikes);
        }

        [Test]
        public void MeleeBurst_DamagesWallPerStrike()
        {
            var model = Model(new EnemyAttackStats(AttackType.Melee, 5, 1f, 0f, 0f, 3, 0.2f));
            int before = _wall.CurrentHp;

            Tick(model, 1f);
            Tick(model, 0.4f);

            Assert.AreEqual(before - 15, _wall.CurrentHp);
        }

    }
}
