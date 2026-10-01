using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>장비 강화 팝업. 지금은 열고 닫기만 하고, 강화·장착 해제는 미완성 안내를 띄운다</summary>
    public sealed class EquipUpgradePopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _unequipAllButton;
        [SerializeField] private ComingSoonToastView _toast;

        // TODO(data): 장비 이름·레벨·능력치·장식품 효과·비용 표시 (장비 데이터와 API 추가 예정)
        // private int _slot;

        private void Awake()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => _panel.SetActive(false));
            _upgradeButton.onClick.AddListener(_toast.Show);
            _unequipAllButton.onClick.AddListener(_toast.Show);
        }

        public void Open(int slot)
        {
            // _slot = slot;
            // Refresh();
            _panel.SetActive(true);
        }
    }
}
