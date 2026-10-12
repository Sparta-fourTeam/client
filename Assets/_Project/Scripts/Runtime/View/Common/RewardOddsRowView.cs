using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>확률 정보 팝업의 한 줄: 후보 아이콘, 이름, 확률</summary>
    public sealed class RewardOddsRowView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name, _percent;

        public void Set(Sprite icon, string itemName, string percent)
        {
            // 아이콘이 없으면 프리팹의 자리표시 스프라이트를 그대로 둔다
            if (_icon != null && icon != null) { _icon.sprite = icon; }
            _name.text = itemName;
            _percent.text = percent;
        }
    }
}
