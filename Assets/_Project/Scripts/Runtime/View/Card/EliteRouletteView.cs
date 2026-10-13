using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>엘리트 룰렛. 전투가 멈춘 동안 바늘을 돌리고 당첨 강화 카드를 위에 차례로 쌓는다</summary>
    public sealed class EliteRouletteView : HudView
    {
        private const float SpinSeconds = 2.2f * 2f / 3f;
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private UnityEngine.UI.Image[] _weaponIcons;
        [SerializeField] private CardSlotView[] _rewardCards;
        [SerializeField] private TMP_Text _heading;
        [SerializeField] private SkillAssetTable _assets;
        [SerializeField] private Sprite _cardBackground;
        [SerializeField] private UnityEngine.UI.Button _skipButton;

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
        }

        private void OnStateChanged(StageStateChanged message)
        {
            _animation?.Kill();
            _run++;
            _awaitingConfirmation = false;
            if (message.State != StageState.EliteRoulette)
            {
                _panel.SetActive(false);
                return;
            }
            _panel.SetActive(true);
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
            ShowSlots(round);
            float start = _spinner.localEulerAngles.z;
            float target = -(22.5f + round.WinnerIndex * 45f);
            float travel = Mathf.Repeat(start - target, 360f) + 1080f;
            _animation = DOVirtual.Float(0f, travel, SpinSeconds,
                angle => _spinner.localRotation = Quaternion.Euler(0f, 0f, start - angle))
                .SetEase(Ease.OutQuart).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => Reveal(run, round));
        }

        private void ShowSlots(EliteRouletteRound round)
        {
            for (int i = 0; i < _weaponIcons.Length; i++)
            {
                var icon = _assets.GetHudIcon(round.Slots[i].skill.Data.assetKey);
                _weaponIcons[i].sprite = icon;
                _weaponIcons[i].enabled = icon != null;
            }
        }

        private void Reveal(int run, EliteRouletteRound round)
        {
            if (!Current(run)) { return; }
            ShowReward(round);
            if (!Current(run)) { return; }
            _animation = DOVirtual.DelayedCall(0.4f, () => SpinNext(run), ignoreTimeScale: true).SetLink(gameObject);
        }

        private void ShowReward(EliteRouletteRound round)
        {
            var card = _rewardCards[_shownCards];
            // 레벨이 실제로 오르기 전에 표시를 묶어 현재 → 다음 레벨을 올바르게 보존한다.
            card.Bind(_shownCards, round.Winner,
                _assets.GetCardIcon(round.Winner.skill.Data.assetKey, SkillCardIcon.Upgrade), _cardBackground, null,
                hint => _assets.GetFormIcon(hint.Form, hint.Skill.assetKey));
            if (_manager.ApplyRouletteReward(round))
            {
                card.gameObject.SetActive(true);
                card.GetComponentInChildren<UnityEngine.UI.Button>(true).interactable = false;
                card.PlayEntrance(0f);
                _shownCards++;
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
            // 진행 중인 당첨 결과를 보존하고, 각 강화 적용 후 다음 회차 후보를 다시 뽑는다.
            while (Current(run))
            {
                var round = session.PrepareRound();
                if (round == null) { break; }
                ShowSlots(round);
                _spinner.localRotation = Quaternion.Euler(0f, 0f, -(22.5f + round.WinnerIndex * 45f));
                ShowReward(round);
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
            _skipButton.onClick.RemoveListener(OnActionClicked);
            base.DisposeView();
        }
    }
}
