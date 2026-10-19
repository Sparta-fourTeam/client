using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using MessagePipe;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ChatTests
    {
        [Test]
        public void Frames_PreserveFragmentedUtf8AndReadMultipleFramesWithHeartbeats()
        {
            var reader = new StompFrameReader();
            byte[] bytes = Encoding.UTF8.GetBytes("\nCONNECTED\r\nversion:1.2\r\n\r\n\0\nMESSAGE\ndestination:/topic/chat\ncontent-length:6\n\n안녕\0");
            for (int i = 0; i < bytes.Length - 1; i++) { reader.Append(new[] { bytes[i] }, 1); }
            Assert.IsTrue(reader.TryRead(out var connected));
            Assert.AreEqual("CONNECTED", connected.Command);
            Assert.IsFalse(reader.TryRead(out _), "NUL 도착 전 메시지를 공개하지 않는다");
            reader.Append(new byte[] { 0 }, 1);
            Assert.IsTrue(reader.TryRead(out var message));
            Assert.AreEqual("안녕", message.Body);
            Assert.IsFalse(reader.TryRead(out _));
        }

        [Test]
        public void Frames_HonorByteLengthIncludingEmbeddedNulAndFirstHeader()
        {
            var reader = new StompFrameReader();
            var bytes = Encoding.UTF8.GetBytes("MESSAGE\nx:a\\cb\nx:ignored\ncontent-length:3\n\na\0b\0");
            reader.Append(bytes, bytes.Length);
            Assert.IsTrue(reader.TryRead(out var frame));
            Assert.AreEqual("a:b", frame.Headers["x"]);
            Assert.AreEqual("a\0b", frame.Body);
        }

        [Test]
        public void Frames_RejectInvalidLengthAndMissingTerminator()
        {
            foreach (string frame in new[] { "MESSAGE\ncontent-length:-1\n\na\0", "MESSAGE\ncontent-length:1\n\nab" })
            {
                var reader = new StompFrameReader();
                var bytes = Encoding.UTF8.GetBytes(frame); reader.Append(bytes, bytes.Length);
                Assert.Throws<FormatException>(() => reader.TryRead(out _));
            }
        }

        [Test]
        public void Recent_UsesAuthenticatedRestAndPreservesServerOrder()
        {
            var auth = new AuthSession(); auth.Set("test-access", "test-refresh");
            var transport = new FakeHttpTransport
            {
                Response = new HttpResponseData(200,
                "[{\"senderId\":4,\"content\":\"먼저\",\"sentAt\":\"2026-10-19T03:54:04.298Z\"},{\"senderId\":5,\"content\":\"다음\",\"sentAt\":\"2026-10-19T03:55:04.298Z\"}]")
            };
            var config = new HttpApiConfig("https://example.test");
            var api = new HttpChatClient(new HttpApiClient(config, transport, auth), config, auth);
            var recent = api.GetRecentAsync(default).GetAwaiter().GetResult();
            Assert.AreEqual("https://example.test/api/chat/recent", transport.Requests[0].Url);
            Assert.AreEqual("Bearer test-access", transport.Requests[0].Headers["Authorization"]);
            Assert.AreEqual("먼저", recent[0].Content);
            Assert.AreEqual("다음", recent[1].Content);
        }

        [Test]
        public void Chat_ShowsOnlyBroadcastAndCountsUnreadOnlyWhileClosed()
        {
            var client = new Client();
            using var service = new ChatService(client, new Publisher());
            service.Start();
            Assert.AreEqual(1, service.Messages.Count);
            Assert.IsNull(service.OnlineCount);
            client.Presence(128);
            Assert.AreEqual(128, service.OnlineCount);
            client.Presence(0);
            Assert.AreEqual(0, service.OnlineCount, "0명도 서버가 보낸 실제 상태다");
            Assert.Zero(service.UnreadCount, "최근 대화는 새 알림에 세지 않는다");
            Assert.IsFalse(service.SendAsync(" \t").GetAwaiter().GetResult());
            Assert.IsTrue(service.SendAsync("안녕").GetAwaiter().GetResult());
            Assert.AreEqual(1, service.Messages.Count, "송신 완료로 메시지를 미리 추가하지 않는다");
            client.Received(new ChatMessage(4, "안녕", DateTimeOffset.UtcNow));
            Assert.AreEqual(2, service.Messages.Count);
            Assert.AreEqual(1, service.UnreadCount);
            service.SetOpen(true);
            client.Received(new ChatMessage(5, "반가워", DateTimeOffset.UtcNow));
            Assert.Zero(service.UnreadCount);
            service.Dispose();
            Assert.IsTrue(client.Token.IsCancellationRequested);
            Assert.IsNull(service.OnlineCount, "연결 종료 후 이전 인원수를 현재 값으로 표시하지 않는다");
        }

        [Test]
        public void Chat_SubscriptionHandoffMergesHistoryAndLiveMessagesWithoutLossOrDuplicates()
        {
            var time = DateTimeOffset.Parse("2026-10-19T03:00:00Z");
            var old = new ChatMessage(1, "기존", time);
            var early = new ChatMessage(2, "구독 직후", time.AddSeconds(1));
            var during = new ChatMessage(3, "조회 중", time.AddSeconds(2));
            var late = new ChatMessage(4, "늦은 방송", time.AddSeconds(3));
            var client = new Client { DelayConnected = true, PendingHistory = new() };
            using var service = new ChatService(client, new Publisher());

            service.Start();
            Assert.Zero(client.RecentRequests, "구독 확인 전에는 REST 스냅샷을 찍지 않는다");
            client.Received(early);
            client.Connected();
            client.Received(during);
            Assert.IsFalse(service.IsConnected, "이력 병합 전에는 전송을 열지 않는다");
            client.PendingHistory.TrySetResult(new[] { old, early, late });

            Assert.IsTrue(service.IsConnected);
            CollectionAssert.AreEqual(new[] { old, early, during, late }, service.Messages);
            Assert.AreEqual(1, service.UnreadCount, "이력에 이미 포함된 방송은 중복 알림에 세지 않는다");
            client.Received(late);
            Assert.AreEqual(4, service.Messages.Count, "REST보다 늦게 도착한 같은 방송도 한 번만 표시한다");
            client.Received(new ChatMessage(5, "연결 이후", time.AddSeconds(4)));
            Assert.AreEqual(5, service.Messages.Count);
            Assert.AreEqual(2, service.UnreadCount);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Chat_InterruptedHistoryClosesAttemptAndCanRetry(bool disconnected)
        {
            var client = new Client { PendingHistory = new() };
            using var service = new ChatService(client, new Publisher());
            service.Start();
            var firstToken = client.Token;
            client.Received(new ChatMessage(2, "실패한 시도", DateTimeOffset.UtcNow));
            if (disconnected) { client.Disconnect(); }
            else { client.PendingHistory.TrySetException(new InvalidOperationException("history unavailable")); }
            Assert.IsTrue(firstToken.IsCancellationRequested);
            Assert.IsFalse(service.IsConnected);
            Assert.IsEmpty(service.Messages);
            client.PendingHistory = null;

            service.Retry();

            Assert.IsTrue(service.IsConnected);
            Assert.AreEqual(2, client.RecentRequests);
            Assert.AreEqual(1, service.Messages.Count);
            Assert.AreEqual("기존", service.Messages[0].Content);
        }

        private sealed class Publisher : IBufferedPublisher<ChatChanged>
        { public void Publish(ChatChanged message) { } }
        private sealed class Client : IChatClient
        {
            public Action<ChatMessage> Received;
            public Action<int> Presence;
            public CancellationToken Token;
            private UniTaskCompletionSource _disconnected;
            public Action Connected;
            public bool DelayConnected;
            public int RecentRequests;
            public UniTaskCompletionSource<IReadOnlyList<ChatMessage>> PendingHistory;
            public UniTask<IReadOnlyList<ChatMessage>> GetRecentAsync(CancellationToken token)
            {
                RecentRequests++;
                if (PendingHistory != null)
                {
                    var pending = PendingHistory;
                    token.Register(() => pending.TrySetCanceled(token));
                    return pending.Task;
                }
                return UniTask.FromResult<IReadOnlyList<ChatMessage>>(new[] { new ChatMessage(1, "기존", DateTimeOffset.UtcNow) });
            }
            public void Disconnect() => _disconnected.TrySetResult();
            public UniTask RunAsync(Action connected, Action<ChatMessage> received, Action<int> presence, CancellationToken token)
            {
                var disconnected = _disconnected = new UniTaskCompletionSource();
                Token = token; Received = received; Presence = presence; Connected = connected;
                token.Register(() => disconnected.TrySetCanceled(token));
                if (!DelayConnected) { connected(); }
                return disconnected.Task;
            }
            public UniTask SendAsync(string content, CancellationToken token) => UniTask.CompletedTask;
        }
    }
}
