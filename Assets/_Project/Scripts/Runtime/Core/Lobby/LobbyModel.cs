using System;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    public sealed class LobbyModel : IStartable, IDisposable
    {
        private readonly PlayerProfile _profile;
        private readonly IBufferedPublisher<WalletChanged> _walletChangedPublisher;
        private readonly IBufferedPublisher<ProgressChanged> _progressChangedPublisher;

        public LobbyModel(
            PlayerProfile profile,
            IBufferedPublisher<WalletChanged> walletChangedPublisher,
            IBufferedPublisher<ProgressChanged> progressChangedPublisher)
        {
            _profile = profile;
            _walletChangedPublisher = walletChangedPublisher;
            _progressChangedPublisher = progressChangedPublisher;
        }

        public void Start()
        {
            _profile.Changed += OnProfileChanged;
            OnProfileChanged(_profile);
        }

        public void Dispose()
        {
            _profile.Changed -= OnProfileChanged;
        }

        private void OnProfileChanged(PlayerProfile profile)
        {
            _walletChangedPublisher.Publish(new WalletChanged(profile.Gold));
            _progressChangedPublisher.Publish(new ProgressChanged(profile.HighestUnlockedStage));
        }
    }
}
