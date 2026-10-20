using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>메뉴의 공지사항 팝업. 받은 공지를 최신순으로 보여주고, 줄을 누르면 내용을 확인창으로 열면서 읽음 처리한다.
    /// 아래 버튼은 모두 읽음 처리한다. 틀은 LobbyListPopupView(탭 없음)</summary>
    public sealed class NoticeListPopupView : LobbyListPopupView, IUiBindable
    {
        [SerializeField] private NoticeRowView _rowPrefab;
        [SerializeField] private Transform _rowParent;
        [SerializeField] private Button _markAllReadButton;

        private readonly List<NoticeRowView> _rows = new();
        private NoticeService _notices;
        private ConfirmPopupView _confirm;

        public void BindUi(IUiRegistry registry) => _confirm = registry.Get<ConfirmPopupView>();

        [Inject]
        public void Construct(NoticeService notices, IBufferedSubscriber<NoticeChanged> changed)
        {
            _notices = notices;
            Track(changed.Subscribe(_ =>
            {
                if (IsOpen) { Refresh(); }
            }));
        }

        protected override void InitializeView()
        {
            base.InitializeView();
            _markAllReadButton.onClick.AddListener(() => _notices.MarkAllRead());
        }

        protected override void OnTabSelected(int index) => Refresh();

        private void Refresh()
        {
            var notices = _notices.Notices;
            while (_rows.Count < notices.Count)
            {
                var row = Instantiate(_rowPrefab, _rowParent);
                int index = _rows.Count;
                row.Button.onClick.AddListener(() => OpenAsync(index).Forget(Debug.LogException));
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool shown = i < notices.Count;
                _rows[i].gameObject.SetActive(shown);
                if (shown) { _rows[i].Show(notices[i]); }
            }

            EmptyLabel.gameObject.SetActive(notices.Count == 0);
            _markAllReadButton.interactable = _notices.UnreadCount > 0;
        }

        private async UniTask OpenAsync(int index)
        {
            if (index >= _notices.Notices.Count) { return; }

            var notice = _notices.Notices[index];
            _notices.MarkRead(notice.Id);
            await _confirm.ShowAsync(new ConfirmRequest { Title = notice.Title, Message = notice.Content });
        }
    }
}
