using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public class WallHpHudView : HudView
    {
        [SerializeField] private Image _hpFill;
        [SerializeField] private TMP_Text _hpText;

        [Inject]
        public void Construct(IBufferedSubscriber<WallHpChanged> hpChanged)
        {
            Track(hpChanged.Subscribe(OnHpChanged));
        }

        private void OnHpChanged(WallHpChanged message)
        {
            // Max 0 = 벽이 아직 Initialize되지 않음
            if (message.Max <= 0)
            {
                _hpFill.fillAmount = 0f;
                _hpText.text = "-";
                return;
            }

            _hpFill.fillAmount = (float)message.Current / message.Max;
            _hpText.text = $"{message.Current} / {message.Max}";
        }
    }
}
