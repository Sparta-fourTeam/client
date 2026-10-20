using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Network
{
    /// <summary>GET /api/notifications/subscribe SSE에 연결해 notice 이벤트만 공지로 넘긴다.
    /// connect 이벤트는 연결 확인으로, ping은 하트비트로만 쓰고, forceLogout은 다른 기기 로그인으로 이 세션이 끊겼다는 알림이다. 서버 로그인 상태에서만 연결한다.
    /// ping이 30초마다 오므로 IdleTimeoutSeconds 동안 아무 줄도 없으면 죽은 연결로 보고 끝낸다</summary>
    public sealed class HttpNoticeClient : INoticeClient
    {
        public const float IdleTimeoutSeconds = 75f;
        private const string NoticeEvent = "notice";
        private const string ConnectEvent = "connect";
        private const string ForceLogoutEvent = "forceLogout";

        private readonly HttpApiConfig _config;
        private readonly AuthSession _auth;

        public HttpNoticeClient(HttpApiConfig config, AuthSession auth)
        {
            _config = config;
            _auth = auth;
        }

        public async UniTask RunAsync(Action connected, Action<Notice> received, Action forceLogout, CancellationToken cancellationToken)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException("WebGL cannot stream SSE.");
#else
            if (!_auth.IsSignedIn)
            {
                throw new NotSupportedException("Server sign-in required.");
            }

            long generation = _auth.Generation;
            using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_config.BaseUrl}/api/notifications/subscribe");
            request.Headers.Accept.ParseAdd("text/event-stream");
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _auth.AccessToken);

            using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            idle.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, idle.Token);
            response.EnsureSuccessStatusCode();

            // 읽기 대기 중에는 취소 토큰이 닿지 않으므로, 취소되면 응답을 닫아 읽기를 깨운다
            using var abort = idle.Token.Register(response.Dispose);
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync());
            var parser = new SseParser();
            while (true)
            {
                idle.CancelAfter(TimeSpan.FromSeconds(IdleTimeoutSeconds));
                string line;
                try
                {
                    line = await reader.ReadLineAsync();
                }
                catch (Exception) when (idle.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("Notice stream went silent.");
                }

                if (line == null)
                {
                    return;
                }

                // 로그아웃하거나 다른 계정으로 로그인했으면 옛 토큰의 스트림을 끝낸다
                if (_auth.Generation != generation)
                {
                    return;
                }

                if (!parser.TryFeed(line, out var evt))
                {
                    continue;
                }

                if (evt.Name == ConnectEvent)
                {
                    await UniTask.SwitchToMainThread(cancellationToken);
                    connected();
                }
                else if (evt.Name == NoticeEvent && TryMap(evt.Data, out var notice))
                {
                    await UniTask.SwitchToMainThread(cancellationToken);
                    received(notice);
                }
                else if (evt.Name == ForceLogoutEvent)
                {
                    // 이 세션은 서버에서 끊겼다. 알리고 스트림을 끝낸다(다시 붙어도 옛 토큰이라 거절된다)
                    await UniTask.SwitchToMainThread(cancellationToken);
                    forceLogout();
                    return;
                }
            }
#endif
        }

        // 잘못된 공지 한 건 때문에 스트림 전체를 끊지 않는다
        private static bool TryMap(string json, out Notice notice)
        {
            notice = null;
            try
            {
                var row = JsonConvert.DeserializeObject<NoticeDto>(json);
                if (row?.id == null || row.id <= 0 || (string.IsNullOrWhiteSpace(row.title) && string.IsNullOrWhiteSpace(row.content))
                    || !DateTimeOffset.TryParse(row.createdAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdAt))
                {
                    Debug.LogWarning($"[Notice] 잘못된 공지를 건너뜁니다: {json}");
                    return false;
                }

                notice = new Notice(row.id.Value, row.title, row.content, createdAt);
                return true;
            }
            catch (JsonException)
            {
                Debug.LogWarning($"[Notice] 공지를 읽지 못해 건너뜁니다: {json}");
                return false;
            }
        }

        [Serializable]
        private sealed class NoticeDto
        {
            public long? id;
            public string title;
            public string content;
            public string createdAt;
        }
    }
}
