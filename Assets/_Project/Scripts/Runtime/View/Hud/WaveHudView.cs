using DG.Tweening;
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
        private const float FillSeconds = 0.25f;
        private const float PunchSeconds = 0.3f;

        // 스테이지와 보상 규칙이 공유하는 총 웨이브 수를 함께 표시한다.
        [SerializeField] private TMP_Text _waveValueText;
        [SerializeField] private Image _gaugeFill;
        [Tooltip("게이지가 찬 끝을 따라다니는 광채. 비우면 표시하지 않는다")]
        [SerializeField] private RectTransform _gaugeStar;

        private WaveGaugeChanged _gauge;
        private bool _selectingCard;
        private Tween _fillTween;
        private Tween _punchTween;
        private int _shownWave;

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
                SetFill(0f, false);
                _shownWave = 0;
                return;
            }

            // 다음 웨이브 신호는 카드 선택 전에 온다. 선택 중에는 방금 끝낸 웨이브를 표시한다.
            int index = _selectingCard ? Mathf.Max(1, _gauge.WaveIndex - 1) : _gauge.WaveIndex;
            _waveValueText.text = $"{index}/{StageRewardRules.WaveCount}";

            // 같은 웨이브 안에서 차오를 때만 부드럽게 채운다. 새 웨이브로 넘어가 다시 비워질 때는 줄어드는 모습 없이 바로 비운다
            float target = _selectingCard ? 1f : (float)_gauge.Current / _gauge.Max;
            bool animate = index == _shownWave && target >= _gaugeFill.fillAmount;
            SetFill(target, animate);

            if (_shownWave != 0 && index != _shownWave)
            {
                PunchWaveText();
            }
            _shownWave = index;
        }

        private void SetFill(float value, bool animate)
        {
            _fillTween?.Kill();
            if (!animate || !isActiveAndEnabled)
            {
                _gaugeFill.fillAmount = value;
                PlaceStar();
                return;
            }

            _fillTween = DOTween.To(() => _gaugeFill.fillAmount, v => { _gaugeFill.fillAmount = v; PlaceStar(); }, value, FillSeconds)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        // 광채를 게이지가 찬 끝에 둔다. 채움 이미지의 로컬 좌표로 끝을 구해 앵커 설정과 상관없이 맞는다
        private void PlaceStar()
        {
            if (_gaugeStar == null) { return; }
            var rect = ((RectTransform)_gaugeFill.transform).rect;
            var local = new Vector3(rect.xMin + rect.width * _gaugeFill.fillAmount, rect.center.y, 0f);
            _gaugeStar.position = _gaugeFill.transform.TransformPoint(local);
        }

        // 웨이브가 바뀌면 숫자가 한 번 튀어 올라 알린다
        private void PunchWaveText()
        {
            if (!isActiveAndEnabled) { return; }
            _punchTween?.Kill(true);
            var rect = _waveValueText.rectTransform;
            rect.localScale = Vector3.one;
            _punchTween = rect.DOPunchScale(Vector3.one * 0.35f, PunchSeconds, 1, 0f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        protected override void OnDestroy()
        {
            _fillTween?.Kill();
            _punchTween?.Kill();
            base.OnDestroy();
        }
    }
}
