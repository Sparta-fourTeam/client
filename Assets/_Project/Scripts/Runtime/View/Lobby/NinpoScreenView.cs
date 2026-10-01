using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>인술 화면. 열 때 IGrowthCatalog에서 인술 목록을 읽어 칸을 채우고, 열린 칸을 누르면 인술 강화 팝업을 연다</summary>
    public sealed class NinpoScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private NinpoSlotView[] _slots;        // 화면의 칸 전부 (목록보다 많으면 남는 칸은 숨김)
        [SerializeField] private TMP_Text _goldText, _gemText, _ticketText;
        [SerializeField] private NinpoUpgradePopupView _upgradePopup;

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;

        // TODO(data): 강화 후 다시 그리려면 UpgradeChanged(추가 예정) 구독
        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile)
        {
            _catalog = catalog;
            _profile = profile;
        }

        private void Awake()
        {
            _panel.SetActive(false);
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

            var ninpos = _catalog.Ninpos;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i >= ninpos.Count)
                {
                    _slots[i].Hide();
                    continue;
                }

                int index = i;
                _slots[i].Bind(ninpos[i], () => _upgradePopup.Open(index));
            }
        }
    }
}
