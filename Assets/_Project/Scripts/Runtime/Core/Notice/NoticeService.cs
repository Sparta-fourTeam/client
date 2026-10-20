using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>앱이 켜져 있는 동안 공지 스트림을 유지하고 받은 공지를 저장한다. 전투 중에 온 공지도 받으려고 Root에 둔다.
    /// 끊기면 지수 백오프로 다시 연결하고, 같은 Id의 공지는 한 번만 담는다</summary>
    public sealed class NoticeService : IStartable, IDisposable
    {
        public const int MaxNotices = 50;
        private const float NotSupportedRetrySeconds = 5f;
        private const float MaxBackoffSeconds = 30f;

        private readonly INoticeClient _client;
        private readonly INoticeStore _store;
        private readonly IBufferedPublisher<NoticeChanged> _changed;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly List<Notice> _notices = new();
        private bool _disposed;

        /// <summary>최신 공지가 앞에 온다</summary>
        public IReadOnlyList<Notice> Notices => _notices;

        public int UnreadCount => _notices.Count(notice => !notice.IsRead);

        /// <summary>다른 기기에서 로그인해 이 세션을 끊으라는 서버 요청이 와 있다. 새 세션으로 연결되거나 로그아웃돼 있으면 해제된다</summary>
        public bool ForceLogoutPending { get; private set; }

        /// <summary>강제 로그아웃 요청을 처리하기 시작한다. 요청이 있었으면 true를 돌려주고 한 번만 true가 된다</summary>
        public bool ConsumeForceLogout()
        {
            bool pending = ForceLogoutPending;
            ForceLogoutPending = false;
            return pending;
        }

        public NoticeService(INoticeClient client, INoticeStore store, IBufferedPublisher<NoticeChanged> changed)
        {
            _client = client;
            _store = store;
            _changed = changed;
            _notices.AddRange(store.Load().OrderByDescending(notice => notice.CreatedAt).ThenByDescending(notice => notice.Id).Take(MaxNotices));
        }

        public void Start() => RunLoopAsync().Forget();

        /// <summary>팝업으로 보여 줄 다음 공지. 읽지 않은 것 중 가장 오래된 것부터 차례로 나온다. 없으면 null</summary>
        public Notice NextUnread() => _notices.Where(notice => !notice.IsRead)
            .OrderBy(notice => notice.CreatedAt).ThenBy(notice => notice.Id).FirstOrDefault();

        public void MarkRead(long id)
        {
            var notice = _notices.Find(n => n.Id == id);
            if (notice == null || notice.IsRead)
            {
                return;
            }

            notice.IsRead = true;
            Commit();
        }

        public void MarkAllRead()
        {
            bool changed = false;
            foreach (var notice in _notices.Where(n => !n.IsRead))
            {
                notice.IsRead = true;
                changed = true;
            }

            if (changed)
            {
                Commit();
            }
        }

        /// <summary>새 공지를 담는다. 이미 있는 Id면 무시한다. 목록은 최근 MaxNotices개만 남긴다</summary>
        public void Receive(Notice notice)
        {
            if (_disposed || notice == null || _notices.Exists(n => n.Id == notice.Id))
            {
                return;
            }

            _notices.Add(notice);
            _notices.Sort((a, b) =>
            {
                int byTime = b.CreatedAt.CompareTo(a.CreatedAt);
                return byTime != 0 ? byTime : b.Id.CompareTo(a.Id);
            });
            if (_notices.Count > MaxNotices)
            {
                _notices.RemoveRange(MaxNotices, _notices.Count - MaxNotices);
            }

            Commit();
        }

        private void Commit()
        {
            _store.Save(_notices);
            _changed.Publish(new NoticeChanged());
        }

        private async UniTaskVoid RunLoopAsync()
        {
            var token = _lifetime.Token;
            int failures = 0;
            while (!token.IsCancellationRequested)
            {
                float delay;
                try
                {
                    await _client.RunAsync(() =>
                    {
                        // 새 세션으로 연결됐다. 옛 세션의 강제 로그아웃 요청은 더 이상 유효하지 않다
                        failures = 0;
                        ForceLogoutPending = false;
                    }, Receive, () => ForceLogoutPending = !_disposed, token);
                    delay = 1f;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (NotSupportedException)
                {
                    // 로그인 전이거나 지원하지 않는 플랫폼. 로그인하면 다음 시도에서 붙는다. 이미 로그아웃돼 있으니 강제 로그아웃 요청은 끝난 것이다
                    ForceLogoutPending = false;
                    delay = NotSupportedRetrySeconds;
                }
                catch (Exception)
                {
                    failures++;
                    delay = Math.Min(MaxBackoffSeconds, (float)Math.Pow(2, failures));
                }

                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.UnscaledDeltaTime, cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
