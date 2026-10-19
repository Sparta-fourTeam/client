using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>하단 메뉴. 인벤토리·캐릭터·스킬·상점 탭은 해당 화면을 열고, 홈은 모두 닫는다. 화면은 한 번에 하나만 열린다</summary>
    public sealed class LobbyNavView : MonoBehaviour, IUiBindable, IButtonLockState
    {
        public void BindUi(IUiRegistry registry)
        {
            _inventoryScreen = registry.Get<InventoryScreenView>();
            _characterScreen = registry.Get<CharacterScreenView>();
            _skillScreen = registry.Get<SkillScreenView>();
            _shopScreen = registry.Get<ShopScreenView>();
            _homeHeader = registry.Get<LobbyHomeHeaderView>();
            _chatButton = registry.Get<ChatOpenButton>();
        }

        [SerializeField] private Button _inventoryTab, _characterTab, _homeTab, _skillTab, _shopTab;
        [SerializeField] private GameObject _inventorySelected, _characterSelected, _homeSelected, _skillSelected, _shopSelected;
        private InventoryScreenView _inventoryScreen;
        private CharacterScreenView _characterScreen;
        private SkillScreenView _skillScreen;
        private ShopScreenView _shopScreen;
        private LobbyHomeHeaderView _homeHeader;
        private ChatOpenButton _chatButton;

        private enum Tab
        {
            Home,
            Inventory,
            Character,
            Skill,
            Shop,
        }

        private void Awake()
        {
            _inventoryTab.onClick.AddListener(() => Select(Tab.Inventory));
            _characterTab.onClick.AddListener(() => Select(Tab.Character));
            _homeTab.onClick.AddListener(() => Select(Tab.Home));
            _skillTab.onClick.AddListener(() => Select(Tab.Skill));
            _shopTab.onClick.AddListener(() => Select(Tab.Shop));
        }

        // 화면들의 Awake(패널 끄기)가 끝난 뒤에 처음 상태를 정한다
        private void Start()
        {
            Select(Tab.Home);
        }

        /// <summary>튜토리얼이 강조하는 스킬 탭 버튼</summary>
        public RectTransform SkillTab => (RectTransform)_skillTab.transform;
        public RectTransform CharacterTab => (RectTransform)_characterTab.transform;

        // 공통 버튼 효과음의 잠금 계약을 유지하고, 새 공용 탭 잠금 상태를 읽는다
        public bool IsLockedButton(Button button) => button == _skillTab
            && _skillTab.TryGetComponent<FeatureLockView>(out var featureLock)
            && (featureLock.IsLocked || featureLock.IsUnlockPending);

        /// <summary>스킬 화면을 연다 (인벤토리의 "강화하러 가기" 등)</summary>
        public void OpenSkill() => Select(Tab.Skill);

        /// <summary>캐릭터 화면을 연다 (인벤토리의 "강화하러 가기" 등)</summary>
        public void OpenCharacter() => Select(Tab.Character);

        /// <summary>상점 화면의 해당 탭을 연다 (재화 칸의 + 버튼 등)</summary>
        public void OpenShop(ShopTab tab)
        {
            Select(Tab.Shop);
            _shopScreen.SelectTab(tab);
        }

        // 화면은 한 번에 하나만 연다
        private void Select(Tab tab)
        {
            _homeHeader.SetVisible(tab == Tab.Home);
            if (_chatButton != null) { _chatButton.gameObject.SetActive(tab == Tab.Home); }
            _inventoryScreen.SetVisible(tab == Tab.Inventory);
            _characterScreen.SetVisible(tab == Tab.Character);
            _skillScreen.SetVisible(tab == Tab.Skill);
            _shopScreen.SetVisible(tab == Tab.Shop);
            _inventorySelected.SetActive(tab == Tab.Inventory);
            _characterSelected.SetActive(tab == Tab.Character);
            _homeSelected.SetActive(tab == Tab.Home);
            _skillSelected.SetActive(tab == Tab.Skill);
            _shopSelected.SetActive(tab == Tab.Shop);
        }
    }
}
