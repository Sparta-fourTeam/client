using System.Linq;
using System.Reflection;
using DG.Tweening;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class LobbyAssetBindingTests
    {
        private GameObject _root;
        private GameDataStore _data;
        private PlayerProfile _profile;
        private DummyGrowthCatalog _catalog;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            _data = new GameDataStore();
            _profile = new PlayerProfile(_data);
            _catalog = new DummyGrowthCatalog(_profile, _data);
        }

        [TearDown]
        public void TearDown()
        {
            DOTween.KillAll();
            if (_root != null) { Object.DestroyImmediate(_root); }
            _catalog.Dispose();
        }

        private T Load<T>(string name) where T : Component
        {
            _root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/_Project/Prefabs/UI/{name}.prefab"));
            return _root.GetComponent<T>();
        }

        [Test]
        public void SkillScreen_ReopenBindsEveryVisibleSlotToItsSkillAsset()
        {
            var screen = Load<SkillScreenView>("SkillScreen");
            screen.Construct(_catalog, _profile);
            var assets = AssetDatabase.LoadAssetAtPath<SkillAssetTable>("Assets/_Project/Data/SkillAssetTable.asset");
            var definitions = _data.LoadSkills().ToDictionary(s => s.id.ToString());
            var slots = (SkillListSlotView[])typeof(SkillScreenView).GetField("_slots", Private).GetValue(screen);

            for (int pass = 0; pass < 2; pass++)
            {
                screen.SetVisible(true);
                for (int i = 0; i < _catalog.Skills.Count; i++)
                {
                    var skill = _catalog.Skills[i];
                    var icon = slots[i].transform.Find("Icon").GetComponent<Image>();
                    Assert.AreEqual(definitions[skill.Id].assetKey, skill.AssetKey);
                    Assert.IsNotNull(assets.GetHudIcon(skill.AssetKey), skill.Name);
                    Assert.AreSame(assets.GetHudIcon(skill.AssetKey), icon.sprite, skill.Name);
                    Assert.IsTrue(icon.enabled, skill.Name);
                    Assert.AreEqual(!skill.IsUnlocked, slots[i].transform.Find("Lock").gameObject.activeSelf);
                }

                foreach (var unused in slots.Skip(_catalog.Skills.Count))
                {
                    Assert.IsFalse(unused.gameObject.activeSelf);
                }

                screen.SetVisible(false);
            }
        }

        [Test]
        public void SkillPopup_ChangingSelectionReplacesSkillAndMaterialSprites()
        {
            var popup = Load<SkillUpgradePopupView>("SkillUpgradePopup");
            popup.Construct(_catalog, _profile, null, _data);
            typeof(SkillUpgradePopupView).GetMethod("Awake", Private).Invoke(popup, null);
            var assets = AssetDatabase.LoadAssetAtPath<SkillAssetTable>("Assets/_Project/Data/SkillAssetTable.asset");
            var items = AssetDatabase.LoadAssetAtPath<ItemIconTable>("Assets/_Project/Data/ItemIconTable.asset");
            var icon = _root.transform.Find("Panel/Window/Info/Icon/Art").GetComponent<Image>();
            var book = _root.transform.Find("Panel/Window/Cost/BookIcon").GetComponent<Image>();

            for (int i = 0; i < _catalog.Skills.Count; i++)
            {
                popup.Open(i);
                var skill = _catalog.Skills[i];
                Assert.AreSame(assets.GetHudIcon(skill.AssetKey), icon.sprite, skill.Name);
                var expected = skill.MaterialItemId != null
                    ? items.Get(_data.Items.GetOrThrow(skill.MaterialItemId).IconKey) : null;
                Assert.AreSame(expected, book.sprite, skill.Name);
                Assert.AreEqual(expected != null, book.enabled, skill.Name);
            }
        }

        [Test]
        public void SkillSlot_MissingIconClearsPreviousSelection()
        {
            var slot = Load<SkillListSlotView>("SkillListSlot");
            var assets = AssetDatabase.LoadAssetAtPath<SkillAssetTable>("Assets/_Project/Data/SkillAssetTable.asset");
            var skill = _catalog.Skills[0];
            var icon = _root.transform.Find("Icon").GetComponent<Image>();
            slot.Bind(skill, null, assets.GetHudIcon(skill.AssetKey));
            Assert.IsNotNull(icon.sprite);
            slot.Bind(skill, null);
            Assert.IsNull(icon.sprite);
            Assert.IsFalse(icon.enabled);
        }
    }
}
