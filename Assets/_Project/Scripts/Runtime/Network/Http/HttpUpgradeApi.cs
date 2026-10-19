using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Network
{
    /// <summary>#507의 서버 견적 검증을 재사용하고 계정별 동일 구매 키를 재시작 뒤에도 복구한다.</summary>
    public sealed class HttpUpgradeApi : IUpgradeApi, IUpgradeRecovery
    {
        private readonly HttpApiConfig _config;
        private readonly IHttpTransport _transport;
        private readonly IAuthTokenStore _tokens;
        private readonly HttpPlayerApi _player;
        private readonly PlayerProfile _profile;
        private readonly GameDataStore _data;
        private int _busy;

        private sealed class Pending
        { public string UpgradeId, CreateId; public int ExpectedLevel; }
        private sealed class PurchaseResponse
        { public string upgradeId; public int? level; }
        private sealed class CatalogRow
        {
            public string upgradeId, materialItemId;
            public int? maxLevel, effectiveUnlockLevel, costBase, costStep, materialBase, materialStep;
            public int?[] levelCosts, materialCosts;
        }

        public HttpUpgradeApi(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens,
            HttpPlayerApi player, PlayerProfile profile, GameDataStore data)
        { _config = config; _transport = transport; _tokens = tokens; _player = player; _profile = profile; _data = data; }

        public bool HasPendingPurchase => LoadPending() != null;
        public string PendingUpgradeId => LoadPending()?.UpgradeId;
        public UniTask<PlayerSnapshot> Purchase(string upgradeId, string idempotencyKey) => Execute(upgradeId, idempotencyKey, false);
        public UniTask<PlayerSnapshot> RecoverPurchase() => Execute(null, null, true);

        private async UniTask<PlayerSnapshot> Execute(string id, string key, bool recover)
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_REQUEST_BUSY"); }
            try
            {
                long generation = _tokens.Generation;
                Ensure(generation);
                var pending = LoadPending();
                if (pending == null)
                {
                    if (recover) { throw new InvalidOperationException("복구할 강화 구매가 없습니다"); }
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 255 || string.IsNullOrWhiteSpace(key) || key.Length > 255)
                    { throw new ArgumentException("강화 ID와 구매 키가 필요합니다"); }
                    await ValidateQuote(id, generation);
                    pending = new Pending { UpgradeId = id, CreateId = key, ExpectedLevel = _profile.UpgradeLevel(id) + 1 };
                    SavePending(pending);
                }
                else if (!recover && (pending.UpgradeId != id || pending.CreateId != key))
                { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_RECOVERY_REQUIRED"); }

                PurchaseResponse response;
                try
                {
                    response = await BoundClient(generation).Post<object, PurchaseResponse>("api/user/upgrades",
                        new { upgradeId = pending.UpgradeId, createId = pending.CreateId },
                        isValid: value => value.upgradeId == pending.UpgradeId && value.level >= pending.ExpectedLevel);
                }
                catch (ApiException error)
                {
                    if (HttpErrorMapper.IsDefinitiveRequestRejection(error))
                    { Ensure(generation); ClearPending(); }
                    throw;
                }
                Ensure(generation);
                var snapshot = await _player.GetMe();
                Ensure(generation);
                if ((snapshot.upgrades.Find(row => row.upgradeId == pending.UpgradeId)?.level ?? 0) < response.level)
                { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse); }
                ClearPending();
                return snapshot;
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        private async UniTask ValidateQuote(string id, long generation)
        {
            if (!_profile.IsLoaded || !_data.HasServerPlayerLevels)
            { throw new ApiException(ApiErrorKind.Rejected, "PLAYER_NOT_LOADED"); }
            var local = _data.Upgrades.GetOrThrow(id);
            var rows = await BoundClient(generation).Get<List<CatalogRow>>("api/upgrade");
            Ensure(generation);
            int level = _profile.UpgradeLevel(id);
            var matches = rows.Where(row => row?.upgradeId == id).ToList();
            if (matches.Count != 1 || level >= local.MaxLevel)
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_CATALOG_MISMATCH"); }
            var server = matches[0];
            if (!TryCost(server.levelCosts, server.costBase, server.costStep, level, server.maxLevel, out long gold)
                || !TryCost(server.materialCosts, server.materialBase, server.materialStep, level, server.maxLevel, out long material)
                || server.maxLevel != local.MaxLevel || server.effectiveUnlockLevel != _data.UnlockLevelOf(local)
                || (server.materialItemId ?? "") != (local.MaterialItemId ?? "") || gold != local.CostAt(level + 1)
                || (local.UsesMaterial && material != local.MaterialAt(level + 1)))
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_CATALOG_MISMATCH"); }
            if (_profile.Level < _data.UnlockLevelOf(local))
            { throw new ApiException(ApiErrorKind.Rejected, "LOCKED"); }
        }

        private static bool TryCost(int?[] costs, int? basis, int? step, int level, int? maxLevel, out long value)
        {
            value = 0;
            if (costs != null)
            {
                if (costs.Length != maxLevel || costs.Any(cost => !cost.HasValue || cost.Value <= 0)
                    || level < 0 || level >= costs.Length) { return false; }
                value = costs[level].Value;
                return true;
            }
            value = basis.HasValue ? (long)basis.Value + (long)(step ?? 0) * level : 0;
            return value >= 0 && value <= int.MaxValue;
        }

        private void Ensure(long generation)
        {
            if (_tokens.Generation != generation || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }
        private HttpApiClient BoundClient(long generation) => new(_config,
            new BoundTransport(_transport, () => Ensure(generation)), tokens: _tokens);
        private sealed class BoundTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;
            private readonly Action _ensure;
            public BoundTransport(IHttpTransport inner, Action ensure) { _inner = inner; _ensure = ensure; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { _ensure(); return _inner.Send(request, timeoutSeconds, cancellationToken); }
        }

        private string PendingKey => "server.upgrade.pending." + ServerAccountIdentity.Subject(_tokens.AccessToken);
        private Pending LoadPending()
        {
            if (string.IsNullOrEmpty(_tokens.AccessToken)) { return null; }
            string json = PlayerPrefs.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json)) { return null; }
            try
            {
                var pending = JsonConvert.DeserializeObject<Pending>(json);
                if (pending == null || string.IsNullOrWhiteSpace(pending.UpgradeId)
                    || string.IsNullOrWhiteSpace(pending.CreateId) || pending.ExpectedLevel < 1)
                { throw new FormatException(); }
                return pending;
            }
            catch (Exception error) when (error is JsonException or FormatException)
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_PENDING_INVALID"); }
        }
        private void SavePending(Pending pending)
        { PlayerPrefs.SetString(PendingKey, JsonConvert.SerializeObject(pending)); PlayerPrefs.Save(); }
        private void ClearPending()
        { PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); }
    }
}
