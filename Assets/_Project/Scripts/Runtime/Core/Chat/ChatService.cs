using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    public readonly struct ChatChanged { }

    /// <summary>로비 수명 동안 연결을 유지한다. 화면에는 서버가 브로드캐스트한 메시지만 추가한다.</summary>
    public sealed class ChatService : IStartable, IDisposable
    {
        private readonly IChatClient _client;
        private readonly IBufferedPublisher<ChatChanged> _changed;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly CancellationToken _token;
        private readonly List<ChatMessage> _messages = new();
        private bool _running, _open, _sending, _disposed;
        public IReadOnlyList<ChatMessage> Messages => _messages;
        public bool IsConnected { get; private set; }
        public int UnreadCount { get; private set; }
        public int? OnlineCount { get; private set; }
        public string Status { get; private set; } = "연결 준비 중…";

        public ChatService(IChatClient client, IBufferedPublisher<ChatChanged> changed)
        {
            _token = _lifetime.Token;
            _client = client;
            _changed = changed;
        }

        public void Start() => Retry();
        public void Retry()
        {
            if (!_running && !_disposed) { ConnectAsync().Forget(); }
        }

        private async UniTask ConnectAsync()
        {
            _running = true;
            OnlineCount = null;
            Status = "채팅 연결 중…";
            Notify();
            using var attempt = CancellationTokenSource.CreateLinkedTokenSource(_token);
            UniTask running = default;
            bool started = false;
            try
            {
                var subscribed = new UniTaskCompletionSource();
                var buffered = new List<ChatMessage>();
                var overlap = new Dictionary<(long, string, DateTimeOffset), int>();
                bool loading = true;
                void Incoming(ChatMessage message)
                {
                    if (_disposed || attempt.IsCancellationRequested) { return; }
                    if (loading)
                    {
                        buffered.Add(message);
                        if (buffered.Count > 200) { buffered.RemoveAt(0); }
                        return;
                    }
                    var key = (message.SenderId, message.Content, message.SentAt);
                    if (overlap.TryGetValue(key, out int count) && count > 0)
                    { overlap[key] = count - 1; return; }
                    Receive(message);
                }
                running = _client.RunAsync(() => subscribed.TrySetResult(), Incoming, count =>
                {
                    if (count < 0 || _disposed) { return; }
                    OnlineCount = count;
                    Notify();
                }, attempt.Token).Preserve();
                started = true;
                if (await UniTask.WhenAny(subscribed.Task, running) == 1)
                { throw new InvalidOperationException("Chat disconnected before subscription."); }
                Status = "최근 대화 불러오는 중…";
                Notify();
                var history = _client.GetRecentAsync(attempt.Token).Preserve();
                if (await UniTask.WhenAny(history.AsUniTask(), running) == 1)
                { throw new InvalidOperationException("Chat disconnected during history load."); }
                var recent = await history;
                _messages.Clear();
                _messages.AddRange(recent);
                foreach (var message in recent)
                {
                    var key = (message.SenderId, message.Content, message.SentAt);
                    overlap.TryGetValue(key, out int count);
                    overlap[key] = count + 1;
                }
                loading = false;
                foreach (var message in buffered) { Incoming(message); }
                var ordered = _messages.OrderBy(message => message.SentAt).TakeLast(200).ToArray();
                _messages.Clear();
                _messages.AddRange(ordered);
                IsConnected = true;
                Status = "실시간 연결됨";
                Notify();
                await running;
                Status = "연결이 끊겼습니다. 다시 연결해 주세요.";
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (NotSupportedException) { Status = "서버 로그인 후 채팅을 이용할 수 있습니다."; }
            catch (Exception) { Status = "채팅에 연결하지 못했습니다. 다시 연결해 주세요."; }
            finally
            {
                attempt.Cancel();
                if (started)
                {
                    try { await running; }
                    catch (Exception) { } // 위의 연결/조회 오류를 표시하고 소켓 종료까지 기다린다.
                }
                _running = false;
                IsConnected = false;
                OnlineCount = null;
                Notify();
            }
        }

        private void Receive(ChatMessage message)
        {
            if (_disposed) { return; }
            _messages.Add(message);
            if (_messages.Count > 200) { _messages.RemoveAt(0); }
            if (!_open) { UnreadCount++; }
            Notify();
        }

        public void SetOpen(bool open)
        {
            _open = open;
            if (open) { UnreadCount = 0; }
            Notify();
        }

        public async UniTask<bool> SendAsync(string content)
        {
            if (_disposed || _sending || !IsConnected || string.IsNullOrWhiteSpace(content)) { return false; }
            _sending = true;
            try
            {
                await _client.SendAsync(content, _token);
                return true;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return false; }
            catch (Exception)
            {
                Status = "전송을 확인하지 못했습니다. 대화를 확인한 뒤 다시 시도해 주세요.";
                Notify();
                return false;
            }
            finally { _sending = false; }
        }

        private void Notify()
        {
            if (!_disposed) { _changed.Publish(new ChatChanged()); }
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
