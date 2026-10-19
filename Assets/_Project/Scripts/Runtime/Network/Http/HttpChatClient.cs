using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;

namespace Game.Network
{
    public sealed class HttpChatClient : IChatClient
    {
        private readonly HttpApiClient _http;
        private readonly HttpApiConfig _config;
        private readonly AuthSession _auth;
        private readonly SemaphoreSlim _write = new(1, 1);
        private ClientWebSocket _socket;

        public HttpChatClient(HttpApiClient http, HttpApiConfig config, AuthSession auth)
        { _http = http; _config = config; _auth = auth; }

        public async UniTask<IReadOnlyList<ChatMessage>> GetRecentAsync(CancellationToken cancellationToken)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException("WebGL cannot set WebSocket handshake headers.");
#else
            if (!_auth.IsSignedIn) { throw new NotSupportedException("Server sign-in required."); }
            var rows = await _http.Get<ChatDto[]>("api/chat/recent", cancellationToken);
            var messages = new List<ChatMessage>(rows.Length);
            foreach (var row in rows) { messages.Add(Map(row)); }
            return messages;
#endif
        }

        public async UniTask RunAsync(Action connected, Action<ChatMessage> received, Action<int> presence, CancellationToken cancellationToken)
        {
            var apiUri = new Uri(_config.BaseUrl);
            var uri = new UriBuilder(apiUri)
            {
                Scheme = apiUri.Scheme == "http" ? "ws" : "wss",
                Path = "/api/ws-chat",
                Port = apiUri.IsDefaultPort ? -1 : apiUri.Port,
                Query = string.Empty,
            }.Uri;
            using var socket = new ClientWebSocket();
            socket.Options.SetRequestHeader("Authorization", $"Bearer {_auth.AccessToken}");
            socket.Options.AddSubProtocol("v12.stomp");
            socket.Options.AddSubProtocol("v11.stomp");
            socket.Options.AddSubProtocol("v10.stomp");
            using var abort = cancellationToken.Register(socket.Abort);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            await socket.ConnectAsync(uri, timeout.Token);
            _socket = socket;
            bool ready = false;
            try
            {
                await WriteAsync(socket, $"CONNECT\naccept-version:1.2,1.1,1.0\nhost:{uri.Host}\nheart-beat:0,0\n\n\0", timeout.Token);
                var reader = new StompFrameReader();
                var buffer = new byte[8192];
                while (socket.State == WebSocketState.Open)
                {
                    var read = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ready ? cancellationToken : timeout.Token);
                    if (read.MessageType == WebSocketMessageType.Close) { return; }
                    if (read.MessageType != WebSocketMessageType.Text) { throw new FormatException("Expected STOMP text."); }
                    reader.Append(buffer, read.Count);
                    while (reader.TryRead(out var frame))
                    {
                        if (frame.Command == "ERROR") { throw new InvalidOperationException("Chat server rejected the frame."); }
                        if (frame.Command == "CONNECTED" && !ready)
                        {
                            if (frame.Headers.TryGetValue("version", out var version) && version != "1.2" && version != "1.1" && version != "1.0")
                            { throw new FormatException("Unsupported STOMP version."); }
                            await WriteAsync(socket, "SUBSCRIBE\nid:chat\ndestination:/topic/chat\nack:auto\n\n\0", timeout.Token);
                            await WriteAsync(socket, "SUBSCRIBE\nid:chat-presence\ndestination:/topic/chat/presence\nack:auto\n\n\0", timeout.Token);
                            ready = true;
                            timeout.CancelAfter(Timeout.Infinite);
                            await UniTask.SwitchToMainThread(cancellationToken);
                            connected();
                        }
                        else if (frame.Command == "MESSAGE" && ready
                            && frame.Headers.TryGetValue("destination", out var destination))
                        {
                            if (destination == "/topic/chat")
                            {
                                var message = Map(JsonConvert.DeserializeObject<ChatDto>(frame.Body));
                                await UniTask.SwitchToMainThread(cancellationToken);
                                received(message);
                            }
                            else if (destination == "/topic/chat/presence")
                            {
                                var update = JsonConvert.DeserializeObject<PresenceDto>(frame.Body);
                                if (update?.onlineCount >= 0)
                                {
                                    await UniTask.SwitchToMainThread(cancellationToken);
                                    presence(update.onlineCount.Value);
                                }
                            }
                        }
                    }
                }
            }
            finally { _socket = null; socket.Abort(); }
        }

        public async UniTask SendAsync(string content, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(content)) { throw new ArgumentException("Message is blank.", nameof(content)); }
            var socket = _socket;
            if (socket == null || socket.State != WebSocketState.Open) { throw new InvalidOperationException("Chat disconnected."); }
            string body = JsonConvert.SerializeObject(new { content });
            string frame = $"SEND\ndestination:/app/chat.send\ncontent-type:application/json\ncontent-length:{Encoding.UTF8.GetByteCount(body)}\n\n{body}\0";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            try { await WriteAsync(socket, frame, timeout.Token); }
            catch { socket.Abort(); throw; }
        }

        private async UniTask WriteAsync(ClientWebSocket socket, string frame, CancellationToken token)
        {
            await _write.WaitAsync(token);
            try { await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(frame)), WebSocketMessageType.Text, true, token); }
            finally { _write.Release(); }
        }

        private static ChatMessage Map(ChatDto row)
        {
            if (row?.senderId == null || row.senderId <= 0 || string.IsNullOrWhiteSpace(row.content)
                || !DateTimeOffset.TryParse(row.sentAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var sentAt))
            { throw new FormatException("Invalid chat message."); }
            return new ChatMessage(row.senderId.Value, row.content, sentAt, row.nickname, row.avatarId ?? -1);
        }

        private sealed class PresenceDto
        {
            public int? onlineCount;
        }

        [Serializable]
        private sealed class ChatDto
        {
            public long? senderId;
            public string content;
            public string sentAt;
            public string nickname;
            public int? avatarId;
        }
    }
}
