using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Messages;
using Game.Core.Stage;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class WaveManagerTests
    {
        private IServiceProvider _provider;
        private IPublisher<WaveCompleted> _waveCompleted;
        private List<WaveStarted> _started;
        private IDisposable _capture;
        private WaveManager _manager;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<WaveStarted>();
            builder.AddMessageBroker<WaveCompleted>();
            _provider = builder.BuildServiceProvider();

            _waveCompleted = _provider.GetRequiredService<IPublisher<WaveCompleted>>();
            _started = new List<WaveStarted>();
            _capture = _provider.GetRequiredService<ISubscriber<WaveStarted>>().Subscribe(_started.Add);

            var stage = new StageDefinition
            {
                Waves = new List<WaveDefinition>
                {
                    Wave((1, 5, EnemyType.Normal)),
                    Wave((1, 6, EnemyType.Normal), (11, 1, EnemyType.Elite), (12, 1, EnemyType.Elite)),
                    Wave((1, 9, EnemyType.Normal), (100, 1, EnemyType.Boss)),
                },
            };
            _manager = new WaveManager(
                _provider.GetRequiredService<IPublisher<WaveStarted>>(),
                _provider.GetRequiredService<ISubscriber<WaveCompleted>>(),
                stage);
            _manager.Initialize();
        }

        private static WaveDefinition Wave(params (int id, int count, EnemyType type)[] spawns) => new()
        {
            Spawns = spawns.Select(s => new WaveSpawn { MonsterId = s.id, Count = s.count, Type = s.type }).ToList()
        };

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            _capture.Dispose();
        }

        [Test]
        public void Initialize_StartsFirstWaveFromStageTable()
        {
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual(1, _started[0].WaveIndex);
            Assert.AreEqual(5, _started[0].EnemyCount);
            Assert.IsFalse(_started[0].IsFinalWave);
        }

        [Test]
        public void WaveCompleted_AdvancesThroughWavesAndMarksLastAsFinal()
        {
            _waveCompleted.Publish(new WaveCompleted(1, false));
            _waveCompleted.Publish(new WaveCompleted(2, false));

            Assert.AreEqual(3, _started.Count);
            Assert.AreEqual(8, _started[1].EnemyCount);
            CollectionAssert.AreEqual(new[] { 1, 11, 12 }, _started[1].Spawns.Select(s => s.MonsterId).ToArray());
            Assert.AreEqual(10, _started[2].EnemyCount);
            Assert.AreEqual(EnemyType.Boss, _started[2].Spawns[1].Type);
            Assert.IsTrue(_started[2].IsFinalWave);
        }

        [Test]
        public void WaveCompleted_AfterFinalWave_StartsNothingMore()
        {
            _waveCompleted.Publish(new WaveCompleted(1, false));
            _waveCompleted.Publish(new WaveCompleted(2, false));
            _waveCompleted.Publish(new WaveCompleted(3, true));

            Assert.AreEqual(3, _started.Count);
        }

        [Test]
        public void WaveCompleted_DuplicateOrOutOfOrder_DoesNotAdvance()
        {
            _waveCompleted.Publish(new WaveCompleted(2, false));
            Assert.AreEqual(1, _started.Count);
            _waveCompleted.Publish(new WaveCompleted(1, false));
            _waveCompleted.Publish(new WaveCompleted(1, false));
            Assert.AreEqual(2, _started.Count);
        }
    }
}
