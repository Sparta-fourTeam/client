using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>웨이브가 끝나면 카드 후보를 보여주고 고른 카드를 StageManager에 알린다. CardSelect 상태에서만 보인다</summary>
    public sealed class CardSelectView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private CardSlotView[] _slots;
        [SerializeField] private SkillIconTable _iconTable;
        [SerializeField] private Sprite _newWeaponBg, _upgradeBg;

        private StageManager _stageManager;

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
                _panel.SetActive(false);
            }
        }

        private void Show()
        {
            var choices = _stageManager.Choices;
            for (int i = 0; i < _slots.Length; i++)
            {
                bool hasChoice = i < choices.Count;
                _slots[i].gameObject.SetActive(hasChoice);
                if (hasChoice)
                {
                    var choice = choices[i];
                    string iconKey = choice.IsNewWeapon ? choice.NewWeaponData.iconKey :
                        choice.Weapon.Data.iconKey;
                    Sprite icon = _iconTable != null ? _iconTable.Find(iconKey) : null;
                    Sprite bg = choice.IsNewWeapon ? _newWeaponBg : _upgradeBg;
                    _slots[i].Bind(i, choice, icon, bg, OnPick);
                }
            }

            _panel.SetActive(true);
        }

        private void OnPick(int index)
        {
            _stageManager.PickCard(index);
        }
    }
}
