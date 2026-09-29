using NUnit.Framework;
using Game.Core;
using UnityEngine;

namespace Game.Tests
{
    public class EnemySpawnerTests
    {
        private class FakeEnemyViewFactory : IEnemyViewFactory
        {
            private readonly float _speed;

            public int CreateCallCount;
            public Enemy LastCreatedEnemy;

            public FakeEnemyViewFactory(float speed) => _speed = speed;

            public Enemy Create(Vector2 spawnPosition, EnemyType type)
            {
                CreateCallCount++;
                LastCreatedEnemy = new Enemy(spawnPosition, _speed, type);

                return LastCreatedEnemy;
            }
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
                enemyType: EnemyType.Normal);
        }

        private static SpawnArea CreateTestSpawnArea()
        {
            var go = new GameObject("SpawnArea");
            return go.AddComponent<SpawnArea>();
        }

        [Test]
        public void Advance_SpawnsExactlyOncePerFixedInterval()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f); // 랜덤이지만 항상 1초로 고정

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random);

            spawner.Advance(0.9f); // 아직 1초 안 지남
            Assert.AreEqual(0, factory.CreateCallCount);

            spawner.Advance(0.2f); // 누적 1.1초 -> 스폰돼야 함
            Assert.AreEqual(1, factory.CreateCallCount);
        }

        [Test]
        public void Advance_MovesSpawnedEnemyUsingSpeedFromProvider()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random);

            spawner.Advance(1f); // 스폰 발생
            var spawnedY = factory.LastCreatedEnemy.Position.y;

            spawner.Advance(0.5f); // 다음 스폰 간격(1초) 안 지남 -> 이동만 발생
            var movedY = factory.LastCreatedEnemy.Position.y;

            Assert.AreEqual(spawnedY - 3f * 0.5f, movedY, 0.0001f);
        }
    }
}