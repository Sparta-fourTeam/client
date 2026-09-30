using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public sealed class WalletView : HudView
    {
        [SerializeField] private TMP_Text _value;

        [Inject]
        public void Construct(IBufferedSubscriber<WalletChanged> walletChangedSubscriber)
        {
            Track(walletChangedSubscriber.Subscribe(m => _value.text = CurrencyFormat.Format(m.Gold)));
        }

    }
}
