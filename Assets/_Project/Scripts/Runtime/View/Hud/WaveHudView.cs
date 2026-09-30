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
        // "WAVE" 글자는 프리팹의 고정 텍스트이고, 여기서는 번호만 바꾼다
        [SerializeField] private TMP_Text _waveValueText;
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
                _waveValueText.text = string.Empty;
                _gaugeFill.fillAmount = 0f;
                return;
            }

            // TODO : WaveStarted에 총 웨이브 수가 생기면 "{index}/{total}"로 표시
            _waveValueText.text = message.WaveIndex.ToString();
            _gaugeFill.fillAmount = (float)message.Current / message.Max;
        }
    }
}
