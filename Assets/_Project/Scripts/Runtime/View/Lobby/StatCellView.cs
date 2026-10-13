using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.View
{
    /// <summary>강화 팝업의 능력치 칸. "공격력 2,180.36 +127.32"</summary>
    public sealed class StatCellView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;
        [SerializeField] private TMP_Text _increase;

        public void Bind(StatLine line, bool showIncrease)
        {
            gameObject.SetActive(true);
            _label.text = line.Label;
            _value.text = line.Current;

            bool hasIncrease = showIncrease && !string.IsNullOrEmpty(line.Increase);
            _increase.gameObject.SetActive(hasIncrease);
            if (hasIncrease)
            {
                _increase.text = line.Increase;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
