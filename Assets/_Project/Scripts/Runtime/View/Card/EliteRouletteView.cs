using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>엘리트 룰렛. 전투가 멈춘 동안 원판을 돌리고, 윗자리에 고정된 핀이 원판의 알전구에 튕기며 당첨 칸에 멈춘다.
    /// 당첨 강화 카드를 위에 차례로 쌓는다</summary>
    public sealed class EliteRouletteView : HudView
    {
        private const float SpinSeconds = 2.2f * 2f / 3f;
        private const float PegDegrees = 45f;       // 알전구 사이 각도 (원판에 8개)
        private const float PinKickDegrees = 28f;
        private const float PinReturnSeconds = 0.16f;
        private const float WinFxEmitSeconds = 0.18f;
        private const float WinGlowAlpha = 0.65f;
        private Tween _winFxStop;
        private Tween _winGlowTween;
        [SerializeField] private GameObject _panel;
        [Tooltip("돌아가는 원판 (원판 면·알전구·무기 아이콘 칸·당첨 칸 하이라이트가 들어 있다)")]
        [SerializeField] private RectTransform _spinner;
        [Tooltip("원판 윗자리에 고정된 핀. 알전구가 지나갈 때마다 튕긴다. 위쪽 끝을 피벗으로 둔다")]
        [SerializeField] private RectTransform _pin;
        [Tooltip("당첨되는 순간 한 번 튀는 원판 전체")]
        [SerializeField] private RectTransform _wheelRoot;
        [Tooltip("당첨되는 순간 터지는 파티클 (UIParticle 프리팹의 ParticleSystem). 반복 파티클로 두면 코드가 짧게 재생했다 멈춘다")]
        [SerializeField] private ParticleSystem[] _winFx;
        [SerializeField] private UnityEngine.UI.Image[] _weaponIcons;
        [SerializeField] private CardSlotView[] _rewardCards;
        [SerializeField] private TMP_Text _heading;
        [SerializeField] private SkillAssetTable _assets;
        [SerializeField] private Sprite _cardBackground;
        [SerializeField] private UnityEngine.UI.Button _skipButton;
        [SerializeField] private PointerClickRelay _background;
        [Tooltip("당첨 칸을 칠하는 부채꼴 (Radial360, 12시 기준 시계 방향 45도 채움). 비워 두면 표시하지 않는다")]
        [SerializeField] private RectTransform _winGlow;

        private StageManager _manager;
        private Tween _animation;
        private TMP_Text _skipButtonLabel;
        private bool _awaitingConfirmation;
        private int _run, _shownCards;

        [Inject]
        public void Construct(StageManager manager, IBufferedSubscriber<StageStateChanged> stateChanged)
        {
            _manager = manager;
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _skipButtonLabel = _skipButton.GetComponentInChildren<TMP_Text>(true);
            _skipButton.onClick.AddListener(OnActionClicked);
            if (_background != null) { _background.Clicked += SkipCurrentRound; }
        }

        private void OnStateChanged(StageStateChanged message)
        {
            _animation?.Kill();
            HideWinGlow();
            _run++;
            _awaitingConfirmation = false;
            if (message.State != StageState.EliteRoulette)
            {
                _panel.SetActive(false);
                return;
            }
            _panel.SetActive(true);
            if (_pin != null) { _pin.DOKill(); _pin.localRotation = Quaternion.identity; }
            _skipButton.interactable = true;
            _skipButtonLabel.text = "스킵";
            _shownCards = 0;
            for (int i = 0; i < _rewardCards.Length; i++)
            {
                var card = _rewardCards[i];
                card.gameObject.SetActive(false);
                var rect = (RectTransform)card.transform.parent;
                rect.anchoredPosition = new Vector2((i - (_manager.Roulette.CardCount - 1) * 0.5f) * 228f, 0f);
            }
            ShowSlots();
            _spinner.localRotation = Quaternion.identity;
            SpinNext(_run);
        }

        private bool Current(int run) => run == _run && _manager.State == StageState.EliteRoulette;

        private void SpinNext(int run)
        {
            if (!Current(run)) { return; }
            var session = _manager.Roulette;
            var round = session.PrepareRound();
            if (round == null)
            {
                ShowCompletion(run);
                return;
            }
            _heading.text = $"엘리트 보상  {session.CompletedRolls + 1} / {session.CardCount}";
            _animation = PlaySpin(round.WinnerIndex, () => Reveal(run, round));
        }

        /// <summary>원판이 시계 방향으로 여러 바퀴 돌아 winnerIndex 칸의 가운데가 핀 아래에 오도록 멈추는 회전 연출.
        /// 알전구가 핀을 지날 때마다 핀이 튕긴다</summary>
        public Tween PlaySpin(int winnerIndex, TweenCallback onComplete = null)
        {
            HideWinGlow();
            float start = _spinner.localEulerAngles.z;
            float target = 22.5f + winnerIndex * 45f;
            float travel = Mathf.Repeat(start - target, 360f) + 1080f;
            int lastPeg = Mathf.FloorToInt(start / PegDegrees);
            return DOVirtual.Float(0f, travel, SpinSeconds, angle =>
                {
                    float rotation = start - angle;
                    _spinner.localRotation = Quaternion.Euler(0f, 0f, rotation);
                    int peg = Mathf.FloorToInt(rotation / PegDegrees);
                    if (peg != lastPeg)
                    {
                        lastPeg = peg;
                        KickPin();
                    }
                })
                .SetEase(Ease.OutQuart).SetUpdate(true).SetLink(gameObject)
                .OnComplete(onComplete);
        }

        // 알전구가 핀 끝을 밀어 올렸다가 놓아 주면 핀이 제자리로 튕겨 돌아온다
        private void KickPin()
        {
            if (_pin == null) { return; }
            _pin.DOKill();
            _pin.localRotation = Quaternion.Euler(0f, 0f, PinKickDegrees);
            _pin.DOLocalRotate(Vector3.zero, PinReturnSeconds).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>원판이 멈추며 당첨된 순간의 연출: 당첨 칸을 민트색으로 밝히고, 원판이 튀고, 파티클이 터진다</summary>
        public void PlayWinEffect(int winnerIndex)
        {
            ShowWinGlow(winnerIndex);
            if (_wheelRoot != null)
            {
                _wheelRoot.DOKill(true);
                _wheelRoot.DOPunchScale(Vector3.one * 0.07f, 0.4f, 6, 0.6f).SetUpdate(true).SetLink(gameObject);
            }

            if (_winGlow != null && _winGlow.TryGetComponent<UnityEngine.UI.Image>(out var glow))
            {
                var color = glow.color;
                color.a = 0.95f;
                glow.color = color;
                _winGlowTween = DOTween.To(() => glow.color.a, a => { var c = glow.color; c.a = a; glow.color = c; }, WinGlowAlpha, 0.3f)
                    .SetUpdate(true).SetLink(gameObject);
            }

            if (_winFx == null || _winFx.Length == 0) { return; }
            foreach (var fx in _winFx) { fx.Play(true); }
            _winFxStop?.Kill();
            _winFxStop = DOVirtual.DelayedCall(WinFxEmitSeconds, () =>
            {
                foreach (var fx in _winFx) { fx.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            }, true).SetLink(gameObject);
        }

        // 당첨된 칸을 민트색으로 칠한다. 12시에서 시계 방향으로 칸 번호 × 45도만큼 돌려 해당 칸에 맞춘다
        private void ShowWinGlow(int winnerIndex)
        {
            if (_winGlow == null) { return; }
            _winGlowTween?.Kill();
            if (_winGlow.TryGetComponent<UnityEngine.UI.Image>(out var glow))
            {
                var color = glow.color;
                color.a = WinGlowAlpha;
                glow.color = color;
            }
            _winGlow.localRotation = Quaternion.Euler(0f, 0f, -winnerIndex * 45f);
            _winGlow.gameObject.SetActive(true);
        }

        private void HideWinGlow()
        {
            _winGlowTween?.Kill();
            if (_winGlow != null) { _winGlow.gameObject.SetActive(false); }
        }

        private void ShowSlots()
        {
            var slots = _manager.Roulette.Slots;
            for (int i = 0; i < _weaponIcons.Length; i++)
            {
                var icon = i < slots.Count && slots[i].skill != null ? _assets.GetHudIcon(slots[i].skill.Data.id) : null;
                _weaponIcons[i].sprite = icon;
                _weaponIcons[i].enabled = icon != null;
                _weaponIcons[i].color = Color.white;
            }
        }

        private void Reveal(int run, EliteRouletteRound round)
        {
            if (!Current(run)) { return; }
            ShowReward(round);
            PlayWinEffect(round.WinnerIndex);
            if (!Current(run)) { return; }
            _animation = DOVirtual.DelayedCall(0.4f, () => SpinNext(run), ignoreTimeScale: true).SetLink(gameObject);
        }

        private void ShowReward(EliteRouletteRound round)
        {
            var card = _rewardCards[_shownCards];
            // 레벨이 실제로 오르기 전에 표시를 묶어 현재 → 다음 레벨을 올바르게 보존한다.
            card.Bind(_shownCards, round.Winner,
                round.Winner.TargetForm != SkillForm.Default
                    ? _assets.GetFormIcon(round.Winner.TargetForm, round.Winner.skill.Data.id)
                    : _assets.GetCardIcon(round.Winner.skill.Data.id, SkillCardIcon.Upgrade), _cardBackground, null,
                hint => _assets.GetFormIcon(hint.Form, hint.Skill.id));
            if (_manager.ApplyRouletteReward(round))
            {
                card.gameObject.SetActive(true);
                card.GetComponentInChildren<UnityEngine.UI.Button>(true).interactable = false;
                card.PlayEntrance(0f);
                _shownCards++;
            }
        }

        private void SkipCurrentRound()
        {
            if (_manager == null || _manager.State != StageState.EliteRoulette || !_skipButton.interactable) { return; }
            if (_awaitingConfirmation)
            {
                OnActionClicked();
                return;
            }

            int run = ++_run;
            _animation?.Kill();
            var session = _manager.Roulette;
            // dim 한 번은 현재 회차만 끝낸다. 이미 준비된 당첨 결과를 그대로 적용한다.
            var round = session.PrepareRound();
            if (round == null) { ShowCompletion(run); return; }
            _spinner.localRotation = Quaternion.Euler(0f, 0f, 22.5f + round.WinnerIndex * 45f);
            ShowReward(round);
            PlayWinEffect(round.WinnerIndex);
            if (!Current(run)) { return; }
            if (session.IsComplete) { ShowCompletion(run); }
            else
            {
                _animation = DOVirtual.DelayedCall(0.4f, () => SpinNext(run), ignoreTimeScale: true).SetLink(gameObject);
            }
        }

        private void OnActionClicked()
        {
            if (_manager == null || _manager.State != StageState.EliteRoulette || !_skipButton.interactable) { return; }
            _skipButton.interactable = false;
            if (_awaitingConfirmation)
            {
                _awaitingConfirmation = false;
                _manager.FinishEliteRoulette();
                return;
            }
            int run = ++_run;
            _animation?.Kill();
            var session = _manager.Roulette;
            // 진행 중인 당첨 결과와 배치를 보존하고, 남은 강화 카드를 중복 없이 뽑는다.
            while (Current(run))
            {
                var round = session.PrepareRound();
                if (round == null) { break; }
                _spinner.localRotation = Quaternion.Euler(0f, 0f, 22.5f + round.WinnerIndex * 45f);
                ShowReward(round);
                ShowWinGlow(round.WinnerIndex);
            }
            if (Current(run)) { ShowCompletion(run); }
        }

        private void ShowCompletion(int run)
        {
            _heading.text = "강화 완료";
            _skipButton.interactable = false;
            // 마지막 카드까지 펼쳐진 뒤에만 확인을 허용한다. 결과는 확인할 때까지 유지한다.
            _animation = DOVirtual.DelayedCall(0.2f, () =>
            {
                if (!Current(run)) { return; }
                _awaitingConfirmation = true;
                _skipButtonLabel.text = "확인";
                _skipButton.interactable = true;
            }, ignoreTimeScale: true).SetLink(gameObject);
        }

        protected override void DisposeView()
        {
            _run++;
            _animation?.Kill();
            _winFxStop?.Kill();
            _winGlowTween?.Kill();
            _skipButton.onClick.RemoveListener(OnActionClicked);
            if (_background != null) { _background.Clicked -= SkipCurrentRound; }
            base.DisposeView();
        }
    }
}
