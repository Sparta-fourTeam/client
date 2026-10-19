using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    public enum ShopSubmissionStatus { NotSubmitted, Acknowledged, Unknown, Rejected }

    /// <summary>불변 영수증과 현재 계정 상태를 분리한다. 현재 잔액만으로 구매 성공을 추정하지 않는다.</summary>
    public sealed class ShopReconciliationResult
    {
        public ShopSubmissionStatus Status { get; }
        public string ErrorCode { get; }
        public ServerDailyShopReceipt DailyReceipt { get; }
        public ServerGoldPackReceipt GoldPackReceipt { get; }
        public ServerShopAccount Account { get; }

        internal ShopReconciliationResult(ShopSubmissionStatus status, string errorCode,
            ServerDailyShopReceipt daily, ServerGoldPackReceipt pack, ServerShopAccount account)
        { Status = status; ErrorCode = errorCode; DailyReceipt = daily; GoldPackReceipt = pack; Account = account; }
    }

    /// <summary>한 구매·재추첨의 요청을 고정한다. 미확정은 같은 POST로, 영수증 확인 후 조회 실패는 GET만 재시도한다.</summary>
    public sealed class HttpShopExchange
    {
        private readonly HttpShopApi _api;
        private readonly ShopExchangeRequest _request;
        private readonly ShopPendingRequestStore _pending;
        private bool _attempted;
        private int _busy;
        private string _error;
        private ServerDailyShopReceipt _daily;
        private ServerGoldPackReceipt _pack;

        public ShopSubmissionStatus Status { get; private set; }

        public HttpShopExchange(HttpShopApi api, ShopExchangeRequest request)
            : this(api, request, null, false) { }

        internal HttpShopExchange(HttpShopApi api, ShopExchangeRequest request, ShopPendingRequestStore pending, bool recovered)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _pending = pending;
            _attempted = recovered;
            Status = recovered ? ShopSubmissionStatus.Unknown : ShopSubmissionStatus.NotSubmitted;
            _api.EnsureSession();
        }

        public async UniTask<ShopReconciliationResult> Submit(CancellationToken cancellationToken = default)
        {
            Enter();
            try
            {
                _api.EnsureSession();
                cancellationToken.ThrowIfCancellationRequested();
                if (!_attempted || Status == ShopSubmissionStatus.Unknown)
                {
                    _pending?.Prepare(_request);
                    bool previouslyUnknown = _attempted;
                    _attempted = true;
                    Status = ShopSubmissionStatus.Unknown;
                    try
                    {
                        (_daily, _pack) = await _api.Post(_request, cancellationToken);
                        _api.EnsureSession();
                        Status = ShopSubmissionStatus.Acknowledged;
                        _error = null;
                        _pending?.Complete(_request);
                    }
                    catch (ApiException error)
                    {
                        _error = error.Code;
                        Status = !previouslyUnknown && error.Kind == ApiErrorKind.Rejected
                            && error.Code != "HTTP_404" && error.Code != HttpErrorMapper.InvalidResponse
                            && error.Code != HttpErrorMapper.SessionExpired
                            ? ShopSubmissionStatus.Rejected : ShopSubmissionStatus.Unknown;
                        if (Status == ShopSubmissionStatus.Rejected) { _pending?.Complete(_request); }
                    }
                }
                return await ReadAccount(cancellationToken);
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        /// <summary>상태 조회만 다시 수행한다. 미확정 POST의 성공 여부를 현재 지갑으로 추측하지 않는다.</summary>
        public async UniTask<ShopReconciliationResult> RefreshAccount(CancellationToken cancellationToken = default)
        {
            Enter();
            try
            {
                if (!_attempted) { throw new InvalidOperationException("제출 전에는 계정 조회를 재시도할 수 없습니다"); }
                return await ReadAccount(cancellationToken);
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        private async UniTask<ShopReconciliationResult> ReadAccount(CancellationToken cancellationToken)
        {
            var account = await _api.GetAccount(cancellationToken);
            return new ShopReconciliationResult(Status, _error, _daily, _pack, account);
        }

        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            { throw new InvalidOperationException("같은 상점 요청이 이미 진행 중입니다"); }
        }
    }
}
