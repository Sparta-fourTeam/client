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
    /// <summary>웨이브 번호와 게이지. 모양은 프리팹의 이미지·파티클이 맡고, 여기서는 값을 반영하고 트윈으로 움직이기만 한다</summary>
    public sealed class WaveHudView : HudView
    {
        private const float FillSeconds = 0.25f;
        private const float PunchSeconds = 0.3f;

        // 별 반짝임: 한 번 커졌다 작아지는 데 걸리는 시간과 크기 범위
        private const float TwinkleSeconds = 0.45f;
        private const float TwinkleMin = 0.85f, TwinkleMax = 1.2f;
        // 게이지가 움직이는 동안 별이 가늘어지는 정도(가로 배율), 가득 찼을 때 가로로 번지는 배율, 웨이브 시작 때 터지는 배율
        private const float MovingThinX = 0.35f, FullFlareX = 2.2f, BurstScale = 1.8f;
        private const float StarShapeSeconds = 0.15f, BurstSeconds = 0.35f;

        // 스테이지와 보상 규칙이 공유하는 총 웨이브 수를 함께 표시한다.
        [SerializeField] private TMP_Text _waveValueText;
        [SerializeField] private Image _gaugeFill;
        [Tooltip("게이지가 찬 끝을 따라다니는 별. 비우면 표시하지 않는다")]
        [SerializeField] private RectTransform _gaugeStar;
        [Tooltip("게이지가 가득 차 카드를 고르는 동안 재생할 번개 파티클. 비우면 재생하지 않는다")]
        [SerializeField] private ParticleSystem _fullLightning;

        private WaveGaugeChanged _gauge;
        private WaveGaugeChanged _previousGauge;
        private bool _selectingCard;
        private bool _deferRender;   // 같은 프레임 안에 상태 전환이 이어질 수 있어 다음 웨이브 표시를 잠시 미룬다
        private int _shownWave;

        private Tween _fillTween;
        private Tween _punchTween;
        private Tween _twinkleTween;
        private Tween _thinTween, _flareTween, _burstTween;

        // 별 모양 배율의 재료. 매 프레임 합쳐서 적용한다
        private float _twinkle = 1f;
        private float _thin;     // 0 = 평소, 1 = 가장 가늘게
        private float _flare;    // 0 = 평소, 1 = 가장 넓게
        private float _burst = 1f;

        [Inject]
        public void Construct(IBufferedSubscriber<WaveGaugeChanged> gaugeChanged,
            IBufferedSubscriber<StageStateChanged> stateChanged)
        {
            Track(gaugeChanged.Subscribe(OnGaugeChanged));
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        private void OnEnable()
        {
            if (_gaugeStar == null || _twinkleTween != null) { return; }
            _twinkleTween = DOTween.To(() => _twinkle, v => { _twinkle = v; ApplyStarShape(); }, TwinkleMax, TwinkleSeconds)
                .From(TwinkleMin)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void OnGaugeChanged(WaveGaugeChanged message)
        {
            _previousGauge = _gauge;
            _gauge = message;

            // 마지막 처치로 웨이브가 끝나면 다음 웨이브의 게이지가 카드 선택 상태보다 먼저 온다.
            // 바로 그리면 가득 찬 모습을 건너뛰고 리셋되므로, 이번 프레임이 끝날 때 상태를 보고 그린다
            if (_previousGauge.Max > 0 && _previousGauge.Current >= _previousGauge.Max
                && _gauge.WaveIndex > _previousGauge.WaveIndex)
            {
                _deferRender = true;
                return;
            }

            RenderGauge();
        }

        private void OnStateChanged(StageStateChanged message)
        {
            if (message.State == StageState.CardSelect) { _selectingCard = true; }
            else if (message.State != StageState.Paused) { _selectingCard = false; }
            _deferRender = false;
            RenderGauge();
        }

        private void LateUpdate()
        {
            if (!_deferRender) { return; }
            _deferRender = false;
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
                SetFull(false);
                return;
            }

            // 다음 웨이브 신호는 카드 선택 전에 온다. 선택 중에는 방금 끝낸 웨이브를 표시한다.
            int index = _selectingCard ? Mathf.Max(1, _gauge.WaveIndex - 1) : _gauge.WaveIndex;
            _waveValueText.text = $"{index}/{StageRewardRules.WaveCount}";

            // 같은 웨이브 안에서 차오를 때만 부드럽게 채운다. 새 웨이브로 넘어가 다시 비워질 때는 줄어드는 모습 없이 바로 비운다
            float target = _selectingCard ? 1f : (float)_gauge.Current / _gauge.Max;
            bool animate = index == _shownWave && target >= _gaugeFill.fillAmount;
            SetFill(target, animate);
            SetFull(_selectingCard);

            if (_shownWave != 0 && index != _shownWave)
            {
                PunchWaveText();
                if (!_selectingCard) { BurstStar(); }
            }
            _shownWave = index;
        }

        private void SetFill(float value, bool animate)
        {
            _fillTween?.Kill();
            // 이미 같은 값이면 움직일 게 없다. 의미 없는 트윈을 만들지 않는다
            if (!animate || !isActiveAndEnabled || Mathf.Approximately(_gaugeFill.fillAmount, value))
            {
                _gaugeFill.fillAmount = value;
                PlaceStar();
                return;
            }

            // 차오르는 동안 별이 가늘어졌다가 멈추면 다시 돌아온다
            TweenTo(ref _thinTween, () => _thin, v => { _thin = v; ApplyStarShape(); }, 1f, StarShapeSeconds);
            _fillTween = DOTween.To(() => _gaugeFill.fillAmount, v => { _gaugeFill.fillAmount = v; PlaceStar(); }, value, FillSeconds)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => TweenTo(ref _thinTween, () => _thin, v => { _thin = v; ApplyStarShape(); }, 0f, StarShapeSeconds));
        }

        // 가득 차 카드를 고르는 동안: 별이 가로로 번지고 번개가 흐른다. 벗어나면 되돌린다
        private void SetFull(bool full)
        {
            TweenTo(ref _flareTween, () => _flare, v => { _flare = v; ApplyStarShape(); }, full ? 1f : 0f, StarShapeSeconds);
            if (_fullLightning == null) { return; }
            if (full && !_fullLightning.isPlaying) { _fullLightning.Play(); }
            else if (!full && _fullLightning.isPlaying) { _fullLightning.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        }

        // 새 웨이브가 시작될 때 게이지 시작점에서 별이 크게 터졌다 가라앉는다
        private void BurstStar()
        {
            if (_gaugeStar == null || !isActiveAndEnabled) { return; }
            _burstTween?.Kill();
            _burst = BurstScale;
            ApplyStarShape();
            _burstTween = DOTween.To(() => _burst, v => { _burst = v; ApplyStarShape(); }, 1f, BurstSeconds)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void TweenTo(ref Tween slot, System.Func<float> get, System.Action<float> set, float to, float seconds)
        {
            slot?.Kill();
            if (!isActiveAndEnabled) { set(to); return; }
            slot = DOTween.To(() => get(), v => set(v), to, seconds).SetUpdate(true).SetLink(gameObject);
        }

        // 별의 배율 = 반짝임 × (가늘어짐 / 번짐) × 터짐
        private void ApplyStarShape()
        {
            if (_gaugeStar == null) { return; }
            float x = _twinkle * _burst * Mathf.Lerp(1f, MovingThinX, _thin) * Mathf.Lerp(1f, FullFlareX, _flare);
            float y = _twinkle * _burst;
            _gaugeStar.localScale = new Vector3(x, y, 1f);
        }

        // 별을 게이지가 찬 끝에 둔다. 채움 이미지의 로컬 좌표로 끝을 구해 앵커 설정과 상관없이 맞는다
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
            _twinkleTween?.Kill();
            _thinTween?.Kill();
            _flareTween?.Kill();
            _burstTween?.Kill();
            base.OnDestroy();
        }
    }
}
