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

        private Tween _fillTween;
        private Tween _punchTween;
        private int _shownWave;

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
                SetFill(0f, false);
                _shownWave = 0;
                return;
            }

            _waveValueText.text = $"{message.WaveIndex}/{StageRewardRules.WaveCount}";
            // 새 웨이브로 넘어가 게이지가 다시 차오르기 시작할 때는 줄어드는 모습 없이 바로 비운다
            float target = (float)message.Current / message.Max;
            bool animate = message.WaveIndex == _shownWave && target >= _gaugeFill.fillAmount;
            SetFill(target, animate);

            if (_shownWave != 0 && message.WaveIndex != _shownWave)
            {
                PunchWaveText();
            }
            _shownWave = message.WaveIndex;
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
