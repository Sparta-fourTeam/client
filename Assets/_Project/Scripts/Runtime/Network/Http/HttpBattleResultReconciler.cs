using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    public enum BattleSubmissionStatus
    {
        NotSubmitted,
        Acknowledged,
        Unknown,
        Rejected,
    }

    /// <summary>제출 응답과 나중에 관측한 서버 상태는 별개다. ObservedState는 이 전투의 보상 영수증이 아니다.</summary>
    public sealed class BattleReconciliationResult
    {
        public BattleSubmissionStatus SubmissionStatus { get; }
        public string SubmissionErrorCode { get; }
        public ServerUserData ObservedState { get; }
        public BattleEndReceipt Receipt { get; }

        internal BattleReconciliationResult(BattleSubmissionStatus status, string errorCode, ServerUserData state, BattleEndReceipt receipt)
        {
            SubmissionStatus = status;
            SubmissionErrorCode = errorCode;
            ObservedState = state;
            Receipt = receipt;
        }
    }

    /// <summary>서버 #73의 동일 POST 재시도와 후속 현재 상태 조회를 연결한다.
    /// 미확정 제출은 같은 요청을 재전송하고, 영수증 확인 후에는 GET만 재시도한다.
    /// Local 저장·프로필 적용·DI 전환·앱 재시작 원장은 다루지 않는다.</summary>
    public sealed class HttpBattleResultReconciler
    {
        private readonly HttpApiClient _client;
        private readonly HttpBattleSessionApi _battles;
        private readonly IAuthTokenStore _tokens;
        private readonly BattleEndRequest _request;
        private readonly long _generation;
        private bool _attempted;
        private int _busy;
        private string _submissionError;
        private BattleEndReceipt _receipt;

        public BattleSubmissionStatus SubmissionStatus { get; private set; }

        public HttpBattleResultReconciler(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens,
            BattleEndRequest request)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _generation = tokens.Generation;
            EnsureSession();
            _client = new HttpApiClient(config, new SessionBoundTransport(transport, EnsureSession), tokens: tokens);
            _battles = new HttpBattleSessionApi(_client);
        }

        /// <summary>미확정 상태는 동일 end 요청으로 복원한다. 확정 후 재호출은 현재 상태만 읽는다.</summary>
        public async UniTask<BattleReconciliationResult> Submit(CancellationToken cancellationToken = default)
        {
            Enter();
            try
            {
                EnsureSession();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_attempted || SubmissionStatus == BattleSubmissionStatus.Unknown)
                {
                    bool previouslyUnknown = _attempted;
                    _attempted = true;
                    // 전송 중 취소나 예상 밖 예외도 서버에서 적용됐을 가능성을 보존한다.
                    SubmissionStatus = BattleSubmissionStatus.Unknown;
                    try
                    {
                        _receipt = await _battles.EndBattle(_request, cancellationToken);
                        EnsureSession();
                        SubmissionStatus = BattleSubmissionStatus.Acknowledged;
                        _submissionError = null;
                    }
                    catch (ApiException ex)
                    {
                        _submissionError = ex.Code;
                        SubmissionStatus = !previouslyUnknown && ex.Kind == ApiErrorKind.Rejected
                            && ex.Code != "HTTP_404" && ex.Code != HttpErrorMapper.InvalidResponse
                            ? BattleSubmissionStatus.Rejected : BattleSubmissionStatus.Unknown;
                    }
                }

                return await ReadState(cancellationToken);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        /// <summary>응답 유실·조회 실패 후 GET만 재시도한다. 조회 성공이나 잔액 변화로 Unknown을 승격하지 않는다.</summary>
        public async UniTask<BattleReconciliationResult> RefreshState(CancellationToken cancellationToken = default)
        {
            Enter();
            try
            {
                if (!_attempted) { throw new InvalidOperationException("제출 전에는 결과 조정을 시작할 수 없습니다"); }
                return await ReadState(cancellationToken);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private async UniTask<BattleReconciliationResult> ReadState(CancellationToken cancellationToken)
        {
            EnsureSession();
            var body = await _client.GetJson("api/user/me", cancellationToken);
            EnsureSession();
            var state = ServerProgressContract.Parse(() => ServerProgressContract.ReadMe(body));
            return new BattleReconciliationResult(SubmissionStatus, _submissionError, state, _receipt);
        }

        private void EnsureSession()
        {
            if (_tokens.Generation != _generation || string.IsNullOrEmpty(_tokens.AccessToken))
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired);
            }
        }

        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                throw new InvalidOperationException("전투 결과 조정이 이미 진행 중입니다");
            }
        }

        // 인증 헤더를 만드는 도중 계정이 바뀐 경우에도 실제 전송 직전에 막는다. refresh 요청도 같은 경계를 지난다.
        private sealed class SessionBoundTransport : IHttpTransport
        {
            private readonly IHttpTransport _transport;
            private readonly Action _ensureSession;

            public SessionBoundTransport(IHttpTransport transport, Action ensureSession)
            {
                _transport = transport ?? throw new ArgumentNullException(nameof(transport));
                _ensureSession = ensureSession;
            }

            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            {
                _ensureSession();
                return _transport.Send(request, timeoutSeconds, cancellationToken);
            }
        }
    }
}
