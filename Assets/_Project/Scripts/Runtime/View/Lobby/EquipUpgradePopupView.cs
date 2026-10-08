using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>장비 강화 팝업. IGrowthCatalog의 장비 정보로 레벨·효과·비용을 채우고, 강화 버튼으로 IUpgradeApi를 부른다.
    /// 코인이나 재료가 모자라면 비용이 빨갛게 보이고 버튼이 눌리지 않으며, 최대 레벨이면 비용 대신 "최대 레벨"을 보여준다.
    /// 장비에는 장착·해제가 없고 강화만 있다</summary>
    public sealed class EquipUpgradePopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TMP_Text _nameText, _levelText, _coinText;
        [FormerlySerializedAs("_bookText")]
        [SerializeField] private TMP_Text _materialText;
        [SerializeField] private TMP_Text _materialNameText;
        [SerializeField] private ItemIconTable _icons;
        [SerializeField] private Image _equipmentIcon, _materialIcon;
        [SerializeField] private GameObject _cost, _maxLevel;
        [SerializeField] private StatRowView[] _statRows;
        [SerializeField] private EquipEffectRowView[] _effectRows;

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
            _upgradeButton.onClick.AddListener(() => UpgradeAsync().Forget(Debug.LogException));
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

            _nameText.text = info.Name;
            _levelText.text = $"Lv.{info.Level}";
            if (_icons != null)
            {
                SetIcon(_equipmentIcon, _icons.Get(info.UpgradeId));
                var item = _data.Items.GetOrThrow(info.MaterialItemId);
                SetIcon(_materialIcon, _icons.Get(item.IconKey));
            }
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

            // 최대 레벨에는 다음 비용이 없으므로 비용 칸과 강화 버튼을 숨기고 "최대 레벨"을 띄운다
            bool isMax = info.IsMaxLevel;
            _cost.SetActive(!isMax);
            _maxLevel.SetActive(isMax);
            _upgradeButton.gameObject.SetActive(!isMax);
            if (isMax)
            {
                return;
            }

            int coin = _profile.Gold;
            int material = _profile.ItemQuantity(info.MaterialItemId);
            _coinText.text = CostFormat.HaveNeed(coin, info.CoinCost);
            _materialText.text = CostFormat.HaveNeed(material, info.MaterialCost);
            _materialNameText.text = _data.Items.GetOrThrow(info.MaterialItemId).Name;
            _upgradeButton.interactable = !_busy && info.IsUnlocked && coin >= info.CoinCost && material >= info.MaterialCost;
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) { return; }
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private async UniTask UpgradeAsync()
        {
            if (_busy)
            {
                return;
            }

            // 연타로 두 번 요청하지 않게 응답이 올 때까지 막는다. 매 요청마다 새 키를 쓴다
            _busy = true;
            _upgradeButton.interactable = false;
            string upgradeId = _catalog.Equips[_slot].UpgradeId;
            try
            {
                var snapshot = await _upgradeApi.Purchase(upgradeId, Guid.NewGuid().ToString("N"));
                _profile.Apply(snapshot);
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
