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

        internal BattleReconciliationResult(BattleSubmissionStatus status, string errorCode, ServerUserData state)
        {
            SubmissionStatus = status;
            SubmissionErrorCode = errorCode;
            ObservedState = state;
        }
    }

    /// <summary>한 로그인 세대·한 전투 결과의 POST 1회와 후속 GET을 연결한다.
    /// 같은 인스턴스에서 재시도는 GET만 한다. Local 저장·보상 계산·프로필 적용·DI 전환은 하지 않는다.
    /// 앱 재시작 복구용 영구 원장이 아니며, Unknown을 확정하려면 서버의 battleId별 처리 결과 조회가 필요하다.</summary>
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

        /// <summary>최초 호출만 end를 보낸다. 재호출은 성공 응답 유실 여부와 관계없이 상태 조회만 한다.</summary>
        public async UniTask<BattleReconciliationResult> Submit(CancellationToken cancellationToken = default)
        {
            Enter();
            try
            {
                EnsureSession();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_attempted)
                {
                    _attempted = true;
                    // 전송 중 취소나 예상 밖 예외도 서버에서 적용됐을 가능성을 보존한다.
                    SubmissionStatus = BattleSubmissionStatus.Unknown;
                    try
                    {
                        await _battles.EndBattle(_request, cancellationToken);
                        SubmissionStatus = BattleSubmissionStatus.Acknowledged;
                    }
                    catch (ApiException ex)
                    {
                        _submissionError = ex.Code;
                        SubmissionStatus = ex.Kind == ApiErrorKind.Rejected && ex.Code != "HTTP_404"
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
            return new BattleReconciliationResult(SubmissionStatus, _submissionError, ServerUserStateReader.Parse(body));
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
