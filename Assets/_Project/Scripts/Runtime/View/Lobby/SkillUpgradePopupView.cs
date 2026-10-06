using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>스킬 강화 팝업. IGrowthCatalog의 스킬 정보로 능력치·비용을 채운다. 강화 버튼은 아직 미완성 안내</summary>
    public sealed class SkillUpgradePopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private GameObject _maxLevel;
        [SerializeField] private TMP_Text _nameText, _levelText, _descText, _coinText, _bookText;
        [SerializeField] private StatRowView[] _statRows;
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
            // TODO(data): IUpgradeApi.Purchase(스킬 id)를 감싸는 로직으로 교체
            _upgradeButton.onClick.AddListener(_toast.Show);
        }

        public void Open(int index)
        {
            _panel.SetActive(true);
            SkillInfo info = _catalog.Skills[index];

            _nameText.text = info.Name;
            _levelText.text = $"Lv.{info.Level}";
            _descText.text = info.Description;
            for (int i = 0; i < _statRows.Length; i++)
            {
                if (i < info.Stats.Count && !info.IsMaxLevel)
                {
                    _statRows[i].Bind(info.Stats[i]);
                }
                else
                {
                    _statRows[i].Hide();
                }
            }

            _coinText.text = CostFormat.HaveNeed(_profile.Gold, info.CoinCost);
            _bookText.text = CostFormat.HaveNeed(_catalog.SkillBooks, info.BookCost);
            _upgradeButton.gameObject.SetActive(!info.IsMaxLevel);
            _maxLevel.SetActive(info.IsMaxLevel);
        }
    }
}
