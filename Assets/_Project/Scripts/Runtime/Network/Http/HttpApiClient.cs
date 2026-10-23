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
        long Generation { get; }
        bool TryRefresh(long generation, string accessToken, string refreshToken);
        bool TryClear(long generation);
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
    /// 로그인·재발급 경로는 이 처리를 하지 않는다. Bearer 인증 계정 연동·삭제는 재발급 대상이다</summary>
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
        private long _refreshingGeneration;

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
            return Deserialize(body, isValid, Describe(HttpVerb.Get, path));
        }

        /// <summary>날짜 문자열·필드 존재 여부를 직접 검증해야 하는 Network 내부 독자용 원문.
        /// 인증·오류 처리는 같으며, 호출부가 JSON 형태와 필수 필드를 검증해야 한다.</summary>
        internal UniTask<string> GetJson(string path, CancellationToken cancellationToken = default) =>
            Execute(HttpVerb.Get, path, null, cancellationToken);

        /// <summary>필드 존재와 UTC 원문 검증이 필요한 상태 변경 응답용.</summary>
        internal UniTask<string> PostJson<TRequest>(string path, TRequest request, CancellationToken cancellationToken = default) =>
            Execute(HttpVerb.Post, path, JsonConvert.SerializeObject(request), cancellationToken);

        public async UniTask<TResponse> Post<TRequest, TResponse>(string path, TRequest request,
            CancellationToken cancellationToken = default, Func<TResponse, bool> isValid = null)
        {
            var body = await Execute(HttpVerb.Post, path, JsonConvert.SerializeObject(request), cancellationToken);
            return Deserialize(body, isValid, Describe(HttpVerb.Post, path));
        }

        /// <summary>응답 본문이 없는 요청 (2xx만 오면 성공)</summary>
        public async UniTask Post<TRequest>(string path, TRequest request, CancellationToken cancellationToken = default)
        {
            await Execute(HttpVerb.Post, path, JsonConvert.SerializeObject(request), cancellationToken);
        }

        /// <summary>계정 삭제 계약은 204다. 다른 2xx도 완료로 간주하지 않는다.</summary>
        public async UniTask Delete(string path, CancellationToken cancellationToken = default)
        {
            await Execute(HttpVerb.Delete, path, null, cancellationToken, expectedStatusCode: 204);
        }

        private async UniTask<string> Execute(HttpVerb verb, string path, string jsonBody, CancellationToken cancellationToken,
            int? expectedStatusCode = null)
        {
            long generation = _tokens?.Generation ?? 0;
            string sentAccessToken = _tokens?.AccessToken;
            var response = await Send(verb, path, jsonBody, cancellationToken);

            var error = HttpErrorMapper.Map(response, _errorCodeReader);
            if (error == null)
            {
                ValidateStatus(verb, path, response, expectedStatusCode);
                return response.Body;
            }

            if (!ShouldRefresh(path, response, sentAccessToken))
            {
                throw error;
            }

            if (!await RefreshAsync(generation, sentAccessToken, cancellationToken))
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

            ValidateStatus(verb, path, response, expectedStatusCode);
            return response.Body;
        }

        private static void ValidateStatus(HttpVerb verb, string path, HttpResponseData response, int? expectedStatusCode)
        {
            if (expectedStatusCode.HasValue && response.StatusCode != expectedStatusCode.Value)
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse,
                    $"{Describe(verb, path)} status {response.StatusCode}");
            }
        }

        private UniTask<HttpResponseData> Send(HttpVerb verb, string path, string jsonBody, CancellationToken cancellationToken)
        {
            var url = $"{_config.BaseUrl}/{path.TrimStart('/')}";
            var headers = GameClientIdentity.Headers();
            foreach (var header in _auth.GetHeaders()) { headers[header.Key] = header.Value; }
            return _transport.Send(new HttpRequestData(verb, url, headers, jsonBody), _config.TimeoutSeconds, cancellationToken);
        }

        // 로그인한 상태로 보낸 요청이 401을 받았을 때만 토큰 만료로 본다. 로그인·재발급 요청의 401은 자격 증명이 틀린 것이다
        private bool ShouldRefresh(string path, HttpResponseData response, string sentAccessToken) =>
            _tokens != null
            && response.StatusCode == 401
            && !string.IsNullOrEmpty(sentAccessToken)
            && (!path.TrimStart('/').StartsWith(AuthPathPrefix, StringComparison.Ordinal)
                || path.TrimStart('/') == "api/auth/social/link"
                || path.TrimStart('/') == "api/auth/me");

        /// <summary>새 토큰을 받았거나 다른 요청이 이미 받아 둔 상태면 true. 리프레시 토큰이 거절돼 로그인이 필요하면 false(토큰을 비운다).
        /// 전송 실패는 토큰이 이미 소비됐을 수 있으므로 세션을 끝내고 같은 refresh 토큰을 재사용하지 않는다</summary>
        private async UniTask<bool> RefreshAsync(long generation, string sentAccessToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tokens.Generation != generation) { return false; }
            // 이 요청을 보낸 뒤 다른 요청이 이미 새 토큰을 받았으면 그 토큰으로 다시 보내면 된다
            if (!string.IsNullOrEmpty(_tokens.AccessToken) && _tokens.AccessToken != sentAccessToken)
            {
                return true;
            }

            var refreshing = _refreshing;
            if (refreshing == null || _refreshingGeneration != generation)
            {
                string refreshToken = _tokens.RefreshToken;
                if (string.IsNullOrEmpty(refreshToken))
                {
                    _tokens.TryClear(generation);
                    return false;
                }

                refreshing = _refreshing = new UniTaskCompletionSource<RefreshOutcome>();
                _refreshingGeneration = generation;
                CompleteRefreshAsync(refreshing, generation, refreshToken).Forget();
            }

            // 개별 요청의 취소는 기다림만 끝낸다. 재발급 전송까지 취소하면 서버가 이미 회전시킨 토큰을
            // 다른 요청이 다시 사용할 수 있어, 공유 전송은 HttpApiConfig의 제한 시간 안에서 끝까지 받는다.
            var shared = await refreshing.Task.AttachExternalCancellation(cancellationToken);
            if (_tokens.Generation != generation) { return false; }
            if (shared.Error != null)
            {
                throw shared.Error;
            }

            return shared.Refreshed;
        }

        private async UniTask CompleteRefreshAsync(UniTaskCompletionSource<RefreshOutcome> refreshing, long generation, string refreshToken)
        {
            var outcome = new RefreshOutcome();
            try
            {
                outcome.Refreshed = await RequestNewTokensAsync(generation, refreshToken, CancellationToken.None);
            }
            catch (Exception ex)
            {
                outcome.Error = ex;
            }
            finally
            {
                if (ReferenceEquals(_refreshing, refreshing)) { _refreshing = null; }
                refreshing.TrySetResult(outcome);
            }
        }

        private async UniTask<bool> RequestNewTokensAsync(long generation, string refreshToken, CancellationToken cancellationToken)
        {
            // 재발급 요청에는 만료된 액세스 토큰을 붙이지 않는다
            var url = $"{_config.BaseUrl}/{RefreshPath}";
            var body = JsonConvert.SerializeObject(new RefreshRequest { refreshToken = refreshToken });
            var response = await _transport.Send(new HttpRequestData(HttpVerb.Post, url, GameClientIdentity.Headers(), body),
                _config.TimeoutSeconds, cancellationToken);

            if (_tokens.Generation != generation) { return false; }
            ApiException error = HttpErrorMapper.Map(response, _errorCodeReader);
            if (error != null)
            {
                if (error.Code is "MAINTENANCE" or "DRAINING" or "CLIENT_UPDATE_REQUIRED") { throw error; }
                // 재발급 거절뿐 아니라 응답 유실·시간 초과·서버 오류도 토큰이 소비됐을 수 있다.
                // 서버의 재발급은 멱등하지 않으므로 같은 토큰을 다시 보내지 않고 로그인부터 시작한다.
                _tokens.TryClear(generation);
                return false;
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
                _tokens.TryClear(generation);
                return false;
            }

            return _tokens.TryRefresh(generation, tokens.accessToken, tokens.refreshToken);
        }

        /// <summary>진단 로그용 요청 식별자. 쿼리 문자열은 값이 섞일 수 있어 뺀다</summary>
        private static string Describe(HttpVerb verb, string path)
        {
            int query = path?.IndexOf('?') ?? -1;
            return $"{verb.ToString().ToUpperInvariant()} {(query >= 0 ? path.Substring(0, query) : path)}";
        }

        private static T Deserialize<T>(string json, Func<T, bool> isValid, string request)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse, $"{request} empty body");
            }

            T value;
            try
            {
                value = JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException)
            {
                // 성공 응답인데 형식이 계약과 다르면 다시 보내도 같은 결과일 가능성이 크다
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse, $"{request} json format");
            }

            if (value == null || (isValid != null && !isValid(value)))
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse,
                    value == null ? $"{request} null body" : $"{request} isValid rule");
            }

            return value;
        }
    }
}
