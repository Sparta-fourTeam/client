using System.Linq;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class UiPartsAssetTests
    {
        [Test]
        public void SkillCards_ReuseGlyphAndFrame_WithDifferentStateBackgrounds()
        {
            var table = TestSkillAssets.Real();
            var entries = table.Entries.Where(e => e.cardIconFrame != null).ToArray();
            Assert.AreEqual(9, entries.Length);
            foreach (var entry in entries)
            {
                var fresh = table.GetCardVisual(entry.key, SkillCardIcon.New);
                var upgrade = table.GetCardVisual(entry.key, SkillCardIcon.Upgrade);
                Assert.AreSame(fresh.Glyph, upgrade.Glyph, entry.key);
                Assert.AreSame(fresh.Frame, upgrade.Frame, entry.key);
                Assert.AreEqual("CircleBlue", fresh.Background.name, entry.key);
                Assert.AreEqual("CircleGray", upgrade.Background.name, entry.key);
                Assert.AreEqual("CircleFrame", fresh.Frame.name, entry.key);
            }
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/CardSelect.prefab");
            foreach (var slot in root.GetComponentsInChildren<CardSlotView>(true))
            {
                var so = new SerializedObject(slot);
                Assert.IsNotNull(so.FindProperty("_layeredIcon").objectReferenceValue, slot.name);
            }
        }

        [TestCase("UiSkillParts", 9)]
        [TestCase("UiMenuParts", 12)]
        [TestCase("UiPopupParts", 6)]
        public void Atlases_HaveAllNamedParts_AndTransparentFrameCenters(string file, int count)
        {
            string path = "Assets/_Project/Sprites/" + file + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.IsTrue(importer.alphaIsTransparency);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Assert.AreEqual(count, sprites.Length);
            Assert.AreEqual(count, sprites.Select(s => s.name).Distinct().Count());
            var image = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(image.LoadImage(System.IO.File.ReadAllBytes(path)));
                foreach (var sprite in sprites.Where(s => s.name.EndsWith("Frame")))
                {
                    var rect = sprite.rect;
                    var center = image.GetPixel((int)rect.center.x, (int)rect.center.y);
                    Assert.LessOrEqual(center.a, 2f / 255f, sprite.name + " center must be transparent");
                }
            }
            finally { Object.DestroyImmediate(image); }
        }

        [Test]
        public void LobbyMenus_HaveActiveGlyphs_AndSeparateClickableBackgrounds()
        {
            var scene = SceneManager.GetSceneByPath("Assets/_Project/Scenes/Lobby.unity");
            bool opened = !scene.isLoaded;
            if (opened) { scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Lobby.unity", OpenSceneMode.Additive); }
            try
            {
                var allViews = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LayeredIconView>(true)).ToArray();
                var views = allViews.Where(v => v.name == "IconLayers").ToArray();
                Assert.AreEqual(10, views.Length);
                foreach (var view in views)
                {
                    var so = new SerializedObject(view);
                    var glyph = (Image)so.FindProperty("_glyph").objectReferenceValue;
                    var background = (Image)so.FindProperty("_background").objectReferenceValue;
                    Assert.IsTrue(glyph.gameObject.activeSelf, view.transform.parent.name);
                    Assert.IsTrue(glyph.enabled);
                    Assert.IsNotNull(glyph.sprite);
                    Assert.AreNotSame(glyph, background);
                    Assert.IsTrue(background.raycastTarget);
                    Assert.IsNotNull(background.GetComponent<Button>());
                    Assert.AreEqual("Assets/_Project/Sprites/UiSkillParts.png", AssetDatabase.GetAssetPath(background.sprite));
                }
                var top = allViews.Single(v => v.name == "Menu");
                var topGlyph = (Image)new SerializedObject(top).FindProperty("_glyph").objectReferenceValue;
                Assert.IsTrue(topGlyph.gameObject.activeSelf);
                Assert.AreEqual("MenuLines", topGlyph.sprite.name);
                var nav = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LobbyNavView>(true)).Single();
                var skillIcon = (LayeredIconView)new SerializedObject(nav).FindProperty("_skillIcon").objectReferenceValue;
                var skillGlyph = (Image)new SerializedObject(skillIcon).FindProperty("_glyph").objectReferenceValue;
                Assert.AreEqual("MenuWeapon", skillGlyph.sprite.name, "기존 스킬 메뉴의 무기 문양을 유지한다");
            }
            finally { if (opened) { EditorSceneManager.CloseScene(scene, true); } }
        }

        [Test]
        public void PopupCloseButton_RetainsFullHitArea_WithIndependentCross()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/EnergyRecoverPopup.prefab");
            var popup = root.GetComponent<EnergyRecoverPopupView>();
            var button = (Button)new SerializedObject(popup).FindProperty("_closeButton").objectReferenceValue;
            var image = button.GetComponent<Image>();
            Assert.IsTrue(image.raycastTarget);
            Assert.AreEqual(Vector3.one, image.rectTransform.localScale);
            Assert.AreEqual("CloseBackground", image.sprite.name);
            var glyph = button.GetComponentsInChildren<Image>(true).Single(i => i.sprite != null && i.sprite.name == "CloseCross");
            Assert.AreNotSame(image, glyph);
            Assert.IsFalse(glyph.raycastTarget);
        }
    }
}
