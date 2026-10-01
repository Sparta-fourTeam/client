using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.View
{
    /// <summary>강화 팝업의 능력치 한 줄. "현재" 또는 "현재 → 다음"으로 표시한다</summary>
    public sealed class StatRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _value;

        public void Bind(StatLine line)
        {
            gameObject.SetActive(true);
            _label.text = line.Label;
            _value.text = line.Next == null ? line.Current : $"{line.Current} → {line.Next}";
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
