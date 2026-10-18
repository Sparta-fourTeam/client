using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    public enum ProgressSubmissionStatus { NotSubmitted, Acknowledged, Unknown, Rejected }

    /// <summary>수령 확인과 현재 상태 관측은 별개다. 조회 결과나 잔액 변화만으로 Unknown을 성공으로 바꾸지 않는다.</summary>
    public sealed class ProgressReconciliationResult<TReceipt>
    {
        public ProgressSubmissionStatus Status { get; }
        public string ErrorCode { get; }
        public TReceipt Receipt { get; }
        public ServerUserData ObservedState { get; }

        internal ProgressReconciliationResult(ProgressSubmissionStatus status, string error, TReceipt receipt, ServerUserData state)
        { Status = status; ErrorCode = error; Receipt = receipt; ObservedState = state; }
    }

    /// <summary>처음 요청·계정을 고정한 튜토리얼·퀘스트 처리. 영수증 확인 뒤에는 GET만 재시도한다.</summary>
    public sealed class HttpProgressOperation<TReceipt> where TReceipt : class
    {
        private readonly IAuthTokenStore _tokens;
        private readonly long _generation;
        private readonly HttpApiClient _client;
        private readonly Func<HttpApiClient, CancellationToken, UniTask<TReceipt>> _send;
        private TReceipt _receipt;
        private string _error;
        private int _busy;
        public ProgressSubmissionStatus Status { get; private set; }

        internal HttpProgressOperation(HttpApiClient client, IAuthTokenStore tokens, long generation,
            Func<HttpApiClient, CancellationToken, UniTask<TReceipt>> send)
        {
            _tokens = tokens;
            _generation = generation;
            _send = send;
            _client = client;
            EnsureSession();
        }

        /// <summary>실패·유실 시 같은 요청을 재전송한다. 과거 기간 재시도에도 처음 기간키를 유지한다.</summary>
        public async UniTask<ProgressReconciliationResult<TReceipt>> Submit(CancellationToken cancellationToken = default)
        {
            Enter();
            try
            {
                EnsureSession();
                cancellationToken.ThrowIfCancellationRequested();
                if (Status == ProgressSubmissionStatus.NotSubmitted || Status == ProgressSubmissionStatus.Unknown)
                {
                    bool previouslyUnknown = Status == ProgressSubmissionStatus.Unknown;
                    Status = ProgressSubmissionStatus.Unknown;
                    try
                    {
                        _receipt = await _send(_client, cancellationToken);
                        EnsureSession();
                        Status = ProgressSubmissionStatus.Acknowledged;
                        _error = null;
                    }
                    catch (ApiException ex)
                    {
                        _error = ex.Code;
                        Status = !previouslyUnknown && ex.Kind == ApiErrorKind.Rejected
                            && ex.Code != HttpErrorMapper.InvalidResponse && ex.Code != "HTTP_404"
                            && ex.Code != HttpErrorMapper.SessionExpired
                            ? ProgressSubmissionStatus.Rejected : ProgressSubmissionStatus.Unknown;
                    }
                }
                return await ReadState(cancellationToken);
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        /// <summary>현재 상태만 조회한다. 영수증을 받지 못한 수령은 여전히 Unknown이다.</summary>
        public async UniTask<ProgressReconciliationResult<TReceipt>> RefreshState(CancellationToken cancellationToken = default)
        {
            Enter();
            try
            {
                if (Status == ProgressSubmissionStatus.NotSubmitted)
                { throw new InvalidOperationException("제출 전에는 상태 조정을 시작할 수 없습니다"); }
                return await ReadState(cancellationToken);
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        private async UniTask<ProgressReconciliationResult<TReceipt>> ReadState(CancellationToken cancellationToken)
        {
            EnsureSession();
            var json = await _client.GetJson("api/user/me", cancellationToken);
            EnsureSession();
            var state = ServerProgressContract.Parse(() => ServerProgressContract.ReadMe(json));
            EnsureSession();
            return new ProgressReconciliationResult<TReceipt>(Status, _error, _receipt, state);
        }

        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            { throw new InvalidOperationException("같은 수령 요청이 이미 처리 중입니다"); }
        }

        private void EnsureSession()
        {
            if (_tokens.Generation != _generation || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }

    }

    /// <summary>서버의 자연 멱등 키를 가진 작업만 만든다. 앱 재시작 원장과 Root DI 전환은 포함하지 않는다.</summary>
    public sealed class HttpProgressReconciler
    {
        private readonly HttpApiClient _client;
        private readonly IAuthTokenStore _tokens;
        private readonly long _generation;

        /// <summary>계정 세션 하나에서 공유한다. 여러 수령이 동시에 401을 받아도 refresh는 한 번만 보낸다.</summary>
        public HttpProgressReconciler(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _generation = tokens.Generation;
            EnsureSession();
            _client = new HttpApiClient(config, new BoundTransport(transport, EnsureSession), tokens: tokens);
        }

        public HttpProgressOperation<TutorialCompletionReceipt> Tutorial(TutorialCompletionRequest request)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            return new(_client, _tokens, _generation, (client, ct) => new HttpTutorialApi(client).Complete(request, ct));
        }

        public HttpProgressOperation<QuestClaimReceipt> Quest(QuestClaimRequest request, bool chest = false)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }
            return new(_client, _tokens, _generation, (client, ct) => chest
                ? new HttpQuestApi(client).ClaimChest(request, ct) : new HttpQuestApi(client).ClaimQuest(request, ct));
        }

        private void EnsureSession()
        {
            if (_tokens.Generation != _generation || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }

        private sealed class BoundTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;
            private readonly Action _ensure;
            internal BoundTransport(IHttpTransport inner, Action ensure)
            { _inner = inner ?? throw new ArgumentNullException(nameof(inner)); _ensure = ensure; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { _ensure(); return _inner.Send(request, timeoutSeconds, cancellationToken); }
        }
    }
}
