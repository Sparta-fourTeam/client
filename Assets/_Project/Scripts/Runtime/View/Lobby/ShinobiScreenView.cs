using Game.Core;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    /// <summary>닌자(캐릭터) 화면. 열 때 IGrowthCatalog에서 캐릭터·장비 정보를 읽어 채우고, 열린 장비 칸을 누르면 장비 강화 팝업을 연다.
    /// 아래 닌자 목록(카드)은 아직 데이터가 없어 프리팹 그대로 둔다</summary>
    public sealed class ShinobiScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _nameText, _rankText, _powerText;
        [SerializeField] private EquipSlotView[] _equipSlots;   // IGrowthCatalog.Equips와 같은 순서
        [SerializeField] private EquipUpgradePopupView _equipPopup;

        private IGrowthCatalog _catalog;

        // TODO(data): 장비 강화·캐릭터 변경 후 다시 그리려면 관련 메시지(추가 예정) 구독
        [Inject]
        public void Construct(IGrowthCatalog catalog)
        {
            _catalog = catalog;
        }

        private void Awake()
        {
            _panel.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            _panel.SetActive(visible);
            if (visible)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            ShinobiInfo shinobi = _catalog.Shinobi;
            _nameText.text = shinobi.Name;
            _rankText.text = shinobi.Rank;
            _powerText.text = CurrencyFormat.Format(shinobi.Power);

            var equips = _catalog.Equips;
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                if (i >= equips.Count)
                {
                    _equipSlots[i].Hide();
                    continue;
                }

                int slot = i;
                _equipSlots[i].Bind(equips[i], () => _equipPopup.Open(slot));
            }
        }
    }
}
