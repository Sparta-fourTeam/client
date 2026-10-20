using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>공지 목록의 한 줄. 제목·받은 시각과, 아직 안 읽은 공지의 빨간 점을 보여준다. 줄을 누르면 내용을 연다</summary>
    public sealed class NoticeRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _title, _date;
        [SerializeField] private GameObject _unreadDot;
        [SerializeField] private Button _button;

        public Button Button => _button;

        public void Show(Notice notice)
        {
            _title.text = string.IsNullOrWhiteSpace(notice.Title) ? notice.Content : notice.Title;
            _date.text = notice.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm");
            _unreadDot.SetActive(!notice.IsRead);
        }
    }
}
