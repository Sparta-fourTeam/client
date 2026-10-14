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

        private const float LagDelaySeconds = 0.45f;
        private const float LagSeconds = 0.7f;
        private const float FxEmitSeconds = 0.12f;
        private const float LowHpRatio = 0.3f;
        private static readonly Color SoulColor = new Color(0.24f, 0.92f, 0.71f, 1f);
        private static readonly Color LowColor = new Color(1f, 0.31f, 0.43f, 1f);

        [SerializeField] private Image _hpFill;
        [SerializeField] private TMP_Text _hpText;

        [Header("연출 (비워 두면 그 연출만 빠진다)")]
        [Tooltip("깎인 만큼 다른 색으로 남았다가 천천히 줄어드는 판. 체력 채움 바로 뒤에 같은 자리로 둔다")]
        [SerializeField] private Image _hpLag;
        [Tooltip("왼쪽 혼령 메달. 숨 쉬듯 떠 있고 맞으면 흔들린다")]
        [SerializeField] private RectTransform _symbol;
        [Tooltip("메달 안의 불꽃. 일렁인다")]
        [SerializeField] private RectTransform _flame;
        [Tooltip("메달 뒤에서 숨 쉬는 빛. 체력이 적으면 붉어진다")]
        [SerializeField] private Image _glow;
        [Tooltip("맞는 순간 터지는 파티클 (UIParticle 프리팹의 ParticleSystem). 반복 파티클로 두면 코드가 짧게 재생했다 멈춘다")]
        [SerializeField] private ParticleSystem[] _hitFx;

        private Tween _fillTween;
        private Tween _lagTween;
        private Tween _hitTween;
        private Tween _fxStop;
        private Tween _glowTween;
        private Color _textColor;
        private int _lastCurrent = -1;
        private bool _low;
        private Vector2 _symbolHome;

        [Inject]
        public void Construct(IBufferedSubscriber<WallHpChanged> hpChanged)
        {
            _textColor = _hpText.color;
            Track(hpChanged.Subscribe(OnHpChanged));
        }

        protected override void InitializeView()
        {
            StartIdle();
        }

        // 혼령 메달은 가만히 있지 않는다: 둥실 떠오르고, 불꽃이 일렁이고, 뒤의 빛이 숨을 쉰다
        private void StartIdle()
        {
            if (_symbol != null)
            {
                _symbolHome = _symbol.anchoredPosition;
                DOTween.To(() => 0f, t => _symbol.anchoredPosition = _symbolHome + new Vector2(0f, Mathf.Sin(t) * 3.5f), Mathf.PI * 2f, 2.4f)
                    .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetUpdate(true).SetLink(gameObject);
            }

            if (_flame != null)
            {
                _flame.DOScaleY(1.07f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
                _flame.DOScaleX(0.96f, 0.83f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
                _flame.DOLocalRotate(new Vector3(0f, 0f, 2.2f), 1.25f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
            }

            if (_glow != null)
            {
                _glow.color = WithAlpha(SoulColor, 0.35f);
                _glowTween = DOTween.To(() => _glow.color.a, a => _glow.color = WithAlpha(_glow.color, a), 0.8f, 1.1f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void OnHpChanged(WallHpChanged message)
        {
            // Max 0 = 벽이 아직 Initialize되지 않음
            if (message.Max <= 0)
            {
                _fillTween?.Kill();
                _lagTween?.Kill();
                _hpFill.fillAmount = 0f;
                if (_hpLag != null) { _hpLag.fillAmount = 0f; }
                _hpText.text = "-";
                _lastCurrent = -1;
                return;
            }

            bool damaged = _lastCurrent >= 0 && message.Current < _lastCurrent;
            bool animate = _lastCurrent >= 0 && isActiveAndEnabled;
            _lastCurrent = message.Current;

            _hpText.text = $"{message.Current} / {message.Max}";
            float ratio = (float)message.Current / message.Max;
            SetLag(ratio, damaged && animate);
            SetFill(ratio, animate);
            SetLow(ratio <= LowHpRatio);
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

        // 맞으면 깎인 만큼이 다른 색으로 남았다가, 잠시 뒤 천천히 줄어든다. 회복이나 처음 표시는 바로 따라간다
        private void SetLag(float value, bool damaged)
        {
            if (_hpLag == null) { return; }
            _lagTween?.Kill();
            if (!damaged)
            {
                _hpLag.fillAmount = value;
                return;
            }

            // 연타로 맞아도 아직 남아 있는 깎인 양은 그대로 두고, 새로 깎인 만큼만 더한다
            _hpLag.fillAmount = Mathf.Max(_hpLag.fillAmount, _hpFill.fillAmount);
            _lagTween = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .AppendInterval(LagDelaySeconds)
                .Append(DOTween.To(() => _hpLag.fillAmount, v => _hpLag.fillAmount = v, value, LagSeconds).SetEase(Ease.InOutQuad));
        }

        private void SetLow(bool low)
        {
            if (_low == low || _glow == null) { return; }
            _low = low;
            _glow.color = WithAlpha(low ? LowColor : SoulColor, _glow.color.a);
        }

        // 벽이 맞으면 체력 글자가 붉게 번쩍이며 흔들리고, 혼령 메달도 움찔하며 파티클이 튄다
        private void PlayHit()
        {
            PlaySymbolHit();
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

        private void PlaySymbolHit()
        {
            if (_symbol != null)
            {
                _symbol.DOKill(true);
                _symbol.DOPunchScale(Vector3.one * -0.18f, 0.3f, 8, 0.6f).SetUpdate(true).SetLink(gameObject);
            }

            if (_glow != null)
            {
                _glowTween?.Kill();
                _glow.color = WithAlpha(LowColor, 1f);
                _glowTween = DOTween.To(() => _glow.color.a, a => _glow.color = WithAlpha(_glow.color, a), 0.35f, 0.5f)
                    .SetUpdate(true).SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        _glow.color = WithAlpha(_low ? LowColor : SoulColor, 0.35f);
                        _glowTween = DOTween.To(() => _glow.color.a, a => _glow.color = WithAlpha(_glow.color, a), 0.8f, 1.1f)
                            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(gameObject);
                    });
            }

            if (_hitFx == null || _hitFx.Length == 0) { return; }
            foreach (var fx in _hitFx)
            {
                fx.Play(true);
            }

            _fxStop?.Kill();
            _fxStop = DOVirtual.DelayedCall(FxEmitSeconds, () =>
            {
                foreach (var fx in _hitFx)
                {
                    fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }, true).SetLink(gameObject);
        }

        protected override void DisposeView()
        {
            _lagTween?.Kill();
            _fxStop?.Kill();
            _glowTween?.Kill();
            _fillTween?.Kill();
            _hitTween?.Kill();
            base.DisposeView();
        }
    }
}
