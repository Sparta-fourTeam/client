using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>SSE 줄 파싱이 ping·주석을 공지로 착각하거나 여러 줄 data를 잘못 잇는 회귀, 같은 공지의 중복 저장·팝업 순서·개수 제한 회귀를 막는다</summary>
    public sealed class NoticeTests
    {
        private static readonly DateTimeOffset Base = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

        [Test]
        public void SseParser_EmitsOnBlankLine_SkipsCommentsAndJoinsMultilineData()
        {
            var parser = new SseParser();
            Assert.IsFalse(parser.TryFeed(": keep-alive", out _));
            Assert.IsFalse(parser.TryFeed("event: notice", out _));
            Assert.IsFalse(parser.TryFeed("data: {\"a\":", out _));
            Assert.IsFalse(parser.TryFeed("data:1}", out _));
            Assert.IsTrue(parser.TryFeed("", out var evt));
            Assert.AreEqual("notice", evt.Name);
            Assert.AreEqual("{\"a\":\n1}", evt.Data);

            // 이름이 없으면 message이고, data가 없는 이벤트는 내보내지 않으며 이전 이벤트의 이름이 새어 나오지 않는다
            Assert.IsFalse(parser.TryFeed("event: ping", out _));
            Assert.IsFalse(parser.TryFeed("", out _));
            parser.TryFeed("data: x", out _);
            Assert.IsTrue(parser.TryFeed("", out var plain));
            Assert.AreEqual("message", plain.Name);
        }

        [Test]
        public void Service_IgnoresDuplicateIds_KeepsNewestFirst_AndCapsAtMax()
        {
            var store = new MemoryStore();
            var service = new NoticeService(new IdleClient(), store, new Publisher());
            service.Receive(Make(1, 1));
            service.Receive(Make(1, 5));
            Assert.AreEqual(1, service.Notices.Count, "같은 Id는 한 번만 담는다");

            for (int i = 2; i <= NoticeService.MaxNotices + 5; i++)
            {
                service.Receive(Make(i, i));
            }

            Assert.AreEqual(NoticeService.MaxNotices, service.Notices.Count);
            Assert.AreEqual(NoticeService.MaxNotices + 5, service.Notices[0].Id, "최신 공지가 맨 앞에 온다");
            Assert.IsNull(service.Notices.FirstOrDefault(n => n.Id == 1), "넘치면 가장 오래된 것부터 버린다");
            Assert.AreEqual(NoticeService.MaxNotices, store.Saved.Count);
        }

        [Test]
        public void Service_NextUnread_IsOldestFirst_AndReadStateIsRestoredFromStore()
        {
            var store = new MemoryStore();
            var service = new NoticeService(new IdleClient(), store, new Publisher());
            service.Receive(Make(2, 20));
            service.Receive(Make(1, 10));
            service.Receive(Make(3, 30));
            Assert.AreEqual(1, service.NextUnread().Id);

            service.MarkRead(1);
            Assert.AreEqual(2, service.NextUnread().Id);
            Assert.AreEqual(2, service.UnreadCount);

            var restored = new NoticeService(new IdleClient(), store, new Publisher());
            Assert.AreEqual(2, restored.UnreadCount, "읽음 상태는 저장돼서 다시 켜도 유지된다");
            Assert.IsTrue(restored.Notices.First(n => n.Id == 1).IsRead);
        }

        [Test]
        public void Service_ForceLogout_IsPendingUntilConsumed_AndNeverStoredAsNotice()
        {
            var store = new MemoryStore();
            var service = new NoticeService(new KickedClient(), store, new Publisher());
            Assert.IsFalse(service.ForceLogoutPending);

            service.Start();
            Assert.IsTrue(service.ForceLogoutPending, "forceLogout 이벤트가 오면 처리 대기로 남는다");
            Assert.IsTrue(service.ConsumeForceLogout());
            Assert.IsFalse(service.ConsumeForceLogout(), "한 번 처리하면 다시 true가 되지 않는다");
            Assert.AreEqual(0, service.Notices.Count, "forceLogout은 공지 목록에 저장하지 않는다");
            service.Dispose();
        }

        private static Notice Make(long id, int minutes) => new Notice(id, $"t{id}", $"c{id}", Base.AddMinutes(minutes));

        private sealed class MemoryStore : INoticeStore
        {
            public List<Notice> Saved { get; private set; } = new();

            public IReadOnlyList<Notice> Load() => Saved.ConvertAll(n => new Notice(n.Id, n.Title, n.Content, n.CreatedAt, n.IsRead));

            public void Save(IReadOnlyList<Notice> notices) => Saved = new List<Notice>(notices);
        }

        private sealed class IdleClient : INoticeClient
        {
            public UniTask RunAsync(Action connected, Action<Notice> received, Action forceLogout, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class KickedClient : INoticeClient
        {
            public UniTask RunAsync(Action connected, Action<Notice> received, Action forceLogout, CancellationToken cancellationToken)
            {
                forceLogout();
                return UniTask.CompletedTask;
            }
        }

        private sealed class Publisher : IBufferedPublisher<NoticeChanged>
        {
            public void Publish(NoticeChanged message) { }
        }
    }
}
