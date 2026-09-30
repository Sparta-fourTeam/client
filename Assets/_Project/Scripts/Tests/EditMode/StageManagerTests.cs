using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class StageManagerTests
    {
        private sealed class FakeBattleApi : IBattleApi
        {
            public SubmitResultRequest LastRequest;
            public Exception ToThrow;

            public UniTask<StartBattleResponse> StartBattle(int stageId, int dataRevision)
            {
                throw new NotSupportedException();
            }

            public UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest req)
            {
                LastRequest = req;
                if (ToThrow != null)
                {
                    throw ToThrow;
                }

                return UniTask.FromResult(new SubmitResultResponse { cleared = req.cleared, rewardGold = 100 });
            }
        }

        private IPublisher<StageEnded> _stageEnded;
        private IPublisher<WaveGaugeFilled> _gaugeFilled;
        private ISubscriber<StageStateChanged> _stateChanged;
        private ISubscriber<StageResult> _resultSubscriber;
        private ISubscriber<SubmitRejected> _rejectedSubscriber;
        private ISubscriber<SubmitFailed> _failedSubscriber;
        private FakeBattleApi _api;
        private BattleStats _stats;
        private StageClock _clock;
        private StageManager _manager;
        private readonly List<StageState> _states = new();
        private readonly List<StageResult> _results = new();
        private readonly List<SubmitRejected> _rejected = new();
        private readonly List<SubmitFailed> _failed = new();
        private readonly List<IDisposable> _subscriptions = new();

        [SetUp]
        public void SetUp()
        {
            _states.Clear();
            _results.Clear();
            _rejected.Clear();
            _failed.Clear();
            _subscriptions.Clear();

            var builder = new BuiltinContainerBuilder();
            builder.AddMessagePipe();
            builder.AddMessageBroker<StageStateChanged>();
            builder.AddMessageBroker<StageEnded>();
            builder.AddMessageBroker<StageResult>();
            builder.AddMessageBroker<SubmitRejected>();
            builder.AddMessageBroker<SubmitFailed>();
            builder.AddMessageBroker<WaveGaugeFilled>();
            builder.AddMessageBroker<EnemyDied>();
            builder.AddMessageBroker<WaveGaugeChanged>();
            builder.AddMessageBroker<WallHpChanged>();
            IServiceProvider provider = builder.BuildServiceProvider();

            _stageEnded = provider.GetRequiredService<IPublisher<StageEnded>>();
            _gaugeFilled = provider.GetRequiredService<IPublisher<WaveGaugeFilled>>();
            _stateChanged = provider.GetRequiredService<ISubscriber<StageStateChanged>>();
            _resultSubscriber = provider.GetRequiredService<ISubscriber<StageResult>>();
            _rejectedSubscriber = provider.GetRequiredService<ISubscriber<SubmitRejected>>();
            _subscriptions.Add(_stateChanged.Subscribe(e => _states.Add(e.State)));
            _subscriptions.Add(_resultSubscriber.Subscribe(_results.Add));
            _subscriptions.Add(_rejectedSubscriber.Subscribe(_rejected.Add));
            _failedSubscriber = provider.GetRequiredService<ISubscriber<SubmitFailed>>();
            _subscriptions.Add(_failedSubscriber.Subscribe(_failed.Add));

            _api = new FakeBattleApi();

            _stats = new BattleStats(
                provider.GetRequiredService<ISubscriber<EnemyDied>>(),
                provider.GetRequiredService<ISubscriber<WaveGaugeChanged>>(),
                provider.GetRequiredService<ISubscriber<StageEnded>>(),
                provider.GetRequiredService<ISubscriber<WallHpChanged>>());
            _clock = new StageClock(_stateChanged);

            var context = new StageContext();
            context.SetBattle("battle-1", 1);

            _manager = new StageManager(
                provider.GetRequiredService<IBufferedPublisher<StageStateChanged>>(),
                provider.GetRequiredService<ISubscriber<StageEnded>>(),
                provider.GetRequiredService<IPublisher<StageResult>>(),
                _api,
                context,
                _stats,
                _clock,
                provider.GetRequiredService<ISubscriber<WaveGaugeFilled>>(),
                null,
                provider.GetRequiredService<IPublisher<SubmitRejected>>(),
                provider.GetRequiredService<IPublisher<SubmitFailed>>());
            _manager.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            PlayerPrefs.DeleteKey("stage_speed");
            Time.timeScale = 1f;
        }

        private void FillGauge(bool isFinalWave)
        {
            _gaugeFilled.Publish(new WaveGaugeFilled(isFinalWave));
        }

        [Test(Description = "Start하면 Playing으로 전환되고 상태 변경이 발행된다")]
        public void Start_EntersPlaying()
        {
            Assert.AreEqual(StageState.Playing, _manager.State);
            CollectionAssert.AreEqual(new[] { StageState.Playing }, _states);
        }

        [Test(Description = "Playing에서 Pause하면 시간이 멈추고 Paused가 된다")]
        public void Pause_FromPlaying_StopsTime()
        {
            _manager.Pause();

            Assert.AreEqual(StageState.Paused, _manager.State);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test(Description = "Paused에서 Resume하면 Playing으로 돌아오고 배속이 복구된다")]
        public void Resume_ToPlaying_RestoresSpeed()
        {
            _manager.SetSpeed(2f);
            _manager.Pause();

            _manager.Resume();

            Assert.AreEqual(StageState.Playing, _manager.State);
            Assert.AreEqual(2f, Time.timeScale);
        }

        [Test(Description = "Playing이 아닐 때 Resume은 무시된다")]
        public void Resume_WhenNotPaused_IsIgnored()
        {
            _manager.Resume();

            Assert.AreEqual(StageState.Playing, _manager.State);
            CollectionAssert.AreEqual(new[] { StageState.Playing }, _states);
        }

        [Test(Description = "SetSpeed는 2배 이상이면 2배, 아니면 1배로 고정하고 Playing에서만 timeScale에 반영한다")]
        public void SetSpeed_AppliesOnlyWhilePlaying()
        {
            _manager.SetSpeed(5f);
            Assert.AreEqual(2f, Time.timeScale);

            _manager.SetSpeed(1.5f);
            Assert.AreEqual(1f, Time.timeScale);

            _manager.Pause();
            _manager.SetSpeed(2f);
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test(Description = "마지막 웨이브가 끝나면 카드 선택으로 가지 않는다")]
        public void GaugeFilled_Final_StaysPlaying()
        {
            FillGauge(true);

            Assert.AreEqual(StageState.Playing, _manager.State);
        }

        [Test(Description = "Playing이 아닐 때 게이지가 차도 카드 선택으로 가지 않는다")]
        public void GaugeFilled_WhilePaused_IsIgnored()
        {
            _manager.Pause();

            FillGauge(false);

            Assert.AreEqual(StageState.Paused, _manager.State);
        }

        [Test(Description = "CardSelect가 아닐 때 PickCard는 무시된다")]
        public void PickCard_WhenNotCardSelect_IsIgnored()
        {
            _manager.PickCard(0);

            Assert.AreEqual(StageState.Playing, _manager.State);
            Assert.AreEqual(0, _stats.BuildLog.Count);
        }

        [Test(Description = "Playing 상태에서는 Forfeit이 무시된다")]
        public void Forfeit_WhilePlaying_IsIgnored()
        {
            _manager.Forfeit();

            Assert.AreEqual(StageState.Playing, _manager.State);
            Assert.IsNull(_api.LastRequest);
        }

        [Test(Description = "Paused에서 Forfeit하면 미클리어로 제출하고 Finished가 된다")]
        public void Forfeit_WhilePaused_SubmitsAsFailed()
        {
            _manager.Pause();

            _manager.Forfeit();

            Assert.AreEqual(StageState.Finished, _manager.State);
            Assert.IsFalse(_api.LastRequest.cleared);
            Assert.AreEqual("battle-1", _api.LastRequest.battleId);
            Assert.AreEqual(0, _api.LastRequest.wallHpPercent);
        }

        [Test(Description = "스테이지가 클리어로 끝나면 결과를 제출하고 StageResult를 발행한 뒤 Finished가 된다")]
        public void StageEnded_Clear_SubmitsAndPublishesResult()
        {
            _stageEnded.Publish(new StageEnded(StageOutcome.Clear));

            Assert.AreEqual(StageState.Finished, _manager.State);
            Assert.IsTrue(_api.LastRequest.cleared);
            Assert.AreEqual(1, _results.Count);
            Assert.IsTrue(_results[0].Cleared);
            Assert.AreEqual(100, _results[0].RewardGold);
            CollectionAssert.AreEqual(
                new[] { StageState.Playing, StageState.Submitting, StageState.Finished }, _states);
        }

        [Test(Description = "결과 제출이 거절되면 SubmitRejected만 발행한다. 안내를 보여주고 로비로 보내는 것은 구독하는 뷰의 몫이다")]
        public void StageEnded_Rejected_PublishesAndWaitsForNotice()
        {
            _api.ToThrow = new ApiException(ApiErrorKind.Rejected, "DUPLICATE_SUBMIT");

            _stageEnded.Publish(new StageEnded(StageOutcome.Fail));

            Assert.AreEqual(1, _rejected.Count);
            Assert.AreEqual("DUPLICATE_SUBMIT", _rejected[0].Code);
            Assert.AreEqual(0, _failed.Count);
            Assert.AreEqual(0, _results.Count);
            Assert.AreEqual(StageState.Submitting, _manager.State);
        }

        [Test(Description = "네트워크나 일시적 오류로 제출이 실패하면 멈추지 않고 SubmitFailed를 발행한다 (재시도는 #89)")]
        [TestCase(ApiErrorKind.Transient, "SERVER_BUSY")]
        [TestCase(ApiErrorKind.Network, "NETWORK")]
        public void StageEnded_TransientOrNetwork_PublishesSubmitFailed(ApiErrorKind kind, string code)
        {
            _api.ToThrow = new ApiException(kind, code);

            _stageEnded.Publish(new StageEnded(StageOutcome.Clear));

            Assert.AreEqual(1, _failed.Count);
            Assert.AreEqual(code, _failed[0].Code);
            Assert.AreEqual(0, _rejected.Count);
            Assert.AreEqual(0, _results.Count);
            Assert.AreEqual(StageState.Submitting, _manager.State);
        }

        [Test(Description = "예상 밖 예외로 제출이 실패해도 Submitting에서 멈추지 않도록 UNEXPECTED로 SubmitFailed를 발행한다")]
        public void StageEnded_UnexpectedException_PublishesSubmitFailedUnexpected()
        {
            _api.ToThrow = new InvalidOperationException("boom");

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("boom"));
            _stageEnded.Publish(new StageEnded(StageOutcome.Fail));

            Assert.AreEqual(1, _failed.Count);
            Assert.AreEqual("UNEXPECTED", _failed[0].Code);
        }

        [Test(Description = "Dispose하면 timeScale이 1로 복구된다")]
        public void Dispose_RestoresTimeScale()
        {
            _manager.Pause();

            _manager.Dispose();

            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
