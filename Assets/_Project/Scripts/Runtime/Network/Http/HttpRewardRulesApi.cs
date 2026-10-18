using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>서버 기준 조회 모듈. 실제 앱 모드 전환·DI 등록은 별도 연결 작업에서 수행한다.</summary>
    public sealed class HttpRewardRulesApi
    {
        private readonly HttpApiConfig _config;
        private readonly IHttpTransport _transport;
        private readonly IAuthTokenStore _tokens;

        public HttpRewardRulesApi(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens)
        { _config = config; _transport = transport; _tokens = tokens; }

        /// <summary>유효한 한 버전의 일정/분류를 모두 확인한 뒤 적용한다. 실패 시 기존 데이터는 유지한다.</summary>
        public async UniTask Refresh(GameDataStore data, CancellationToken cancellationToken = default)
        {
            long generation = _tokens.Generation;
            void EnsureSession()
            {
                if (_tokens.Generation != generation || string.IsNullOrEmpty(_tokens.AccessToken))
                { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
            }
            EnsureSession();
            var client = new HttpApiClient(_config, new BoundTransport(_transport, EnsureSession), tokens: _tokens);
            string body = await client.GetJson("api/reward-rules", cancellationToken);
            EnsureSession();
            try
            {
                using var reader = new JsonTextReader(new StringReader(body ?? string.Empty));
                var json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || json["version"]?.Type != JTokenType.Integer
                    || !int.TryParse(json["version"].ToString(), out var version) || version <= 0)
                { throw new InvalidOperationException("버전이 필요합니다"); }
                data.ApplyRewardRules(json.ToString(Formatting.None), version);
            }
            catch (Exception error) when (error is JsonException || error is InvalidOperationException || error is OverflowException)
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse); }
        }

        private sealed class BoundTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;
            private readonly Action _ensure;
            public BoundTransport(IHttpTransport inner, Action ensure) { _inner = inner; _ensure = ensure; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { _ensure(); return _inner.Send(request, timeoutSeconds, cancellationToken); }
        }
    }
}
