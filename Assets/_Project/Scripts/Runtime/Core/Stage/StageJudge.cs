using System;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core.Stage
{
    public sealed class StageJudge : IInitializable, IDisposable
    {
        private readonly ISubscriber<WallDestroyed> _wallDestroyedSubscriber;
        private readonly ISubscriber<AllEnemiesCleared> _allEnemiesClearedSubscriber;
        private readonly IPublisher<StageEnded> _stageEndedPublisher;

        private IDisposable _subscriptions;

        private bool _judged;

        public StageJudge(
            ISubscriber<WallDestroyed> wallDestroyedSubscriber,
            ISubscriber<AllEnemiesCleared> allEnemiesClearedSubscriber,
            IPublisher<StageEnded> stageEndedPublisher)
        {
            _wallDestroyedSubscriber = wallDestroyedSubscriber;
            _allEnemiesClearedSubscriber = allEnemiesClearedSubscriber;
            _stageEndedPublisher = stageEndedPublisher;
        }

        public void Initialize()
        {
            DisposableBagBuilder bag = DisposableBag.CreateBuilder();
            _wallDestroyedSubscriber.Subscribe(_ => Judge(StageOutcome.Fail)).AddTo(bag);
            _allEnemiesClearedSubscriber.Subscribe(_ => Judge(StageOutcome.Clear)).AddTo(bag);
            _subscriptions = bag.Build();
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
        }


        private void Judge(StageOutcome outcome)
        {
            if (_judged)
            {
                return;
            }

            _judged = true;
            _stageEndedPublisher.Publish(new StageEnded(outcome));
        }
    }
}
