using Game.Core;
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
        // 스테이지와 보상 규칙이 공유하는 총 웨이브 수를 함께 표시한다.
        [SerializeField] private TMP_Text _waveValueText;
        [SerializeField] private Image _gaugeFill;
        private WaveGaugeChanged _gauge;
        private bool _selectingCard;

        [Inject]
        public void Construct(IBufferedSubscriber<WaveGaugeChanged> gaugeChanged,
            IBufferedSubscriber<StageStateChanged> stateChanged)
        {
            Track(gaugeChanged.Subscribe(OnGaugeChanged));
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        private void OnGaugeChanged(WaveGaugeChanged message)
        {
            _gauge = message;
            RenderGauge();
        }

        private void OnStateChanged(StageStateChanged message)
        {
            if (message.State == StageState.CardSelect) { _selectingCard = true; }
            else if (message.State != StageState.Paused) { _selectingCard = false; }
            RenderGauge();
        }

        private void RenderGauge()
        {
            // Max 0 = 첫 웨이브 시작 전 (Buffered struct는 구독 즉시 기본값이 온다)
            if (_gauge.Max <= 0)
            {
                _waveValueText.text = string.Empty;
                _gaugeFill.fillAmount = 0f;
                return;
            }

            // 다음 웨이브 신호는 카드 선택 전에 온다. 선택 중에는 방금 끝낸 웨이브를 표시한다.
            int index = _selectingCard ? Mathf.Max(1, _gauge.WaveIndex - 1) : _gauge.WaveIndex;
            _waveValueText.text = $"{index}/{StageRewardRules.WaveCount}";
            _gaugeFill.fillAmount = _selectingCard ? 1f : (float)_gauge.Current / _gauge.Max;
        }
    }
}
