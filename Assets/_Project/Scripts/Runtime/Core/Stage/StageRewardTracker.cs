using System;
using System.Linq;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>웨이브 완료 시점의 확보 보상. 영구 인벤토리는 변경하지 않는다.</summary>
    public sealed class StageRewardTracker : IInitializable, IDisposable
    {
        private readonly ISubscriber<WaveCompleted> _completed;
        private readonly ISubscriber<StageEnded> _ended;
        private readonly StageRewardBalance _balance;
        private readonly StageItemCache _cache;
        private IDisposable _subscriptions;
        private bool _stopped;
        public int CompletedWaves { get; private set; }
        public StageRewards Rewards { get; private set; }

        public StageRewardTracker(ISubscriber<WaveCompleted> completed, ISubscriber<StageEnded> ended,
            StageRewardBalance balance, StageItemCache cache)
        { _completed = completed; _ended = ended; _balance = balance; _cache = cache; }

        public void Initialize()
        {
            _cache.Begin(_balance.StageId, StageRewardRules.Maximum(_balance).Items.Select(item => item.itemId));
            Refresh();
            var bag = DisposableBag.CreateBuilder();
            _completed.Subscribe(message =>
            {
                if (_stopped || message.WaveIndex != CompletedWaves + 1 || message.WaveIndex > StageRewardRules.WaveCount) { return; }
                CompletedWaves = message.WaveIndex;
                Refresh();
            }).AddTo(bag);
            _ended.Subscribe(_ => Stop()).AddTo(bag);
            _subscriptions = bag.Build();
        }

        private void Refresh()
        {
            Rewards = StageRewardRules.Calculate(_balance, CompletedWaves, false, 0);
            _cache.Begin(_balance.StageId, _cache.ObtainableItemIds);
            foreach (var item in Rewards.Items) { _cache.RecordAcquired(item.itemId, item.quantity); }
        }

        public void Stop() => _stopped = true;
        public void Dispose() => _subscriptions?.Dispose();
    }
}
