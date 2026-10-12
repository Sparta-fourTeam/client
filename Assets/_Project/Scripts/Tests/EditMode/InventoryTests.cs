using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class InventoryTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly GameDataStore Data = new();

        private static List<InventoryItem> Build(Dictionary<string, int> owned, InventoryCategory filter) =>
            InventoryItems.Build(Data.Items.Values, id => owned.TryGetValue(id, out var q) ? q : 0, filter);

        [Test(Description = "보유한 아이템만 나오고, 스킬 마법북 → 장비 주문서 → 기타 순서다")]
        public void Build_OnlyOwned_InCategoryOrder()
        {
            var owned = new Dictionary<string, int>
            {
                [ItemIds.GemChest] = 2,
                [ItemIds.RingBook] = 1,
                [ItemIds.FireballBook] = 5,
                [ItemIds.ArrowBook] = 3,
                [ItemIds.LogBook] = 0,
            };

            var items = Build(owned, InventoryCategory.All);

            CollectionAssert.AreEqual(new[] { ItemIds.ArrowBook, ItemIds.FireballBook, ItemIds.RingBook, ItemIds.GemChest },
                items.Select(i => i.ItemId).ToArray());
            Assert.AreEqual(5, items[1].Quantity);
            Assert.AreEqual(InventoryCategory.EquipmentMaterial, items[2].Category);
        }

        [Test(Description = "분류 탭은 그 분류의 아이템만 보여준다")]
        public void Build_FiltersByCategory()
        {
            var owned = new Dictionary<string, int> { [ItemIds.GemChest] = 1, [ItemIds.HatBook] = 1, [ItemIds.LightningBook] = 1 };

            Assert.AreEqual(new[] { ItemIds.LightningBook }, Build(owned, InventoryCategory.SkillMaterial).Select(i => i.ItemId).ToArray());
            Assert.AreEqual(new[] { ItemIds.HatBook }, Build(owned, InventoryCategory.EquipmentMaterial).Select(i => i.ItemId).ToArray());
            Assert.AreEqual(new[] { ItemIds.GemChest }, Build(owned, InventoryCategory.Etc).Select(i => i.ItemId).ToArray());
        }

        [Test(Description = "Items 표의 모든 아이템은 아이콘이 연결돼 있다")]
        public void EveryItem_HasIcon()
        {
            var icons = AssetDatabase.LoadAssetAtPath<ItemIconTable>("Assets/_Project/Data/ItemIconTable.asset");

            foreach (var item in Data.Items.Values)
            {
                Assert.IsNotNull(icons.Get(item.IconKey), item.Id);
            }
        }

        // ---------- 화면 (실제 프리팹) ----------

        private GameObject _root;
        private InventoryScreenView _view;
        private PlayerProfile _profile;
        private GameObject _detailRoot;
        private DetailPopupView _detail;

        private void OpenScreen(params (string id, int quantity)[] owned)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/InventoryScreen.prefab");
            _root = Object.Instantiate(prefab);
            _view = _root.GetComponent<InventoryScreenView>();
            _view.Initialize();
            _profile = new PlayerProfile(Data);
            _profile.Apply(Snapshot(owned));
            _view.Construct(_profile, Data);
            _detailRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Common/DetailPopup.prefab"));
            _detail = _detailRoot.GetComponent<DetailPopupView>();
            _detail.Initialize();
            typeof(InventoryScreenView).GetField("_detail", Flags).SetValue(_view, _detail);
            _view.SetVisible(true);
        }

        private static PlayerSnapshot Snapshot((string id, int quantity)[] owned) => new()
        {
            items = owned.Select(o => new ItemAmount { itemId = o.id, quantity = o.quantity }).ToList(),
        };

        private T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);

        // 테스트 어셈블리는 TextMeshPro를 참조하지 않으므로 글자 컴포넌트는 Component로 받고 text를 이름으로 읽는다
        private Component Label(object target, string name) => Field<Component>(target, name);

        private static string TextOf(Component label) => (string)label.GetType().GetProperty("text").GetValue(label);

        [TearDown]
        public void TearDown()
        {
            if (_root != null) { Object.DestroyImmediate(_root); }
            if (_detailRoot != null) { Object.DestroyImmediate(_detailRoot); }
        }

        [Test(Description = "열면 전체 탭에 보유 아이템 칸이 나오고, 분류 탭을 누르면 그 분류만 남는다")]
        public void Screen_ShowsOwnedItems_AndTabsFilter()
        {
            OpenScreen((ItemIds.ArrowBook, 2), (ItemIds.HatBook, 1), (ItemIds.GemChest, 4));

            Assert.AreEqual(3, _view.FilledSlots.Count);
            Assert.AreEqual(InventoryScreenView.MinSlots, _view.VisibleSlots.Count, "나머지는 빈칸으로 4×10을 채운다");

            Field<Button[]>(_view, "_tabs")[2].onClick.Invoke();

            Assert.AreEqual(InventoryCategory.EquipmentMaterial, _view.Category);
            Assert.AreEqual(new[] { ItemIds.HatBook }, _view.FilledSlots.Select(s => s.Item.ItemId).ToArray());
            Assert.AreEqual(InventoryScreenView.MinSlots, _view.VisibleSlots.Count);
            Assert.IsTrue(Field<GameObject[]>(_view, "_tabSelected")[2].activeSelf);
            Assert.IsFalse(Field<GameObject[]>(_view, "_tabSelected")[0].activeSelf);
        }

        [Test(Description = "보유한 아이템이 없어도 4×10 빈칸이 보이고, 빈칸은 눌리지 않는다")]
        public void Screen_Empty_ShowsEmptySlots()
        {
            OpenScreen();

            Assert.AreEqual(0, _view.FilledSlots.Count);
            Assert.AreEqual(InventoryScreenView.MinSlots, _view.VisibleSlots.Count);
            Assert.IsFalse(Field<Button>(_view.VisibleSlots[0], "_button").interactable);
        }

        [Test(Description = "칸 수는 최소 40이고, 넘으면 4칸(한 줄)씩 늘어난다")]
        public void SlotCount_GrowsByRow()
        {
            Assert.AreEqual(40, InventoryScreenView.SlotCountFor(0));
            Assert.AreEqual(40, InventoryScreenView.SlotCountFor(40));
            Assert.AreEqual(44, InventoryScreenView.SlotCountFor(41));
        }

        [Test(Description = "화면이 열려 있는 동안 보유 개수가 바뀌면 다시 그린다")]
        public void Screen_RefreshesOnProfileChange()
        {
            OpenScreen((ItemIds.ArrowBook, 2));

            _profile.Apply(Snapshot(new[] { (ItemIds.ArrowBook, 2), (ItemIds.FireballBook, 1) }));

            Assert.AreEqual(2, _view.FilledSlots.Count);
        }

        [Test(Description = "상세: 이동할 곳이 있으면 버튼이 보이고 누르면 닫히며 이동한다. 없으면 버튼이 숨고, 배경을 누르면 닫힌다")]
        public void Detail_GoButtonAndClose()
        {
            OpenScreen((ItemIds.ArrowBook, 2));
            int went = 0;

            _detail.Show(null, "화살 마법북", "보유 2개", "설명", () => went++);
            var go = Field<Button>(_detail, "_goButton");
            Assert.IsTrue(_detail.IsShown);
            Assert.IsTrue(go.gameObject.activeSelf);
            Assert.AreEqual("화살 마법북", TextOf(Label(_detail, "_titleText")));

            go.onClick.Invoke();
            Assert.AreEqual(1, went);
            Assert.IsFalse(_detail.IsShown);

            _detail.Show(null, "화살 마법북", "보유 2개", "설명");
            Assert.IsFalse(go.gameObject.activeSelf);
            Field<PointerClickRelay>(_detail, "_background").OnPointerClick(new PointerEventData(null));
            Assert.IsFalse(_detail.IsShown);
        }

        [Test(Description = "칸을 누르면 그 아이템의 이름·보유 개수·설명으로 공용 상세 창이 열린다")]
        public void Slot_Click_OpensDetail()
        {
            OpenScreen((ItemIds.GemChest, 1));

            Field<Button>(_view.FilledSlots[0], "_button").onClick.Invoke();

            Assert.IsTrue(_detail.IsShown);
            Assert.AreEqual("보유 1개", TextOf(Label(_detail, "_subtitleText")));
            Assert.AreEqual(ItemInfo.DescriptionOf(ItemIds.GemChest), TextOf(Label(_detail, "_descText")));
            Assert.IsFalse(Field<Button>(_detail, "_goButton").gameObject.activeSelf, "보석상자는 이동할 곳이 없다");
        }
    }
}
