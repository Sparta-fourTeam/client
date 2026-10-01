using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>인술 강화 팝업. 지금은 열고 닫기만 하고, 강화 버튼은 미완성 안내를 띄운다</summary>
    public sealed class NinpoUpgradePopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private ComingSoonToastView _toast;

        // TODO(data): 이름·레벨·능력치·비용 표시와 강화 요청 (UpgradeService는 가칭, IUpgradeApi.Purchase를 감싸는 로직)
        // [SerializeField] private TMP_Text _nameText, _levelText, _coinText, _bookText;
        // private UpgradeService _upgradeService;
        // private int _index;

        private void Awake()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => _panel.SetActive(false));
            _upgradeButton.onClick.AddListener(_toast.Show);   // TODO(data): _upgradeService.Purchase(...)로 교체
        }

        public void Open(int index)
        {
            // _index = index;
            // Refresh();
            _panel.SetActive(true);
        }
    }
}
