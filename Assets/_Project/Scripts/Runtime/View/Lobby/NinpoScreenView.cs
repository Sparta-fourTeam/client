using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>인술 화면. 지금은 열고 닫기와 인술 칸 → 인술 강화 팝업만 연결한다</summary>
    public sealed class NinpoScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button[] _slots;               // 열린 인술 칸
        [SerializeField] private NinpoUpgradePopupView _upgradePopup;

        // TODO(data): 인술 레벨 표시. 레벨은 PlayerProfile(업그레이드 목록), 목록·비용은 GameDataStore(Upgrades.json)
        // UpgradeChanged는 docs/messages.md에만 있고 아직 코드에 없다 (발행: UpgradeService, 추가 예정)
        // private PlayerProfile _profile;
        // private GameDataStore _data;
        //
        // [Inject]
        // public void Construct(PlayerProfile profile, GameDataStore data, ISubscriber<UpgradeChanged> upgradeChanged)
        // {
        //     _profile = profile;
        //     _data = data;
        //     Track(upgradeChanged.Subscribe(_ => Refresh()));
        // }

        private void Awake()
        {
            _panel.SetActive(false);
            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                _slots[i].onClick.AddListener(() => _upgradePopup.Open(index));
            }
        }

        public void SetVisible(bool visible)
        {
            _panel.SetActive(visible);
            // if (visible) Refresh();
        }

        // TODO(data): 칸마다 Lv.N, 해금 여부 표시
        // private void Refresh() { }
    }
}
