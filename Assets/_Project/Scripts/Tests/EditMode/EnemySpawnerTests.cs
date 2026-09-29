using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
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

            // Range가 어떤 min/max로 호출됐는지 순서대로 기록
            public readonly List<(float Min, float Max)> Calls = new();

            public FakeRandomProvider(float fixedValue) => _fixedValue = fixedValue;

            public float Range(float min, float max)
            {
                Calls.Add((min, max));
                return _fixedValue; // 항상 이 값만 반환
            }
        }

        // 테스트에서 WaveStarted / WaveGaugeFilled를 직접 발행할 수 있게 해주는 가짜 구독자
        private class FakeSubscriber<T> : ISubscriber<T>
        {
            private readonly List<IMessageHandler<T>> _handlers = new();

            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                _handlers.Add(handler);
                return new Subscription(_handlers, handler);
            }

            // 실제로 메시지가 발행된 것처럼 등록된 핸들러들을 그대로 호출
            public void Publish(T message)
            {
                foreach (var handler in _handlers.ToArray())
                {
                    handler.Handle(message);
                }
            }

            private class Subscription : IDisposable
            {
                private readonly List<IMessageHandler<T>> _handlers;
                private readonly IMessageHandler<T> _handler;

                public Subscription(List<IMessageHandler<T>> handlers, IMessageHandler<T> handler)
                {
                    _handlers = handlers;
                    _handler = handler;
                }

                public void Dispose() => _handlers.Remove(_handler);
            }
        }

        private static EnemySpawnConfig CreateTestWaveData()
        {
            return new EnemySpawnConfig(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCooldown: 3f,
                enemyType: EnemyType.Normal);
        }

        private static SpawnArea CreateTestSpawnArea()
        {
            var go = new GameObject("SpawnArea");
            return go.AddComponent<SpawnArea>();
        }

        [Test]
        public void Advance_BeforeWaveStarted_DoesNotSpawn()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();

            spawner.Advance(5f); // WaveStarted가 안 왔으니 시간이 아무리 지나도 스폰 안 됨

            Assert.AreEqual(0, factory.CreateCallCount);
        }

        [Test]
        public void Advance_SpawnsExactlyOncePerFixedInterval()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f); // 랜덤이지만 항상 1초로 고정
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

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
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

            spawner.Advance(1f); // 스폰 발생
            var spawnedY = factory.LastCreatedEnemy.Position.y;

            spawner.Advance(0.5f); // 다음 스폰 간격(1초) 안 지남 -> 이동만 발생
            var movedY = factory.LastCreatedEnemy.Position.y;

            Assert.AreEqual(spawnedY - 3f * 0.5f, movedY, 0.0001f);
        }

        [Test]
        public void Advance_SpawnsUsingSpawnAreaXAndYBoundsSeparately()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            spawnArea.transform.position = new Vector3(2f, 5f, 0f); // X, Y를 다른 값으로 둬서 뒤바뀜을 잡을 수 있게 함

            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

            spawner.Advance(1f); // 스폰 발생

            var expectedXCall = (spawnArea.Min.x, spawnArea.Max.x);
            var expectedYCall = (spawnArea.Min.y, spawnArea.Max.y);

            CollectionAssert.Contains(random.Calls, expectedXCall);
            CollectionAssert.Contains(random.Calls, expectedYCall);
        }

        [Test]
        public void Advance_AfterWaveGaugeFilled_StopsSpawning()
        {
            var waveData = CreateTestWaveData();
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f);
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 5, isFinalWave: false));

            spawner.Advance(1f); // 스폰 1회 발생
            Assert.AreEqual(1, factory.CreateCallCount);

            waveGaugeFilled.Publish(new WaveGaugeFilled(isFinalWave: false)); // 웨이브 종료

            spawner.Advance(10f); // 다음 WaveStarted 전까지는 시간이 지나도 스폰 안 됨
            Assert.AreEqual(1, factory.CreateCallCount);
        }

        [Test]
        public void Advance_FillsBurst_WaitsCooldown_ThenSpawnsNextBurst()
        {
            var waveData = CreateTestWaveData(); // SpawnCooldown = 3f
            var spawnArea = CreateTestSpawnArea();
            var factory = new FakeEnemyViewFactory(3.0f);
            var random = new FakeRandomProvider(1f); // Range가 항상 1초/1유닛 고정
            var waveStarted = new FakeSubscriber<WaveStarted>();
            var waveGaugeFilled = new FakeSubscriber<WaveGaugeFilled>();

            var spawner = new EnemySpawner(factory, waveData, spawnArea, random, waveStarted, waveGaugeFilled);
            spawner.Initialize();
            waveStarted.Publish(new WaveStarted(waveIndex: 0, enemyCount: 2, isFinalWave: false)); // 이번 웨이브는 한 버스트에 2마리

            spawner.Advance(1f); // 버스트 1번째 스폰
            spawner.Advance(1f); // 버스트 2번째 스폰 -> 버스트 크기(2) 채움 -> 대기 상태 전환
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(2.9f); // 쿨다운(3초) 아직 안 지남 -> 계속 대기
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(0.2f); // 누적 3.1초 -> 쿨다운 끝, 대기 상태 해제(이 호출에서 바로 스폰되진 않음)
            Assert.AreEqual(2, factory.CreateCallCount);

            spawner.Advance(1f); // 다음 스폰 간격 지남 -> 다음 버스트의 1번째 스폰 -> 계속 반복돼야 함
            Assert.AreEqual(3, factory.CreateCallCount);
        }
    }
}
