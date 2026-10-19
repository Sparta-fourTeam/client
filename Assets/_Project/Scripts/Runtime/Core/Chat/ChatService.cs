using System;
using System.Collections.Generic;
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
            Status = "최근 대화 불러오는 중…";
            Notify();
            try
            {
                var recent = await _client.GetRecentAsync(_token);
                _messages.Clear();
                _messages.AddRange(recent);
                Status = "채팅 연결 중…";
                Notify();
                await _client.RunAsync(() =>
                {
                    IsConnected = true;
                    Status = "실시간 연결됨";
                    Notify();
                }, Receive, count =>
                {
                    if (count < 0 || _disposed) { return; }
                    OnlineCount = count;
                    Notify();
                }, _token);
                Status = "연결이 끊겼습니다. 다시 연결해 주세요.";
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
            catch (NotSupportedException) { Status = "서버 로그인 후 채팅을 이용할 수 있습니다."; }
            catch (Exception) { Status = "채팅에 연결하지 못했습니다. 다시 연결해 주세요."; }
            finally
            {
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
