using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>스킬 화면. 열 때 IGrowthCatalog에서 스킬 목록을 읽어 칸을 채우고, 열린 칸을 누르면 스킬 강화 팝업을 연다</summary>
    public sealed class SkillScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private SkillListSlotView[] _slots;        // 화면의 칸 전부 (목록보다 많으면 남는 칸은 숨김)
        [SerializeField] private TMP_Text _goldText, _gemText, _ticketText;
        [SerializeField] private SkillUpgradePopupView _upgradePopup;

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;

        // TODO(data): 실제 구매가 붙으면 Upgraded 이벤트 대신 UpgradeChanged(추가 예정) 구독
        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile)
        {
            _catalog = catalog;
            _profile = profile;
        }

        private void Awake()
        {
            _panel.SetActive(false);
            // 팝업에서 레벨업하면 칸의 레벨 표시를 다시 그린다
            _upgradePopup.Upgraded += Refresh;
        }

        protected override void OnDestroy()
        {
            _upgradePopup.Upgraded -= Refresh;
            base.OnDestroy();
        }

        public void SetVisible(bool visible)
        {
            // 칸들의 Awake가 먼저 돌도록 켠 뒤에 채운다
            _panel.SetActive(visible);
            if (visible)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            _goldText.text = CurrencyFormat.Format(_profile.Gold);
            _gemText.text = CurrencyFormat.Format(_catalog.Gems);
            _ticketText.text = CurrencyFormat.Format(_catalog.Tickets);

            var skills = _catalog.Skills;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i >= skills.Count)
                {
                    _slots[i].Hide();
                    continue;
                }

                int index = i;
                _slots[i].Bind(skills[i], () => _upgradePopup.Open(index));
            }
        }
    }
}
