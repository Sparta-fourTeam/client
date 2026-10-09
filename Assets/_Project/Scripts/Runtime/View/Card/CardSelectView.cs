using DG.Tweening;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Game.View
{
    /// <summary>웨이브가 끝나면 카드 후보를 보여주고 고른 카드를 StageManager에 알린다. CardSelect 상태에서만 보인다</summary>
    public sealed class CardSelectView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private CardSlotView[] _slots;
        [FormerlySerializedAs("_iconTable")]
        [SerializeField] private SkillAssetTable _assets;
        [SerializeField] private Sprite _newWeaponBg, _upgradeBg;

        private StageManager _stageManager;
        private bool _picking;   // 선택 연출 중에는 다른 카드를 누를 수 없다
        private Tween _pickTween;

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<StageStateChanged> stateChanged)
        {
            _stageManager = stageManager;
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        private void Awake()
        {
            _panel.SetActive(false);
        }

        // Paused로 넘어가면 숨기고, CardSelect로 돌아오면 같은 후보를 다시 보여준다 (StageManager가 후보를 유지한다)
        private void OnStateChanged(StageStateChanged message)
        {
            if (message.State == StageState.CardSelect)
            {
                Show();
            }
            else
            {
                PopupPanel.Set(_panel, _transition, false);
            }
        }

        private const float EntranceStagger = 0.08f;
        private const float PickDelay = 0.2f;

        private void Show()
        {
            _pickTween?.Kill();
            _picking = false;
            var choices = _stageManager.Choices;
            int order = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool hasChoice = i < choices.Count;
                _slots[i].gameObject.SetActive(hasChoice);
                if (hasChoice)
                {
                    var choice = choices[i];
                    Sprite icon = _assets != null ? FindIcon(choice) : null;
                    Sprite background = choice.IsNewWeapon ? _newWeaponBg : _upgradeBg;
                    _slots[i].Bind(i, choice, icon, background, OnPick, FindFormIcon);
                    _slots[i].PlayEntrance(order++ * EntranceStagger);
                }
            }

            PopupPanel.Set(_panel, _transition, true);
        }

        // 스킬 카드는 스킬의 assetKey로, 일반 카드는 IconKey로 찾는다 (일반 카드는 강화 아이콘 칸을 쓴다)
        private Sprite FindIcon(UpgradeChoice choice)
        {
            if (choice.IsGeneral)
            {
                return _assets.GetCardIcon(choice.GeneralCard.IconKey, SkillCardIcon.Upgrade);
            }

            return choice.IsNewWeapon
                ? _assets.GetCardIcon(choice.newSkillData.assetKey, SkillCardIcon.New)
                : _assets.GetCardIcon(choice.skill.Data.assetKey, SkillCardIcon.Upgrade);
        }

        private Sprite FindFormIcon(FormHint hint) =>
            _assets != null ? _assets.GetFormIcon(hint.Form, hint.Skill.assetKey) : null;

        // 고른 카드는 부풀고 나머지는 사라진 뒤에 실제로 고른다. 그 사이 일시정지되면 PickCard가 무시하고,
        // 돌아와 다시 Show되면 처음 상태로 새로 그려진다
        private void OnPick(int index)
        {
            if (_picking) { return; }
            _picking = true;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].gameObject.activeSelf) { continue; }
                if (i == index) { _slots[i].PlayPicked(); }
                else { _slots[i].PlayDismissed(); }
            }

            _pickTween = DOVirtual.DelayedCall(PickDelay, () =>
            {
                _picking = false;
                _stageManager.PickCard(index);
            }, ignoreTimeScale: true).SetLink(gameObject);
        }

        protected override void OnDestroy()
        {
            _pickTween?.Kill();
            base.OnDestroy();
        }
    }
}
