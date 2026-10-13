using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.View
{
    /// <summary>강화 팝업의 능력치 한 줄. "현재" 또는 "현재 → 다음"으로 표시하거나, 이름과 값을 따로 받아 표시한다</summary>
    public sealed class StatRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;

        public void Bind(StatLine line)
        {
            Bind(line.Label, line.Next == null ? line.Current : $"{line.Current} → {line.Next}");
        }

        /// <summary>이름과 값을 그대로 보여준다 (값에는 리치 텍스트 색을 넣을 수 있다)</summary>
        public void Bind(string label, string value)
        {
            gameObject.SetActive(true);
            _label.text = label;
            _value.text = value;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
