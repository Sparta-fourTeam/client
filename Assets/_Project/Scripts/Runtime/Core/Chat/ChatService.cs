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
        /// <summary>현재 상태 문구의 UIStrings 키. 문구는 읽을 때 현재 로케일로 만들어 언어를 바꿔도 낡지 않는다</summary>
        public string StatusKey { get; private set; }
        public string Status => L10n.Get(StatusKey);

        public ChatService(IChatClient client, IBufferedPublisher<ChatChanged> changed)
        {
            _token = _lifetime.Token;
            StatusKey = "chat.statusReady";
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
            StatusKey = "chat.statusConnecting";
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
                }, attempt.Token).ToAsyncLazy().Task;
                started = true;
                if (await UniTask.WhenAny(subscribed.Task, running) == 1)
                { throw new InvalidOperationException("Chat disconnected before subscription."); }
                StatusKey = "chat.statusLoading";
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
                StatusKey = "chat.statusLive";
                Notify();
                await running;
                StatusKey = "chat.statusDisconnected";
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (NotSupportedException) { StatusKey = "chat.statusLoginRequired"; }
            catch (Exception) { StatusKey = "chat.statusFailed"; }
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
                StatusKey = "chat.statusSendFailed";
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
