using NUnit.Framework;
using Game.Core.Wave;

namespace Game.Tests.Wave
{
    public class EnemySpawnerTests
    {
        private class FakeEnemyViewFactory : IEnemyViewFactory
        {
            public int CreateCallCount;
            public void Create(Enemy enemy) => CreateCallCount++;
        }

        private class FakeRandomProvider : IRandomProvider
        {
            private readonly float _fixedValue;
            public FakeRandomProvider(float fixedValue) => _fixedValue = fixedValue;
            public float Range(float min, float max) => _fixedValue; // 항상 이 값만 반환
        }

        private static WaveData CreateTestWaveData()
        {
            return new WaveData(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCount: 5,
                spawnCooldown: 3f,
                enemyType: EnemyType.Normal,
                enemySpeed: 2f,
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

            var spawner = new EnemySpawner(factory, waveData, random);

            spawner.Advance(0.9f); // 아직 1초 안 지남
            Assert.AreEqual(0, factory.CreateCallCount);

            spawner.Advance(0.2f); // 누적 1.1초 -> 스폰돼야 함
            Assert.AreEqual(1, factory.CreateCallCount);
        }
    }
}
