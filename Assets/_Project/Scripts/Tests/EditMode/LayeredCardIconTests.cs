using System.Reflection;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests
{
    public sealed class LayeredCardIconTests
    {
        private GameObject _root;
        private LayeredIconView _view;
        private Image _glyph, _background, _frame;
        private Texture2D _texture;
        private Sprite _glyphSprite, _gray, _blue, _outline;
        private SkillAssetTable _table;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(16, 16);
            _glyphSprite = MakeSprite();
            _gray = MakeSprite();
            _blue = MakeSprite();
            _outline = MakeSprite();
            _table = SkillAssetTable.Create(new[]
            {
                new SkillAssetEntry
                {
                    key = "arrow",
                    newCardIcon = _glyphSprite, upgradeCardIcon = _glyphSprite,
                    newCardIconBackground = _blue, upgradeCardIconBackground = _gray,
                    cardIconFrame = _outline,
                },
                new SkillAssetEntry { key = "plain", upgradeCardIcon = _glyphSprite },
                new SkillAssetEntry { key = "missing", newCardIconBackground = _blue, cardIconFrame = _outline },
            });
            _root = new GameObject("Icon", typeof(RectTransform));
            _view = _root.AddComponent<LayeredIconView>();
            _glyph = AddImage("Glyph", "_glyph");
            _background = AddImage("Background", "_background");
            _frame = AddImage("Frame", "_frame");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_table);
            Object.DestroyImmediate(_glyphSprite);
            Object.DestroyImmediate(_gray);
            Object.DestroyImmediate(_blue);
            Object.DestroyImmediate(_outline);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void Rebind_NewUpgradePlainMissing_DoesNotRetainPreviousLayers()
        {
            Bind("arrow", SkillCardIcon.New);
            Assert.AreSame(_glyphSprite, _glyph.sprite);
            Assert.AreSame(_blue, _background.sprite);
            Assert.AreSame(_outline, _frame.sprite);
            Assert.Less(_glyph.rectTransform.localScale.x, 1f);

            Bind("arrow", SkillCardIcon.Upgrade);
            Assert.AreSame(_glyphSprite, _glyph.sprite, "상태가 바뀌어도 같은 문양을 쓴다");
            Assert.AreSame(_gray, _background.sprite);
            Assert.AreSame(_outline, _frame.sprite);

            Bind("plain", SkillCardIcon.Upgrade);
            Assert.IsTrue(_glyph.enabled);
            Assert.IsFalse(_background.enabled);
            Assert.IsFalse(_frame.enabled);
            Assert.IsNull(_background.sprite);
            Assert.IsNull(_frame.sprite);
            Assert.AreEqual(Vector3.one, _glyph.rectTransform.localScale);

            Bind("missing", SkillCardIcon.New);
            Assert.IsFalse(_glyph.enabled);
            Assert.IsFalse(_background.enabled);
            Assert.IsFalse(_frame.enabled);

            Bind("arrow", SkillCardIcon.New);
            Assert.IsTrue(_glyph.enabled);
            Assert.IsTrue(_background.enabled);
            Assert.IsTrue(_frame.enabled);
        }

        [Test]
        public void MissingGlyph_UsesFallbackWithoutUnrelatedBackgroundAndFrame()
        {
            var fallbackTable = SkillAssetTable.Create(_table.Entries, cardFallback: _glyphSprite);
            try
            {
                var visual = fallbackTable.GetCardVisual("missing", SkillCardIcon.New);
                Assert.AreSame(_glyphSprite, visual.Glyph);
                Assert.IsNull(visual.Background);
                Assert.IsNull(visual.Frame);
                var unknown = fallbackTable.GetCardVisual("unknown", SkillCardIcon.New);
                Assert.AreSame(visual.Glyph, unknown.Glyph);
                Assert.IsNull(unknown.Background);
                Assert.IsNull(unknown.Frame);
            }
            finally { Object.DestroyImmediate(fallbackTable); }
        }

        [Test]
        public void MenuState_ChangesBackgroundAndLock_WhileReusingGlyphAndFrame()
        {
            var lockImage = AddImage("Lock", "_lock");
            SetField("_normalBackground", _gray);
            SetField("_selectedBackground", _blue);
            SetField("_lockSprite", _outline);
            Bind("arrow", SkillCardIcon.Upgrade);

            _view.SetSelected(true);
            _view.SetLocked(true);
            Assert.AreSame(_blue, _background.sprite);
            Assert.IsTrue(lockImage.enabled);
            Assert.AreSame(_glyphSprite, _glyph.sprite);
            Assert.AreSame(_outline, _frame.sprite);

            _view.SetSelected(false);
            _view.SetLocked(false);
            Assert.AreSame(_gray, _background.sprite);
            Assert.IsFalse(lockImage.enabled);
            Assert.IsNull(lockImage.sprite);
            Assert.AreSame(_glyphSprite, _glyph.sprite);
            Assert.AreSame(_outline, _frame.sprite);
        }

        private void SetField(string field, object value) =>
            typeof(LayeredIconView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_view, value);

        private Sprite MakeSprite() => Sprite.Create(_texture, new Rect(0, 0, 16, 16), Vector2.one * 0.5f);

        private Image AddImage(string name, string field)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(_root.transform, false);
            typeof(LayeredIconView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_view, image);
            return image;
        }

        private void Bind(string key, SkillCardIcon kind)
        {
            var visual = _table.GetCardVisual(key, kind);
            _view.Bind(visual.Glyph, visual.Background, visual.Frame);
        }
    }
}
