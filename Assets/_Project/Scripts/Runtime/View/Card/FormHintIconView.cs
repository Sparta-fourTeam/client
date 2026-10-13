using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>카드 아래에 붙는 형태 변환 아이콘 하나. 이 카드를 고르면 변환을 더는 얻을 수 없을 때는 X 표시를 덮는다</summary>
    public sealed class FormHintIconView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _blockedMark;

        /// <summary>X 표시가 켜져 있는가</summary>
        public bool IsBlocked => _blockedMark != null && _blockedMark.activeSelf;

        public Sprite Icon => _icon != null ? _icon.sprite : null;

        public void Show(Sprite icon, bool blocked)
        {
            gameObject.SetActive(true);
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }

            if (_blockedMark != null)
            {
                _blockedMark.SetActive(blocked);
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
