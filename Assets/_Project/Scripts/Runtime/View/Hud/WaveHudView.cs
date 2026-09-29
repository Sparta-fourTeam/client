using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class WaveHudView : HudView
    {
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private Image _gaugeFill;

        [Inject]
        public void Construct(IBufferedSubscriber<WaveGaugeChanged> gaugeChanged)
        {
            Track(gaugeChanged.Subscribe(OnGaugeChanged));
        }

        private void OnGaugeChanged(WaveGaugeChanged message)
        {
            // Max 0 = 첫 웨이브 시작 전 (Buffered struct는 구독 즉시 기본값이 온다)
            if (message.Max <= 0)
            {
                _waveText.text = string.Empty;
                _gaugeFill.fillAmount = 0f;
                return;
            }

            _waveText.text = $"WAVE {message.WaveIndex}";
            _gaugeFill.fillAmount = (float)message.Current / message.Max;
        }
    }
}
