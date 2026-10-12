using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>확률 정보 팝업의 한 줄</summary>
    public sealed class RewardOddsRowView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name, _percent;

        public void Set(RewardOddsRow row)
        {
            _icon.sprite = row.Icon;
            _icon.enabled = row.Icon != null;
            _name.text = row.Name;
            _percent.text = row.Percent;
        }
    }
}
