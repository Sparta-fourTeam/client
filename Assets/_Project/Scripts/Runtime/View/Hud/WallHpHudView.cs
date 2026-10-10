using DG.Tweening;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class WallHpHudView : HudView
    {
        private const float FillSeconds = 0.2f;
        private const float HitSeconds = 0.25f;
        private static readonly Color HitColor = new Color(1f, 0.35f, 0.3f);

        [SerializeField] private Image _hpFill;
        [SerializeField] private TMP_Text _hpText;

        private Tween _fillTween;
        private Tween _hitTween;
        private Color _textColor;
        private int _lastCurrent = -1;

        [Inject]
        public void Construct(IBufferedSubscriber<WallHpChanged> hpChanged)
        {
            _textColor = _hpText.color;
            Track(hpChanged.Subscribe(OnHpChanged));
        }

        private void OnHpChanged(WallHpChanged message)
        {
            // Max 0 = 벽이 아직 Initialize되지 않음
            if (message.Max <= 0)
            {
                _fillTween?.Kill();
                _hpFill.fillAmount = 0f;
                _hpText.text = "-";
                _lastCurrent = -1;
                return;
            }

            bool damaged = _lastCurrent >= 0 && message.Current < _lastCurrent;
            bool animate = _lastCurrent >= 0 && isActiveAndEnabled;
            _lastCurrent = message.Current;

            _hpText.text = $"{message.Current} / {message.Max}";
            SetFill((float)message.Current / message.Max, animate);
            if (damaged)
            {
                PlayHit();
            }
        }

        private void SetFill(float value, bool animate)
        {
            _fillTween?.Kill();
            if (!animate)
            {
                _hpFill.fillAmount = value;
                return;
            }

            _fillTween = DOTween.To(() => _hpFill.fillAmount, v => _hpFill.fillAmount = v, value, FillSeconds)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        // 벽이 맞으면 체력 글자가 붉게 번쩍이며 흔들린다
        private void PlayHit()
        {
            _hitTween?.Kill(true);
            _hpText.color = HitColor;
            var rect = _hpText.rectTransform;
            var home = rect.anchoredPosition;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            seq.Join(DOTween.To(() => 1f, t => rect.anchoredPosition = home + new Vector2(Mathf.Sin(t * 40f) * 6f * t, 0f), 0f, HitSeconds));
            seq.Join(DOTween.To(() => HitColor, c => _hpText.color = c, _textColor, HitSeconds).SetEase(Ease.OutQuad));
            seq.OnKill(() => rect.anchoredPosition = home);
            _hitTween = seq;
        }

        protected override void DisposeView()
        {
            _fillTween?.Kill();
            _hitTween?.Kill();
            base.DisposeView();
        }
    }
}
