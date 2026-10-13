using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>스킬 강화 팝업. IGrowthCatalog의 스킬 정보로 능력치·레벨별 보상·비용을 채우고, 레벨업 버튼으로 IUpgradeApi를 부른다.
    /// 코인이나 마법북이 모자라면 비용이 빨갛게 보이고 버튼이 눌리지 않으며, 최대 레벨이면 비용 대신 "최대 레벨"을 보여준다.
    /// 강화 데이터가 없는 스킬은 레벨업 버튼이 눌리지 않는다</summary>
    public sealed class SkillUpgradePopupView : HudView, IUiBindable
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private GameObject _maxLevel;
        [SerializeField] private TMP_Text _nameText, _levelText, _descText, _coinText, _bookText;
        [SerializeField] private StatCellView[] _statCells;             // Stats/StatCell1~4
        [SerializeField] private LevelRewardRowView _rewardRowPrefab;   // 보상 행이 모자랄 때 복제할 원본 (LevelRewardRow1)
        [SerializeField] private Transform _rewardContent;              // LevelRewards/Viewport/Content
        [SerializeField] private GameObject _cost;                      // 인법서·엽전 비용 묶음
        [SerializeField] private SkillAssetTable _assets;
        [SerializeField] private ItemIconTable _itemIcons;
        [SerializeField] private Image _skillIcon, _bookIcon;

        private readonly List<LevelRewardRowView> _rewardRows = new();

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private int _index;
        private UpgradeConfirmPopupView _confirmation;
        public void BindUi(IUiRegistry registry) => _confirmation = registry.Get<UpgradeConfirmPopupView>();

        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile, GameDataStore data)
        {
            _catalog = catalog;
            _profile = profile;
            _data = data;
            _profile.Changed += OnProfileChanged;
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => PopupPanel.Set(_panel, _transition, false));
            _upgradeButton.onClick.AddListener(() => RequestUpgrade());

            // 프리팹에 미리 만들어 둔 행을 재사용하고, 모자라면 BindRewards에서 복제한다
            _rewardRows.AddRange(_rewardContent.GetComponentsInChildren<LevelRewardRowView>(true));
        }

        public void Open(int index)
        {
            _index = index;
            PopupPanel.Set(_panel, _transition, true);
            Refresh();
        }

        private void Refresh()
        {
            SkillInfo info = _catalog.Skills[_index];
            bool isMax = info.IsMaxLevel;

            _nameText.text = info.Name;
            _levelText.text = isMax ? "MAX" : $"Lv. {info.Level}";
            _descText.text = info.Description;
            SetIcon(_skillIcon, _assets != null ? _assets.GetHudIcon(info.AssetKey) : null);
            var book = _itemIcons != null && info.MaterialItemId != null && _data.Items.Contains(info.MaterialItemId)
                ? _itemIcons.Get(_data.Items.GetOrThrow(info.MaterialItemId).IconKey)
                : null;
            SetIcon(_bookIcon, book);

            // 최대 레벨이면 다음 레벨이 없으므로 증가량을 숨긴다
            for (int i = 0; i < _statCells.Length; i++)
            {
                if (i < info.Stats.Count)
                {
                    _statCells[i].Bind(info.Stats[i], showIncrease: !isMax);
                }
                else
                {
                    _statCells[i].Hide();
                }
            }

            BindRewards(info);

            // 최대 레벨에는 다음 비용이 없으므로 비용 칸과 강화 버튼을 숨기고 "최대 레벨"을 띄운다.
            // 강화 데이터가 없는 스킬은 비용이 없으므로 비용 칸만 숨기고, 강화 버튼은 눌리지 않게 둔다
            bool upgradable = info.UpgradeId != null;
            _cost.SetActive(!isMax && upgradable);
            _maxLevel.SetActive(isMax);
            _upgradeButton.gameObject.SetActive(!isMax);
            if (isMax || !upgradable)
            {
                _upgradeButton.interactable = false;
                return;
            }

            int coin = _profile.Gold;
            int material = info.MaterialItemId != null ? _profile.ItemQuantity(info.MaterialItemId) : 0;
            _bookText.text = CostFormat.HaveNeed(material, info.MaterialCost);
            _coinText.text = CostFormat.HaveNeed(coin, info.CoinCost);
            _upgradeButton.interactable = LobbyAvailability.CanUpgrade(_profile, info);
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) { return; }
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void RequestUpgrade()
        {
            var info = _catalog.Skills[_index];
            if (LobbyAvailability.CanUpgrade(_profile, info))
            {
                _confirmation.Show(info.Name, info.UpgradeId, false);
            }
        }

        private void OnProfileChanged(PlayerProfile _)
        {
            if (_panel.activeSelf)
            {
                Refresh();
            }
        }
        protected override void DisposeView()
        {
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }
        }

        // 보상 수만큼 행을 채우고 남는 행은 끈다. 달성 여부는 현재 레벨과 비교한다
        private void BindRewards(SkillInfo info)
        {
            int count = info.LevelRewards?.Count ?? 0;
            while (_rewardRows.Count < count)
            {
                _rewardRows.Add(Instantiate(_rewardRowPrefab, _rewardContent));
            }

            for (int i = 0; i < _rewardRows.Count; i++)
            {
                if (i < count)
                {
                    LevelReward reward = info.LevelRewards[i];
                    _rewardRows[i].Bind(reward, reached: reward.Level <= info.Level);
                }
                else
                {
                    _rewardRows[i].Hide();
                }
            }
        }
    }
}
