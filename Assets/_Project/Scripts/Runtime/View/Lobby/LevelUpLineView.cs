using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>레벨업 팝업의 한 줄 (아이콘 + 문구). 아이콘이 없으면 아이콘 칸을 숨긴다</summary>
    public sealed class LevelUpLineView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _text;

        public void Bind(Sprite icon, string text)
        {
            gameObject.SetActive(true);
            _icon.sprite = icon;
            _icon.gameObject.SetActive(icon != null);
            _text.text = text;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
