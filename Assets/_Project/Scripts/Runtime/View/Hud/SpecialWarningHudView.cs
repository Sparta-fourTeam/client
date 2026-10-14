using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>엘리트·보스가 나오기 직전의 경고 연출. 모양은 프리팹의 이미지가 맡고, 여기서는 트윈으로 움직이기만 한다</summary>
    public sealed class SpecialWarningHudView : HudView
    {
        private const float InSeconds = 0.25f, OutSeconds = 0.3f;
        private const float PulseSeconds = 0.35f;
        private const float BossShakeStrength = 14f;

        [Tooltip("연출 전체를 켜고 끄는 루트. 평소에는 꺼 둔다")]
        [SerializeField] private CanvasGroup _root;
        [Tooltip("화면 가장자리 위험 테두리. 경고 동안 맥박처럼 깜빡인다")]
        [SerializeField] private Image _frame;
        [SerializeField] private RectTransform _topBand;
        [SerializeField] private RectTransform _bottomBand;
        [Tooltip("WARNING 글씨")]
        [SerializeField] private RectTransform _text;
        [Range(0f, 1f)][SerializeField] private float _eliteFrameAlpha = 0.55f;
        [Range(0f, 1f)][SerializeField] private float _bossFrameAlpha = 0.9f;

        private Sequence _sequence;
        private Tween _pulse;
        private Vector2 _topHome, _bottomHome, _textHome;

        [Inject]
        public void Construct(ISubscriber<SpecialEnemyWarning> warning)
        {
            Track(warning.Subscribe(OnWarning));
        }

        protected override void InitializeView()
        {
            _topHome = _topBand.anchoredPosition;
            _bottomHome = _bottomBand.anchoredPosition;
            _textHome = _text.anchoredPosition;
            _root.alpha = 0f;
            _root.gameObject.SetActive(false);
        }

        private void OnWarning(SpecialEnemyWarning message)
        {
            Kill();
            bool boss = message.Type == EnemyType.Boss;
            float peak = boss ? _bossFrameAlpha : _eliteFrameAlpha;
            float hold = Mathf.Max(0.1f, message.Seconds - InSeconds - OutSeconds);

            _root.gameObject.SetActive(true);
            _root.alpha = 0f;
            SetAlpha(_frame, peak);
            _topBand.anchoredPosition = _topHome + new Vector2(-_topBand.rect.width, 0f);
            _bottomBand.anchoredPosition = _bottomHome + new Vector2(_bottomBand.rect.width, 0f);
            _text.anchoredPosition = _textHome;
            _text.localScale = Vector3.one * 1.6f;

            _sequence = DOTween.Sequence();
            _sequence.Append(DOTween.To(() => _root.alpha, a => _root.alpha = a, 1f, InSeconds));
            _sequence.Join(Slide(_topBand, _topHome, InSeconds));
            _sequence.Join(Slide(_bottomBand, _bottomHome, InSeconds));
            _sequence.Join(_text.DOScale(1f, InSeconds).SetEase(Ease.OutBack));
            if (boss) { _sequence.Append(Shake(_text, _textHome, hold)); }
            else { _sequence.AppendInterval(hold); }
            _sequence.Append(DOTween.To(() => _root.alpha, a => _root.alpha = a, 0f, OutSeconds));
            _sequence.OnComplete(() => _root.gameObject.SetActive(false));

            // 테두리는 맥박처럼 깜빡인다
            float low = peak * 0.35f;
            _pulse = DOTween.To(() => _frame.color.a, a => SetAlpha(_frame, a), low, PulseSeconds)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            _sequence.OnKill(() => _pulse?.Kill());
        }

        private static Tween Slide(RectTransform rect, Vector2 home, float seconds) =>
            DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, home, seconds).SetEase(Ease.OutCubic);

        // 보스는 글씨가 떨린다. 끝나면 제자리로 돌아온다
        private static Tween Shake(RectTransform rect, Vector2 home, float seconds) =>
            DOTween.To(() => 0f, t =>
            {
                float fade = 1f - t;
                rect.anchoredPosition = home + new Vector2(Mathf.Sin(t * 90f), Mathf.Cos(t * 70f)) * (BossShakeStrength * fade);
            }, 1f, seconds).SetEase(Ease.Linear).OnComplete(() => rect.anchoredPosition = home);

        private static void SetAlpha(Image image, float alpha)
        {
            var c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private void Kill()
        {
            _sequence?.Kill();
            _pulse?.Kill();
        }

        protected override void DisposeView()
        {
            Kill();
            base.DisposeView();
        }
    }
}
