using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>장비 강화 팝업. 위에는 현재 레벨의 능력치, 아래에는 다음 레벨의 능력치(오르는 값은 초록색)를 보여주고,
    /// 강화 버튼은 한 번, 일괄 강화 버튼은 재화가 허락하는 만큼 연속으로 IUpgradeApi를 부른다.
    /// 코인이나 재료가 모자라면 비용이 빨갛게 보이고 버튼이 눌리지 않으며, 최대 레벨이면 다음 블록·비용·버튼 대신 "최대 레벨"을 보여준다.
    /// 장비에는 장착·해제가 없고 강화만 있다</summary>
    public sealed class EquipUpgradePopupView : HudView
    {
        private const string IncreaseColor = "#7BE05A";

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _upgradeAllButton;
        [SerializeField] private TMP_Text _nameText;
        [FormerlySerializedAs("_levelText")]
        [SerializeField] private TMP_Text _currentLevelText;
        [SerializeField] private TMP_Text _nextLevelText;
        [FormerlySerializedAs("_statRows")]
        [SerializeField] private StatRowView[] _currentRows;
        [SerializeField] private StatRowView[] _nextRows;
        [SerializeField] private GameObject _next;          // ▼와 다음 레벨 블록
        [SerializeField] private TMP_Text _coinText;
        [FormerlySerializedAs("_bookText")]
        [SerializeField] private TMP_Text _materialText;
        [SerializeField] private TMP_Text _materialNameText;
        [SerializeField] private ItemIconTable _icons;
        [SerializeField] private Image _equipmentIcon, _materialIcon;
        [SerializeField] private Image _nextEquipmentIcon;
        [SerializeField] private GameObject _cost, _maxLevel;

        private IGrowthCatalog _catalog;
        private PlayerProfile _profile;
        private GameDataStore _data;
        private IUpgradeApi _upgradeApi;
        private int _slot;
        private bool _busy;

        [Inject]
        public void Construct(IGrowthCatalog catalog, PlayerProfile profile, GameDataStore data, IUpgradeApi upgradeApi)
        {
            _catalog = catalog;
            _profile = profile;
            _data = data;
            _upgradeApi = upgradeApi;
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => PopupPanel.Set(_panel, _transition, false));
            _upgradeButton.onClick.AddListener(() => UpgradeAsync(all: false).Forget(Debug.LogException));
            _upgradeAllButton.onClick.AddListener(() => UpgradeAsync(all: true).Forget(Debug.LogException));
        }

        public void Open(int slot)
        {
            _slot = slot;
            PopupPanel.Set(_panel, _transition, true);
            Refresh();
        }

        private void Refresh()
        {
            EquipInfo info = _catalog.Equips[_slot];
            bool isMax = info.IsMaxLevel;

            _nameText.text = info.Name;
            _currentLevelText.text = $"Lv.{info.Level}/{info.MaxLevel}";
            BindRows(_currentRows, info.Stats, next: false);
            if (_icons != null)
            {
                SetIcon(_equipmentIcon, _icons.Get(info.UpgradeId));
                SetIcon(_nextEquipmentIcon, _icons.Get(info.UpgradeId));
                var materialSprite = info.MaterialItemId != null
                    ? _icons.Get(_data.Items.GetOrThrow(info.MaterialItemId).IconKey)
                    : null;
                SetIcon(_materialIcon, materialSprite);
            }

            // 최대 레벨에는 다음 레벨과 비용이 없으므로 다음 블록·비용·버튼을 숨기고 "최대 레벨"을 띄운다
            _next.SetActive(!isMax);
            _cost.SetActive(!isMax);
            _maxLevel.SetActive(isMax);
            _upgradeButton.gameObject.SetActive(!isMax);
            _upgradeAllButton.gameObject.SetActive(!isMax);
            if (isMax)
            {
                return;
            }

            _nextLevelText.text = $"Lv.{info.Level + 1}/{info.MaxLevel}";
            BindRows(_nextRows, info.Stats, next: true);

            _coinText.text = CostFormat.HaveNeed(_profile.Gold, info.CoinCost);
            _materialText.text = CostFormat.HaveNeed(Material(info), info.MaterialCost);
            _materialNameText.text = info.MaterialItemId != null ? _data.Items.GetOrThrow(info.MaterialItemId).Name : string.Empty;

            bool canUpgrade = !_busy && CanUpgrade(info);
            _upgradeButton.interactable = canUpgrade;
            _upgradeAllButton.interactable = canUpgrade;
        }

        // 현재 블록은 현재 값, 다음 블록은 다음 값을 쓴다. 다음 블록에서 값이 바뀌는 줄은 초록색으로 강조한다
        private static void BindRows(StatRowView[] rows, IReadOnlyList<StatLine> stats, bool next)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (i >= stats.Count)
                {
                    rows[i].Hide();
                    continue;
                }

                StatLine line = stats[i];
                string value = !next ? line.Current
                    : line.Next == null || line.Next == line.Current ? line.Current
                    : $"<color={IncreaseColor}>{line.Next}</color>";
                rows[i].Bind(line.Label, value);
            }
        }

        private int Material(EquipInfo info) => info.MaterialItemId != null ? _profile.ItemQuantity(info.MaterialItemId) : 0;

        private bool CanUpgrade(EquipInfo info) =>
            info.IsUnlocked && !info.IsMaxLevel && _profile.Gold >= info.CoinCost && Material(info) >= info.MaterialCost;

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) { return; }
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        // all이면 재화가 모자라거나 최대 레벨이 될 때까지 한 단계씩 연속으로 강화한다
        private async UniTask UpgradeAsync(bool all)
        {
            if (_busy)
            {
                return;
            }

            // 연타로 두 번 요청하지 않게 응답이 올 때까지 막는다. 매 요청마다 새 키를 쓴다
            _busy = true;
            _upgradeButton.interactable = false;
            _upgradeAllButton.interactable = false;
            string upgradeId = _catalog.Equips[_slot].UpgradeId;
            try
            {
                do
                {
                    var snapshot = await _upgradeApi.Purchase(upgradeId, Guid.NewGuid().ToString("N"));
                    _profile.Apply(snapshot);
                }
                while (all && CanUpgrade(_catalog.Equips[_slot]));
            }
            catch (ApiException ex)
            {
                // 버튼이 막혀 있어 보통은 오지 않는다(다른 곳에서 재화가 바뀐 경우 등). 최신 상태로 다시 그린다
                Debug.LogWarning($"장비 강화 거절: {ex.Code}");
            }
            finally
            {
                _busy = false;
                if (_panel.activeSelf)
                {
                    Refresh();
                }
            }
        }
    }
}
