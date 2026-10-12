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
    /// <summary>엘리트 룰렛. 전투가 멈춘 동안 바늘을 돌리고 당첨 강화 카드를 위에 차례로 쌓는다</summary>
    public sealed class EliteRouletteView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private Image[] _weaponIcons;
        [SerializeField] private CardSlotView[] _rewardCards;
        [SerializeField] private TMP_Text _heading;
        [SerializeField] private SkillAssetTable _assets;
        [SerializeField] private Sprite _cardBackground;

        private StageManager _manager;
        private Tween _animation;
        private int _run, _shownCards;

        [Inject]
        public void Construct(StageManager manager, IBufferedSubscriber<StageStateChanged> stateChanged)
        {
            _manager = manager;
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        protected override void InitializeView() => _panel.SetActive(false);

        private void OnStateChanged(StageStateChanged message)
        {
            _animation?.Kill();
            _run++;
            if (message.State != StageState.EliteRoulette)
            {
                _panel.SetActive(false);
                return;
            }
            _panel.SetActive(true);
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
                _heading.text = "강화 완료";
                _animation = DOVirtual.DelayedCall(0.9f, () =>
                {
                    if (Current(run)) { _manager.FinishEliteRoulette(); }
                }, ignoreTimeScale: true).SetLink(gameObject);
                return;
            }
            _heading.text = $"엘리트 보상  {session.CompletedRolls + 1} / {session.CardCount}";
            for (int i = 0; i < _weaponIcons.Length; i++)
            {
                var icon = _assets.GetHudIcon(round.Slots[i].skill.Data.assetKey);
                _weaponIcons[i].sprite = icon;
                _weaponIcons[i].enabled = icon != null;
            }
            float start = _spinner.localEulerAngles.z;
            float target = -(22.5f + round.WinnerIndex * 45f);
            float travel = Mathf.Repeat(start - target, 360f) + 1080f;
            _animation = DOVirtual.Float(0f, travel, 2.2f,
                angle => _spinner.localRotation = Quaternion.Euler(0f, 0f, start - angle))
                .SetEase(Ease.OutQuart).SetUpdate(true).SetLink(gameObject)
                .OnComplete(() => Reveal(run, round));
        }

        private void Reveal(int run, EliteRouletteRound round)
        {
            if (!Current(run)) { return; }
            var card = _rewardCards[_shownCards];
            // 레벨이 실제로 오르기 전에 표시를 묶어 현재 → 다음 레벨을 올바르게 보존한다.
            card.Bind(_shownCards, round.Winner,
                _assets.GetCardIcon(round.Winner.skill.Data.assetKey, SkillCardIcon.Upgrade), _cardBackground, null,
                hint => _assets.GetFormIcon(hint.Form, hint.Skill.assetKey));
            if (_manager.ApplyRouletteReward(round))
            {
                card.gameObject.SetActive(true);
                card.GetComponentInChildren<Button>(true).interactable = false;
                card.PlayEntrance(0f);
                _shownCards++;
            }
            _animation = DOVirtual.DelayedCall(0.4f, () => SpinNext(run), ignoreTimeScale: true).SetLink(gameObject);
        }

        protected override void DisposeView()
        {
            _run++;
            _animation?.Kill();
            base.DisposeView();
        }
    }
}
