using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>닌자(캐릭터) 화면. 지금은 열고 닫기와 장비 칸 → 장비 강화 팝업만 연결한다</summary>
    public sealed class ShinobiScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button[] _equipSlots;          // 열린 장비 칸
        [SerializeField] private EquipUpgradePopupView _equipPopup;

        // TODO(data): 캐릭터·장비 데이터와 API가 생기면 연결 (CharacterChanged는 가칭)
        // private PlayerProfile _profile;
        //
        // [Inject]
        // public void Construct(PlayerProfile profile, ISubscriber<CharacterChanged> characterChanged)
        // {
        //     _profile = profile;
        //     Track(characterChanged.Subscribe(_ => Refresh()));
        // }

        private void Awake()
        {
            _panel.SetActive(false);
            for (int i = 0; i < _equipSlots.Length; i++)
            {
                int slot = i;
                _equipSlots[i].onClick.AddListener(() => _equipPopup.Open(slot));
            }
        }

        public void SetVisible(bool visible)
        {
            _panel.SetActive(visible);
            // if (visible) Refresh();
        }

        // TODO(data): 이름·등급·전투력·장비 레벨 표시
        // private void Refresh() { }
    }
}
