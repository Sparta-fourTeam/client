using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>인벤토리 화면. 하단 메뉴의 인벤토리 탭이 연다. 분류 탭(전체·스킬 재료·장비 재료·기타)별로 4×10 격자를 보여준다.
    /// 보유한 아이템이 앞에서부터 칸을 채우고 나머지는 빈칸이다(40개를 넘으면 한 줄씩 늘어난다). 아이템 칸을 누르면 상세를 연다. 마법북은 스킬 화면으로, 주문서는 캐릭터 화면으로 바로 갈 수 있다.
    /// 화면이 열려 있는 동안 보유 개수가 바뀌면 다시 그린다</summary>
    public sealed class InventoryScreenView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _nav = registry.Get<LobbyNavView>();
            _detail = registry.Get<DetailPopupView>();
        }

        [SerializeField] private GameObject _panel;

        [Header("분류 탭 (전체, 스킬 재료, 장비 재료, 기타 순서)")]
        [SerializeField] private Button[] _tabs;
        [SerializeField] private GameObject[] _tabSelected;

        [Header("격자")]
        [SerializeField] private Transform _grid;
        [Tooltip("복제해서 칸을 만드는 원본. 꺼진 채로 격자 안에 둔다")]
        [SerializeField] private InventorySlotView _slotTemplate;

        [SerializeField] private ItemIconTable _icons;

        public const int Columns = 4;
        public const int MinSlots = Columns * 10;

        private static readonly InventoryCategory[] TabCategories =
        {
            InventoryCategory.All, InventoryCategory.SkillMaterial, InventoryCategory.EquipmentMaterial, InventoryCategory.Etc,
        };

        private readonly List<InventorySlotView> _slots = new();
        private PlayerProfile _profile;
        private GameDataStore _data;
        private LobbyNavView _nav;
        private DetailPopupView _detail;
        private InventoryCategory _category = InventoryCategory.All;

        /// <summary>지금 보이는 칸 전부 (빈칸 포함)</summary>
        public IReadOnlyList<InventorySlotView> VisibleSlots => _slots.FindAll(s => s.gameObject.activeSelf);

        /// <summary>아이템이 들어 있는 칸</summary>
        public IReadOnlyList<InventorySlotView> FilledSlots => _slots.FindAll(s => s.gameObject.activeSelf && s.IsFilled);

        /// <summary>아이템 수에 맞춘 칸 수. 최소 4×10이고, 넘으면 한 줄(4칸) 단위로 늘린다</summary>
        public static int SlotCountFor(int itemCount) =>
            System.Math.Max(MinSlots, (itemCount + Columns - 1) / Columns * Columns);

        public InventoryCategory Category => _category;

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data)
        {
            _profile = profile;
            _data = data;
            _profile.Changed += OnProfileChanged;
        }

        protected override void DisposeView()
        {
            if (_profile != null)
            {
                _profile.Changed -= OnProfileChanged;
            }

            base.DisposeView();
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _slotTemplate.gameObject.SetActive(false);
            for (int i = 0; i < _tabs.Length && i < TabCategories.Length; i++)
            {
                var category = TabCategories[i];
                _tabs[i].onClick.AddListener(() => SelectCategory(category));
            }
        }

        public void SetVisible(bool visible)
        {
            _panel.SetActive(visible);
            if (_detail != null)
            {
                _detail.Hide();
            }
            if (visible)
            {
                // 다시 열면 전체 탭부터 보여준다
                SelectCategory(InventoryCategory.All);
            }
        }

        public void SelectCategory(InventoryCategory category)
        {
            _category = category;
            for (int i = 0; i < _tabSelected.Length && i < TabCategories.Length; i++)
            {
                _tabSelected[i].SetActive(TabCategories[i] == category);
            }

            Refresh();
        }

        private void OnProfileChanged(PlayerProfile _)
        {
            if (_panel.activeSelf)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_profile == null || _data == null)
            {
                return;
            }

            var items = InventoryItems.Build(_data.Items.Values, _profile.ItemQuantity, _category);
            int slotCount = SlotCountFor(items.Count);
            while (_slots.Count < slotCount)
            {
                _slots.Add(Instantiate(_slotTemplate, _grid, false));
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                if (i >= slotCount)
                {
                    _slots[i].gameObject.SetActive(false);
                }
                else if (i >= items.Count)
                {
                    _slots[i].ShowEmpty();
                }
                else
                {
                    var item = items[i];
                    _slots[i].Bind(item, IconOf(item), () => OpenDetail(item));
                }
            }
        }

        private Sprite IconOf(InventoryItem item) => _icons != null ? _icons.Get(item.ItemId) : null;

        private void OpenDetail(InventoryItem item)
        {
            System.Action go = item.Category switch
            {
                InventoryCategory.SkillMaterial when _nav != null => _nav.OpenSkill,
                InventoryCategory.EquipmentMaterial when _nav != null => _nav.OpenCharacter,
                _ => null,
            };
            _detail.Show(IconOf(item), item.Name, $"보유 {CurrencyFormat.Format(item.Quantity)}개", ItemInfo.DescriptionOf(item.ItemId), go);
        }
    }
}
