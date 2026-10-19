using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>서버 구매·현재 상태 조회. 보류 거래는 같은 키를 유지하며 완료 후에도 현재 잔액과 한도를 따로 읽는다.</summary>
    public sealed class HttpEnergyApi : IEnergyPurchaseApi
    {
        private readonly HttpApiClient _client;
        private readonly IAuthTokenStore _tokens;
        private readonly HttpPlayerApi _player;
        private long _generation, _stateGeneration = -1;
        private int _busy;
        private EnergyPurchaseState _state;
        private Pending _pending;
        private sealed class Pending
        {
            public string Key;
            public long Generation;
            public bool Acknowledged, Ambiguous;
        }
        private sealed class StateResponse
        {
            public string serverTime, purchaseDay, resetsAt;
            public int? purchaseCount, remainingPurchases, gemCost, energyGranted, dailyLimit;
        }
        private sealed class Receipt
        {
            public string createId, source, purchaseDay, completedAt;
            public int? energyGranted, gemsSpent, purchaseCount;
        }

        public HttpEnergyApi(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens, HttpPlayerApi player)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _client = new HttpApiClient(config, new SessionTransport(transport, EnsureSession), tokens: tokens);
        }
        public long SessionGeneration => _tokens.Generation;
        public EnergyPurchaseState PurchaseState => _stateGeneration == SessionGeneration
            && !string.IsNullOrEmpty(_tokens.AccessToken) ? _state : null;
        public bool HasPendingPurchase => !string.IsNullOrEmpty(_tokens.AccessToken)
            && ((_pending != null && _pending.Generation == SessionGeneration) || PlayerPrefs.HasKey(PendingKey));

        public async UniTask<PlayerSnapshot> RefreshPurchaseState()
        {
            Enter();
            try { return await ReadCurrent(); }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        public async UniTask<PlayerSnapshot> Recover(EnergySource source, string idempotencyKey = null)
        {
            if (source != EnergySource.Purchase) { throw new ApiException(ApiErrorKind.Rejected, "AD_NOT_READY"); }
            Enter();
            try
            {
                if (idempotencyKey != null && (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128))
                { throw new ArgumentException("구매 키는 1~128자여야 합니다"); }
                if (_pending == null)
                {
                    _pending = new Pending { Key = idempotencyKey ?? Guid.NewGuid().ToString(), Generation = _generation };
                    SavePending();
                }
                else if (idempotencyKey != null && _pending.Key != idempotencyKey)
                { throw new ApiException(ApiErrorKind.Rejected, "ENERGY_RECOVERY_REQUIRED"); }
                _state = null;
                if (!_pending.Acknowledged)
                {
                    try
                    {
                        await _client.Post<object, Receipt>("api/user/energy/recover",
                            new { createId = _pending.Key, source = "Purchase" }, isValid: ValidReceipt);
                        EnsureSession();
                        _pending.Acknowledged = true;
                        SavePending();
                    }
                    catch (ApiException error)
                    {
                        bool definite = HttpErrorMapper.IsDefinitiveRequestRejection(error);
                        if (definite) { EnsureSession(); ClearPending(); }
                        else if (!definite) { _pending.Ambiguous = true; SavePending(); }
                        throw;
                    }
                }
                var snapshot = await ReadCurrent();
                ClearPending();
                return snapshot;
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        private async UniTask<PlayerSnapshot> ReadCurrent()
        {
            _state = null;
            var snapshot = await _player.GetMe();
            EnsureSession();
            if (!TryUtc(snapshot.energyUpdatedAt, out _))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse); }
            var state = await _client.Get<StateResponse>("api/user/energy/purchase-state", isValid: ValidState);
            EnsureSession();
            TryUtc(state.serverTime, out var serverTime);
            TryUtc(state.resetsAt, out var reset);
            TryDay(state.purchaseDay, out var day);
            _state = new EnergyPurchaseState(day, serverTime, reset, state.purchaseCount.Value,
                state.remainingPurchases.Value, state.gemCost.Value, state.energyGranted.Value, state.dailyLimit.Value);
            _stateGeneration = _generation;
            return snapshot;
        }

        private bool ValidReceipt(Receipt value) => value.createId == _pending.Key && value.source == "Purchase"
            && value.energyGranted > 0 && value.gemsSpent > 0 && value.purchaseCount > 0
            && TryDay(value.purchaseDay, out _) && TryUtc(value.completedAt, out _);

        private static bool ValidState(StateResponse value)
        {
            if (!TryUtc(value.serverTime, out var now) || !TryUtc(value.resetsAt, out var reset)
                || !TryDay(value.purchaseDay, out var day)) { return false; }
            return value.dailyLimit > 0 && value.gemCost > 0 && value.energyGranted > 0
                && value.purchaseCount >= 0 && value.purchaseCount <= value.dailyLimit
                && value.remainingPurchases == value.dailyLimit - value.purchaseCount
                && day == GameDay.Date(now) && reset == GameDay.NextResetUtc(now)
                && reset > now;
        }

        private static bool TryDay(string value, out DateTime day) => DateTime.TryParseExact(value,
            "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        private static bool TryUtc(string value, out DateTime time)
        {
            time = default;
            return value != null && value.EndsWith("Z", StringComparison.Ordinal) && DateTime.TryParse(value,
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out time);
        }
        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            { throw new ApiException(ApiErrorKind.Rejected, "ENERGY_REQUEST_BUSY"); }
            _generation = SessionGeneration;
            if (_pending != null && _pending.Generation != _generation) { _pending = null; }
            try { EnsureSession(); }
            catch { Interlocked.Exchange(ref _busy, 0); throw; }
            _pending ??= LoadPending();
        }
        private string PendingKey => "server.energy.pending." + ServerAccountIdentity.Subject(_tokens.AccessToken);
        private Pending LoadPending()
        {
            string key = PlayerPrefs.GetString(PendingKey, string.Empty);
            return string.IsNullOrEmpty(key) ? null : new Pending { Key = key, Generation = _generation, Ambiguous = true };
        }
        private void SavePending()
        { PlayerPrefs.SetString(PendingKey, _pending.Key); PlayerPrefs.Save(); }
        private void ClearPending()
        { PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); _pending = null; }
        private void EnsureSession()
        {
            if (_generation != SessionGeneration || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }
        private sealed class SessionTransport : IHttpTransport
        {
            private readonly IHttpTransport _transport;
            private readonly Action _check;
            public SessionTransport(IHttpTransport transport, Action check) { _transport = transport; _check = check; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { _check(); return _transport.Send(request, timeoutSeconds, cancellationToken); }
        }
    }
}
