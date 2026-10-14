using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>퀘스트 목록의 한 줄. 이름·진행·점수와 받기 버튼을 보여준다. 버튼은 목표를 채웠고 아직 안 받았을 때만 눌린다</summary>
    public sealed class QuestRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title, _progress, _points, _buttonLabel;
        [SerializeField] private Button _button;

        public Button Button => _button;

        public void Show(string title, int count, int target, int points, bool claimed)
        {
            bool complete = count >= target;
            _title.text = title;
            _progress.text = $"({count:N0}/{target:N0})";
            _points.text = points.ToString();
            _buttonLabel.text = claimed ? "완료" : complete ? "받기" : "미완료";
            _button.interactable = complete && !claimed;
        }
    }
}
