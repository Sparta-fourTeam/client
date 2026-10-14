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

    /// <summary>로그인으로 받은 토큰을 들고 있는 곳. HttpApiClient가 액세스 토큰 만료(401)를 만나면
    /// 리프레시 토큰으로 새 토큰을 받아 여기에 다시 담는다</summary>
    public interface IAuthTokenStore : IAuthHeaderProvider
    {
        string AccessToken { get; }
        string RefreshToken { get; }
        void Set(string accessToken, string refreshToken);
        void Clear();
    }

    public sealed class NoAuthHeaders : IAuthHeaderProvider
    {
        private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> GetHeaders() => Empty;
    }

    /// <summary>JSON 요청을 보내고 응답을 DTO로 바꾼다. 실패는 전부 ApiException으로 던진다 (취소만 OperationCanceledException).
    /// 자동 재시도는 하지 않는다: 상태를 바꾸는 요청을 무조건 다시 보내면 중복 처리될 수 있어, 재시도 여부는
    /// 멱등성 규칙을 아는 호출부가 ApiErrorKind를 보고 정한다.
    /// 단 하나의 예외가 액세스 토큰 만료다. 인증된 요청이 401이면 서버가 요청을 처리하기 전에 거절한 것이라 다시 보내도 중복되지 않으므로,
    /// 리프레시 토큰으로 토큰을 새로 받아 같은 요청을 한 번만 다시 보낸다 (아래 RefreshAsync).
    /// 로그인·재발급 경로(api/auth/)는 이 처리를 하지 않는다</summary>
    public sealed class HttpApiClient
    {
        private const string AuthPathPrefix = "api/auth/";
        private const string RefreshPath = "api/auth/refresh";

        private readonly HttpApiConfig _config;
        private readonly IHttpTransport _transport;
        private readonly IAuthHeaderProvider _auth;
        private readonly Func<int, string, string> _errorCodeReader;
        private readonly IAuthTokenStore _tokens;

        // 재발급은 한 번에 하나만 한다. 서버는 쓴 리프레시 토큰을 무효로 하고, 이미 쓴 토큰이 또 오면 저장된 토큰까지 지워
        // 다시 로그인하게 만든다. 요청이 동시에 401을 받아도 재발급을 두 번 보내면 로그인이 풀리기 때문이다
        private UniTaskCompletionSource<RefreshOutcome> _refreshing;

        // 재발급 결과. 같은 재발급을 기다리는 요청에게 성공·실패·오류를 그대로 전한다 (예외로 완료하면 기다리는 요청이 없을 때 처리되지 않은 예외로 기록된다)
        private sealed class RefreshOutcome
        {
            public bool Refreshed;
            public Exception Error;
        }

        public HttpApiClient(HttpApiConfig config, IHttpTransport transport, IAuthHeaderProvider auth = null,
            Func<int, string, string> errorCodeReader = null, IAuthTokenStore tokens = null)
        {
            _config = config;
            _transport = transport;
            _auth = auth ?? (IAuthHeaderProvider)tokens ?? new NoAuthHeaders();
            _errorCodeReader = errorCodeReader;
            _tokens = tokens;
        }

        /// <summary>isValid는 응답이 계약대로인지(필수 필드가 채워졌는지) 가리는 규칙이다. JSON 문법만 맞고 모양이 다른 응답
        /// (예: {})은 역직렬화가 성공하므로, 필수 필드를 아는 API 쪽에서 넘긴다. 주지 않으면 null 여부만 본다</summary>
        public async UniTask<TResponse> Get<TResponse>(string path, CancellationToken cancellationToken = default,
            Func<TResponse, bool> isValid = null)
        {
            var body = await Execute(HttpVerb.Get, path, null, cancellationToken);
            return Deserialize(body, isValid);
        }

        public async UniTask<TResponse> Post<TRequest, TResponse>(string path, TRequest request,
            CancellationToken cancellationToken = default, Func<TResponse, bool> isValid = null)
        {
            var body = await Execute(HttpVerb.Post, path, JsonConvert.SerializeObject(request), cancellationToken);
            return Deserialize(body, isValid);
        }

        /// <summary>응답 본문이 없는 요청 (2xx만 오면 성공)</summary>
        public async UniTask Post<TRequest>(string path, TRequest request, CancellationToken cancellationToken = default)
        {
            await Execute(HttpVerb.Post, path, JsonConvert.SerializeObject(request), cancellationToken);
        }

        private async UniTask<string> Execute(HttpVerb verb, string path, string jsonBody, CancellationToken cancellationToken)
        {
            string sentAccessToken = _tokens?.AccessToken;
            var response = await Send(verb, path, jsonBody, cancellationToken);

            var error = HttpErrorMapper.Map(response, _errorCodeReader);
            if (error == null)
            {
                return response.Body;
            }

            if (!ShouldRefresh(path, response, sentAccessToken))
            {
                throw error;
            }

            if (!await RefreshAsync(sentAccessToken, cancellationToken))
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired);
            }

            // 새 토큰으로 같은 요청을 한 번만 다시 보낸다. 또 401이면 그대로 던진다
            response = await Send(verb, path, jsonBody, cancellationToken);
            error = HttpErrorMapper.Map(response, _errorCodeReader);
            if (error != null)
            {
                throw error;
            }

            return response.Body;
        }

        private UniTask<HttpResponseData> Send(HttpVerb verb, string path, string jsonBody, CancellationToken cancellationToken)
        {
            var url = $"{_config.BaseUrl}/{path.TrimStart('/')}";
            return _transport.Send(new HttpRequestData(verb, url, _auth.GetHeaders(), jsonBody), _config.TimeoutSeconds, cancellationToken);
        }

        // 로그인한 상태로 보낸 요청이 401을 받았을 때만 토큰 만료로 본다. 로그인·재발급 요청의 401은 자격 증명이 틀린 것이다
        private bool ShouldRefresh(string path, HttpResponseData response, string sentAccessToken) =>
            _tokens != null
            && response.StatusCode == 401
            && !string.IsNullOrEmpty(sentAccessToken)
            && !path.TrimStart('/').StartsWith(AuthPathPrefix, StringComparison.Ordinal);

        /// <summary>새 토큰을 받았거나 다른 요청이 이미 받아 둔 상태면 true. 리프레시 토큰이 거절돼 로그인이 필요하면 false(토큰을 비운다).
        /// 네트워크 오류나 서버 오류처럼 로그인이 풀린 것이 아닌 실패는 예외로 던지고 토큰은 그대로 둔다</summary>
        private async UniTask<bool> RefreshAsync(string sentAccessToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 이 요청을 보낸 뒤 다른 요청이 이미 새 토큰을 받았으면 그 토큰으로 다시 보내면 된다
            if (!string.IsNullOrEmpty(_tokens.AccessToken) && _tokens.AccessToken != sentAccessToken)
            {
                return true;
            }

            var refreshing = _refreshing;
            if (refreshing == null)
            {
                string refreshToken = _tokens.RefreshToken;
                if (string.IsNullOrEmpty(refreshToken))
                {
                    _tokens.Clear();
                    return false;
                }

                refreshing = _refreshing = new UniTaskCompletionSource<RefreshOutcome>();
                CompleteRefreshAsync(refreshing, refreshToken).Forget();
            }

            // 개별 요청의 취소는 기다림만 끝낸다. 재발급 전송까지 취소하면 서버가 이미 회전시킨 토큰을
            // 다른 요청이 다시 사용할 수 있어, 공유 전송은 HttpApiConfig의 제한 시간 안에서 끝까지 받는다.
            var shared = await refreshing.Task.AttachExternalCancellation(cancellationToken);
            if (shared.Error != null)
            {
                throw shared.Error;
            }

            return shared.Refreshed;
        }

        private async UniTask CompleteRefreshAsync(UniTaskCompletionSource<RefreshOutcome> refreshing, string refreshToken)
        {
            var outcome = new RefreshOutcome();
            try
            {
                outcome.Refreshed = await RequestNewTokensAsync(refreshToken, CancellationToken.None);
            }
            catch (Exception ex)
            {
                outcome.Error = ex;
            }
            finally
            {
                _refreshing = null;
                refreshing.TrySetResult(outcome);
            }
        }

        private async UniTask<bool> RequestNewTokensAsync(string refreshToken, CancellationToken cancellationToken)
        {
            // 재발급 요청에는 만료된 액세스 토큰을 붙이지 않는다
            var url = $"{_config.BaseUrl}/{RefreshPath}";
            var body = JsonConvert.SerializeObject(new RefreshRequest { refreshToken = refreshToken });
            var response = await _transport.Send(new HttpRequestData(HttpVerb.Post, url, new Dictionary<string, string>(), body),
                _config.TimeoutSeconds, cancellationToken);

            ApiException error = HttpErrorMapper.Map(response, _errorCodeReader);
            if (error != null)
            {
                // 서버가 리프레시 토큰을 거절하면(만료·이미 사용) 로그인이 필요하다. 그 밖의 실패는 잠시 뒤 다시 시도할 수 있다
                if (error.Kind == ApiErrorKind.Rejected)
                {
                    _tokens.Clear();
                    return false;
                }

                throw error;
            }

            AuthTokens tokens = null;
            try
            {
                tokens = JsonConvert.DeserializeObject<AuthTokens>(response.Body ?? string.Empty);
            }
            catch (JsonException)
            {
            }

            if (tokens == null || string.IsNullOrEmpty(tokens.accessToken) || string.IsNullOrEmpty(tokens.refreshToken))
            {
                _tokens.Clear();
                return false;
            }

            _tokens.Set(tokens.accessToken, tokens.refreshToken);
            return true;
        }

        private static T Deserialize<T>(string json, Func<T, bool> isValid)
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

            if (value == null || (isValid != null && !isValid(value)))
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
            }

            return value;
        }
    }
}
