using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>하단 메뉴. 인벤토리·캐릭터·스킬·상점 탭은 해당 화면을 열고, 홈은 모두 닫는다. 화면은 한 번에 하나만 열린다</summary>
    public sealed class LobbyNavView : MonoBehaviour, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _inventoryScreen = registry.Get<InventoryScreenView>();
            _characterScreen = registry.Get<CharacterScreenView>();
            _skillScreen = registry.Get<SkillScreenView>();
            _shopScreen = registry.Get<ShopScreenView>();
        }

        [SerializeField] private Button _inventoryTab, _characterTab, _homeTab, _skillTab, _shopTab;
        [SerializeField] private GameObject _inventorySelected, _characterSelected, _homeSelected, _skillSelected, _shopSelected;
        [Tooltip("스킬 탭 위의 자물쇠. 잠겨 있는 동안 켜지고 탭이 눌리지 않는다")]
        [SerializeField] private RectTransform _skillLock;
        private bool _skillLocked;
        private Sequence _unlock;
        private InventoryScreenView _inventoryScreen;
        private CharacterScreenView _characterScreen;
        private SkillScreenView _skillScreen;
        private ShopScreenView _shopScreen;

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
            _skillTab.onClick.AddListener(() =>
            {
                if (!_skillLocked)
                {
                    Select(Tab.Skill);
                }
            });
            _shopTab.onClick.AddListener(() => Select(Tab.Shop));
        }

        // 화면들의 Awake(패널 끄기)가 끝난 뒤에 처음 상태를 정한다
        private void Start()
        {
            Select(Tab.Home);
        }

        /// <summary>튜토리얼이 강조하는 스킬 탭 버튼</summary>
        public RectTransform SkillTab => (RectTransform)_skillTab.transform;

        /// <summary>스킬 탭을 잠그거나 연다 (연출 없이)</summary>
        public void SetSkillLocked(bool locked)
        {
            _unlock?.Kill();
            _skillLocked = locked;
            if (_skillLock != null)
            {
                _skillLock.gameObject.SetActive(locked);
                _skillLock.localScale = Vector3.one;
                _skillLock.localRotation = Quaternion.identity;
            }
        }

        /// <summary>잠긴 스킬 탭이 열리는 연출: 자물쇠가 흔들리다 튀어 오르며 사라지고 탭이 한 번 튄다. 끝나면 완료된다</summary>
        public UniTask PlaySkillUnlockAsync()
        {
            _unlock?.Kill();
            _skillLocked = false;
            if (_skillLock == null || !_skillLock.gameObject.activeSelf)
            {
                return UniTask.CompletedTask;
            }

            var done = new UniTaskCompletionSource();
            var tab = _skillTab.transform;
            _unlock = DOTween.Sequence().SetUpdate(true).SetLink(gameObject)
                .Append(_skillLock.DOPunchRotation(new Vector3(0f, 0f, 25f), 0.5f, 14))
                .Append(_skillLock.DOScale(1.5f, 0.12f))
                .Append(_skillLock.DOScale(0f, 0.18f).SetEase(Ease.InBack))
                .Append(tab.DOPunchScale(Vector3.one * 0.25f, 0.35f, 6))
                .OnComplete(() =>
                {
                    _skillLock.gameObject.SetActive(false);
                    _skillLock.localScale = Vector3.one;
                    done.TrySetResult();
                })
                .OnKill(() => done.TrySetResult());
            return done.Task;
        }

        /// <summary>스킬 화면을 연다 (인벤토리의 "강화하러 가기" 등)</summary>
        public void OpenSkill() => Select(Tab.Skill);

        /// <summary>캐릭터 화면을 연다 (인벤토리의 "강화하러 가기" 등)</summary>
        public void OpenCharacter() => Select(Tab.Character);

        // 화면은 한 번에 하나만 연다
        private void Select(Tab tab)
        {
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
