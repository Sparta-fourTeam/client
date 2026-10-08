using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>문양·바탕·테두리를 조합한다. 합성 아이콘과 아이콘 없는 일반 카드도 같은 슬롯을 쓴다</summary>
    public sealed class LayeredIconView : MonoBehaviour
    {
        [SerializeField] private Image _glyph;
        [SerializeField] private Image _background;
        [SerializeField] private Image _frame;
        [SerializeField] private Image _lock;
        [SerializeField] private Sprite _normalBackground, _selectedBackground, _lockSprite;
        [SerializeField] private bool _locked;
        [SerializeField, Range(0.1f, 1f)] private float _layeredGlyphScale = 0.72f;

        public void Bind(Sprite glyph, Sprite background = null, Sprite frame = null)
        {
            bool visible = glyph != null;
            SetImage(_glyph, glyph);
            SetImage(_background, visible ? background : null);
            SetImage(_frame, visible ? frame : null);
            SetImage(_lock, visible && _locked ? _lockSprite : null);
            _glyph.rectTransform.localScale = Vector3.one *
                (visible && (background != null || frame != null) ? _layeredGlyphScale : 1f);
        }

        public void SetSelected(bool selected)
        {
            if (_normalBackground == null && _selectedBackground == null) { return; }
            SetImage(_background, selected && _selectedBackground != null ? _selectedBackground : _normalBackground);
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            SetImage(_lock, locked && _glyph != null && _glyph.sprite != null ? _lockSprite : null);
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            if (image == null) { return; }
            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
