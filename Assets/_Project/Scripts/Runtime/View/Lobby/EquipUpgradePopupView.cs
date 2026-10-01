using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>장비 강화 팝업. IGrowthCatalog의 장비 정보로 능력치·장식품 효과·비용을 채운다. 강화·장착 해제는 아직 미완성 안내</summary>
    public sealed class EquipUpgradePopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _unequipAllButton;
        [SerializeField] private TMP_Text _nameText, _levelText, _coinText, _bookText;
        [SerializeField] private StatRowView[] _statRows;
        [SerializeField] private EquipEffectRowView[] _effectRows;
        [SerializeField] private ComingSoonToastView _toast;

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;

        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile)
        {
            _catalog = catalog;
            _profile = profile;
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => _panel.SetActive(false));
            // TODO(data): 장비 강화·장착 해제 API가 생기면 교체
            _upgradeButton.onClick.AddListener(_toast.Show);
            _unequipAllButton.onClick.AddListener(_toast.Show);
        }

        public void Open(int slot)
        {
            _panel.SetActive(true);
            EquipInfo info = _catalog.Equips[slot];

            _nameText.text = info.Name;
            _levelText.text = $"Lv.{info.Level}";
            for (int i = 0; i < _statRows.Length; i++)
            {
                if (i < info.Stats.Count)
                {
                    _statRows[i].Bind(info.Stats[i]);
                }
                else
                {
                    _statRows[i].Hide();
                }
            }

            for (int i = 0; i < _effectRows.Length; i++)
            {
                if (i < info.Effects.Count)
                {
                    _effectRows[i].Bind(info.Effects[i]);
                }
                else
                {
                    _effectRows[i].Hide();
                }
            }

            _coinText.text = CostFormat.HaveNeed(_profile.Gold, info.CoinCost);
            _bookText.text = CostFormat.HaveNeed(_catalog.EquipBooks, info.BookCost);
        }
    }
}
