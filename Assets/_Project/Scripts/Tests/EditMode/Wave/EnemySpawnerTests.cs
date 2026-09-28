using NUnit.Framework;
using Game.Core.Wave;

namespace Game.Tests.Wave
{
    public class EnemySpawnerTests
    {
        private class FakeEnemyViewFactory : IEnemyViewFactory
        {
            public int CreateCallCount;
            public Enemy LastCreatedEnemy;

            public void Create(Enemy enemy)
            {
                CreateCallCount++;
                LastCreatedEnemy = enemy;
            }
        }

        private class FakeRandomProvider : IRandomProvider
        {
            private readonly float _fixedValue;
            public FakeRandomProvider(float fixedValue) => _fixedValue = fixedValue;
            public float Range(float min, float max) => _fixedValue; // 항상 이 값만 반환
        }

        private class FakeEnemySpeedProvider : IEnemySpeedProvider
        {
            private readonly float _speed;
            public FakeEnemySpeedProvider(float speed) => _speed = speed;
            public float GetSpeed(EnemyType type) => _speed;
        }

        private static WaveData CreateTestWaveData()
        {
            return new WaveData(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCount: 5,
                spawnCooldown: 3f,
                enemyType: EnemyType.Normal,
                spawnPositionXMin: -4f,
                spawnPositionXMax: 4f,
                spawnPositionY: 5f);
        }

        [Test]
        public void Advance_SpawnsExactlyOncePerFixedInterval()
        {
            var waveData = CreateTestWaveData();
            var factory = new FakeEnemyViewFactory();
            var random = new FakeRandomProvider(1f); // 랜덤이지만 항상 1초로 고정
            var speedProvider = new FakeEnemySpeedProvider(2f);

            var spawner = new EnemySpawner(factory, waveData, random, speedProvider);

            spawner.Advance(0.9f); // 아직 1초 안 지남
            Assert.AreEqual(0, factory.CreateCallCount);

            spawner.Advance(0.2f); // 누적 1.1초 -> 스폰돼야 함
            Assert.AreEqual(1, factory.CreateCallCount);
        }

        [Test]
        public void Advance_MovesSpawnedEnemyUsingSpeedFromProvider()
        {
            var waveData = CreateTestWaveData();
            var factory = new FakeEnemyViewFactory();
            var random = new FakeRandomProvider(1f); // 스폰 간격도, 스폰 X 위치도 항상 1로 고정
            var speedProvider = new FakeEnemySpeedProvider(3f); // 이 타입은 초당 3만큼 이동

            var spawner = new EnemySpawner(factory, waveData, random, speedProvider);

            spawner.Advance(1f); // 스폰 발생
            var spawnedY = factory.LastCreatedEnemy.Position.y;

            spawner.Advance(0.5f); // 다음 스폰 간격(1초) 안 지남 -> 이동만 발생
            var movedY = factory.LastCreatedEnemy.Position.y;

            // 아래쪽으로 speed(3) * deltaTime(0.5) 만큼 이동했는지 검증
            Assert.AreEqual(spawnedY - 3f * 0.5f, movedY, 0.0001f);
        }
    }
}
