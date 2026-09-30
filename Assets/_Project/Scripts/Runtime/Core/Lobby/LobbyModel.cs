using System;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    public class LobbyModel : IStartable, IDisposable
    {
        private readonly PlayerProfile _profile;
        private readonly IBufferedPublisher<WalletChanged> _walletChangedPublisher;

        public LobbyModel(PlayerProfile profile, IBufferedPublisher<WalletChanged> walletChangedPublisher)
        {
            _profile = profile;
            _walletChangedPublisher = walletChangedPublisher;
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

        public void OnProfileChanged(PlayerProfile profile)
        {
            _walletChangedPublisher.Publish(new WalletChanged(profile.Gold));
        }
    }
}
