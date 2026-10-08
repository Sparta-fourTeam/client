using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;

namespace Game.Network
{
    /// <summary>요청마다 붙일 인증 헤더를 돌려주는 연결 지점. 로그인 방식이 정해지면 토큰을 붙이는 구현을 등록한다</summary>
    public interface IAuthHeaderProvider
    {
        IReadOnlyDictionary<string, string> GetHeaders();
    }

    public sealed class NoAuthHeaders : IAuthHeaderProvider
    {
        private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> GetHeaders() => Empty;
    }

    /// <summary>JSON 요청을 보내고 응답을 DTO로 바꾼다. 실패는 전부 ApiException으로 던진다 (취소만 OperationCanceledException).
    /// 자동 재시도는 하지 않는다: 상태를 바꾸는 요청을 무조건 다시 보내면 중복 처리될 수 있어, 재시도 여부는
    /// 멱등성 규칙을 아는 호출부가 ApiErrorKind를 보고 정한다</summary>
    public sealed class HttpApiClient
    {
        private readonly HttpApiConfig _config;
        private readonly IHttpTransport _transport;
        private readonly IAuthHeaderProvider _auth;
        private readonly Func<int, string, string> _errorCodeReader;

        public HttpApiClient(HttpApiConfig config, IHttpTransport transport, IAuthHeaderProvider auth = null,
            Func<int, string, string> errorCodeReader = null)
        {
            _config = config;
            _transport = transport;
            _auth = auth ?? new NoAuthHeaders();
            _errorCodeReader = errorCodeReader;
        }

        public async UniTask<TResponse> Get<TResponse>(string path, CancellationToken cancellationToken = default)
        {
            var body = await Execute(HttpVerb.Get, path, null, cancellationToken);
            return Deserialize<TResponse>(body);
        }

        public async UniTask<TResponse> Post<TRequest, TResponse>(string path, TRequest request,
            CancellationToken cancellationToken = default)
        {
            var body = await Execute(HttpVerb.Post, path, JsonConvert.SerializeObject(request), cancellationToken);
            return Deserialize<TResponse>(body);
        }

        /// <summary>응답 본문이 없는 요청 (2xx만 오면 성공)</summary>
        public async UniTask Post<TRequest>(string path, TRequest request, CancellationToken cancellationToken = default)
        {
            await Execute(HttpVerb.Post, path, JsonConvert.SerializeObject(request), cancellationToken);
        }

        private async UniTask<string> Execute(HttpVerb verb, string path, string jsonBody, CancellationToken cancellationToken)
        {
            var url = $"{_config.BaseUrl}/{path.TrimStart('/')}";
            var request = new HttpRequestData(verb, url, _auth.GetHeaders(), jsonBody);

            var response = await _transport.Send(request, _config.TimeoutSeconds, cancellationToken);

            var error = HttpErrorMapper.Map(response, _errorCodeReader);
            if (error != null)
            {
                throw error;
            }

            return response.Body;
        }

        private static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
            }

            T value;
            try
            {
                value = JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException)
            {
                // 성공 응답인데 형식이 계약과 다르면 다시 보내도 같은 결과일 가능성이 크다
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
            }

            if (value == null)
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
            }

            return value;
        }
    }
}
