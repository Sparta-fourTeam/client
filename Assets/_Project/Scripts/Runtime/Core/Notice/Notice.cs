using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>서버가 SSE로 보낸 공지. 읽음 여부는 기기에만 저장한다</summary>
    public sealed class Notice
    {
        public long Id { get; }
        public string Title { get; }
        public string Content { get; }
        public DateTimeOffset CreatedAt { get; }
        public bool IsRead { get; set; }

        public Notice(long id, string title, string content, DateTimeOffset createdAt, bool isRead = false)
        {
            Id = id;
            Title = title;
            Content = content;
            CreatedAt = createdAt;
            IsRead = isRead;
        }
    }

    /// <summary>공지 목록이나 읽음 상태가 바뀌었다</summary>
    public readonly struct NoticeChanged { }

    public interface INoticeClient
    {
        /// <summary>공지 스트림에 연결하고 끊길 때까지 실행한다. 서버가 연결을 확인하면 connected를, 공지가 오면 received를,
        /// 다른 기기에서 로그인해 이 세션을 끊으라는 forceLogout 이벤트가 오면 forceLogout을 메인 스레드에서 부른다(forceLogout 뒤에는 스트림을 끝낸다).
        /// 로그인 전이면 NotSupportedException을 던지고, 취소하면 연결을 종료한다</summary>
        UniTask RunAsync(Action connected, Action<Notice> received, Action forceLogout, CancellationToken cancellationToken);
    }

    public interface INoticeStore
    {
        /// <summary>저장된 공지. 없거나 읽을 수 없으면 빈 목록</summary>
        IReadOnlyList<Notice> Load();

        void Save(IReadOnlyList<Notice> notices);
    }
}
