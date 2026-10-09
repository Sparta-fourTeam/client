using DG.Tweening;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public sealed class WalletView : HudView
    {
        private const float CountSeconds = 0.5f;

        [SerializeField] private TMP_Text _value;

        private Tween _countTween;
        private int _shown;
        private bool _hasValue;

        [Inject]
        public void Construct(IBufferedSubscriber<WalletChanged> walletChangedSubscriber)
        {
            Track(walletChangedSubscriber.Subscribe(OnWalletChanged));
        }

        // 처음 값은 바로 보이고, 이후 변화는 숫자가 새 값까지 올라가거나 내려간다
        private void OnWalletChanged(WalletChanged message)
        {
            int target = message.Gold;
            _countTween?.Kill();
            if (!_hasValue || !isActiveAndEnabled || target == _shown)
            {
                _hasValue = true;
                Show(target);
                return;
            }

            int from = _shown;
            _countTween = DOTween.To(() => 0f, t => Show(from + (int)((target - from) * t)), 1f, CountSeconds)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => Show(target));
        }

        private void Show(int value)
        {
            _shown = value;
            _value.text = CurrencyFormat.Format(value);
        }

        protected override void OnDestroy()
        {
            _countTween?.Kill();
            base.OnDestroy();
        }
    }
}
