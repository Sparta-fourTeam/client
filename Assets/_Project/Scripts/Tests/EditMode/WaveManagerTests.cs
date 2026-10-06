using System;
using System.Collections.Generic;
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
        private IPublisher<WaveGaugeFilled> _gaugeFilled;
        private List<WaveStarted> _started;
        private IDisposable _capture;
        private WaveManager _manager;

        [SetUp]
        public void SetUp()
        {
            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<WaveStarted>();
            builder.AddMessageBroker<WaveGaugeFilled>();
            _provider = builder.BuildServiceProvider();

            _gaugeFilled = _provider.GetRequiredService<IPublisher<WaveGaugeFilled>>();
            _started = new List<WaveStarted>();
            _capture = _provider.GetRequiredService<ISubscriber<WaveStarted>>().Subscribe(_started.Add);

            var stage = new StageDefinition
            {
                Waves = new List<WaveDefinition>
                {
                    new WaveDefinition { EnemyCount = 5 },
                    new WaveDefinition { EnemyCount = 8, MaxEliteCount = 2 },
                    new WaveDefinition { EnemyCount = 10, MaxBossCount = 1 },
                },
            };
            _manager = new WaveManager(
                _provider.GetRequiredService<IPublisher<WaveStarted>>(),
                _provider.GetRequiredService<ISubscriber<WaveGaugeFilled>>(),
                stage);
            _manager.Initialize();
        }

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
        public void GaugeFilled_AdvancesThroughWavesAndMarksLastAsFinal()
        {
            _gaugeFilled.Publish(new WaveGaugeFilled(false));
            _gaugeFilled.Publish(new WaveGaugeFilled(false));

            Assert.AreEqual(3, _started.Count);
            Assert.AreEqual(8, _started[1].EnemyCount);
            Assert.AreEqual(2, _started[1].MaxEliteCount);
            Assert.AreEqual(10, _started[2].EnemyCount);
            Assert.AreEqual(1, _started[2].MaxBossCount);
            Assert.IsTrue(_started[2].IsFinalWave);
        }

        [Test]
        public void GaugeFilled_AfterFinalWave_StartsNothingMore()
        {
            _gaugeFilled.Publish(new WaveGaugeFilled(false));
            _gaugeFilled.Publish(new WaveGaugeFilled(false));
            _gaugeFilled.Publish(new WaveGaugeFilled(true));

            Assert.AreEqual(3, _started.Count);
        }
    }
}
