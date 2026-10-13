using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>장비 강화 팝업의 장식품 효과 한 줄. 비활성 효과는 보석을 회색으로 칠한다</summary>
    public sealed class EquipEffectRowView : MonoBehaviour
    {
        private static readonly Color Active = new Color(0.45f, 0.85f, 0.35f, 1f);
        private static readonly Color Inactive = new Color(0.85f, 0.85f, 0.85f, 1f);

        [SerializeField] private Image _gem;
        [SerializeField] private TMP_Text _text;

        public void Bind(EquipEffect effect)
        {
            gameObject.SetActive(true);
            _gem.color = effect.IsActive ? Active : Inactive;
            _text.text = effect.Text;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
