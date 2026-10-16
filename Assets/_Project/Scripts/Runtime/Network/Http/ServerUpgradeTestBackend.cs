using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>명시적으로 켠 개발 모드 전용. 서버 조회·단건 강화 외 모든 Local 상태 변경과 체크포인트 접근을 막는다.
    /// pending은 메모리 수명이며 앱 종료 후 복구 원장이 아니다. 서버 영수증/revision이나 카탈로그 일치를 보장하지 않는다.</summary>
    public sealed class ServerUpgradeTestBackend : IPlayerApi, IUpgradeApi, IUpgradeRecovery, IBattleApi, IEnergyApi,
        IStageApi, ICodexApi, IQuestApi, IShopApi, ITutorialApi, IAccountApi, IBattleCheckpointStore, ISubmitFaultSwitch
    {
        public const string Disabled = "SERVER_TEST_DISABLED";
        private readonly HttpApiClient _client;
        private readonly IAuthTokenStore _tokens;
        private readonly GameDataStore _data;
        private PlayerSnapshot _observed;
        private long _observedGeneration = -1;
        private long _operationGeneration;
        private int _busy;
        private PendingPurchase _pending;
        private sealed class PendingPurchase
        {
            public readonly string upgradeId, createId;
            public readonly long Generation;
            public int? ResultLevel;
            public bool HasAmbiguousAttempt;
            public PendingPurchase(string id, string key, long generation)
            { upgradeId = id; createId = key; Generation = generation; }
        }
        private sealed class PurchaseResponse { public string upgradeId; public int? level; }
        private sealed class CatalogRow
        {
            public string upgradeId, materialItemId;
            public int? maxLevel, unlockLevel, costBase, costStep, materialBase, materialStep;
        }

        public ServerUpgradeTestBackend(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens, GameDataStore data)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _client = new HttpApiClient(config, new BoundTransport(transport, EnsureSession), tokens: tokens);
        }

        public bool HasPendingPurchase => _pending != null && _pending.Generation == _tokens.Generation;
        public string PendingUpgradeId => HasPendingPurchase ? _pending.upgradeId : null;
        public int FailNextSubmits { get; set; }

        public async UniTask<PlayerSnapshot> GetMe()
        {
            Enter();
            try { return await ReadState(); }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        public UniTask<PlayerSnapshot> Purchase(string upgradeId, string idempotencyKey) =>
            PurchaseOrRecover(upgradeId, idempotencyKey, false);

        public UniTask<PlayerSnapshot> RecoverPurchase() => PurchaseOrRecover(null, null, true);

        private async UniTask<PlayerSnapshot> PurchaseOrRecover(string id, string key, bool recover)
        {
            Enter();
            try
            {
                if (recover && _pending == null) { throw new InvalidOperationException("복구할 강화 요청이 없습니다"); }
                if (_pending == null)
                {
                    if (string.IsNullOrWhiteSpace(id) || id.Length > 255 || string.IsNullOrWhiteSpace(key) || key.Length > 255)
                    { throw new ArgumentException("강화 ID와 구매 키가 필요합니다"); }
                    await ValidateQuote(id);
                    _pending = new PendingPurchase(id, key, _operationGeneration);
                }
                else if (!recover && (id != _pending.upgradeId || key != _pending.createId))
                { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_RECOVERY_REQUIRED"); }

                if (!_pending.ResultLevel.HasValue)
                {
                    try
                    {
                        var response = await _client.Post<object, PurchaseResponse>("api/user/upgrades",
                            new { upgradeId = _pending.upgradeId, createId = _pending.createId },
                            isValid: value => value.upgradeId == _pending.upgradeId && value.level >= 1);
                        EnsureSession();
                        _pending.ResultLevel = response.level;
                    }
                    catch (ApiException error)
                    {
                        // 409는 처리 중 중복과 재화 부족을 구분할 수 없다. 5xx/유실/잘못된 성공 응답도 같은 키로만 복구한다.
                        bool definitive = error.Kind == ApiErrorKind.Rejected
                            && error.Code != null && error.Code.StartsWith("HTTP_", StringComparison.Ordinal)
                            && int.TryParse(error.Code.Substring(5), out int status) && status >= 400 && status < 500 && status != 409;
                        // 이번 거절은 이전 시도에서 이미 적용됐을 가능성까지 취소하지 않는다.
                        if (definitive && !_pending.HasAmbiguousAttempt) { _pending = null; }
                        else if (!definitive) { _pending.HasAmbiguousAttempt = true; }
                        throw;
                    }
                }

                var snapshot = await ReadState();
                if ((snapshot.upgrades.Find(row => row.upgradeId == _pending.upgradeId)?.level ?? 0) < _pending.ResultLevel.Value)
                { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse); }
                _pending = null;
                return snapshot;
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        private async UniTask<PlayerSnapshot> ReadState()
        {
            EnsureSession();
            var body = await _client.GetJson("api/user/me");
            EnsureSession();
            // Local 표시 정보도 읽지 않는다. 서버에 없는 영역은 이 모드에서 사용할 수 없다.
            _observed = ServerUserMapper.ToSnapshot(ServerUserStateReader.Parse(body), "서버 강화 테스트");
            _observedGeneration = _operationGeneration;
            return _observed;
        }

        private async UniTask ValidateQuote(string id)
        {
            if (_observed == null || _observedGeneration != _operationGeneration)
            { throw new ApiException(ApiErrorKind.Rejected, "PLAYER_NOT_LOADED"); }
            var local = _data.Upgrades.GetOrThrow(id);
            var rows = await _client.Get<List<CatalogRow>>("api/upgrade");
            EnsureSession();
            var matches = rows.Where(row => row != null && row.upgradeId == id).ToList();
            int level = _observed.upgrades.Find(row => row.upgradeId == id)?.level ?? 0;
            if (matches.Count != 1 || level >= local.MaxLevel)
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_CATALOG_MISMATCH"); }
            var server = matches[0];
            long coin = server.costBase.HasValue ? (long)server.costBase + (long)(server.costStep ?? 0) * level : 0;
            long material = server.materialBase.HasValue ? (long)server.materialBase + (long)(server.materialStep ?? 0) * level : 0;
            if (server.maxLevel != local.MaxLevel || server.unlockLevel != _data.UnlockLevelOf(local)
                || (server.materialItemId ?? "") != (local.MaterialItemId ?? "") || coin != local.CostAt(level + 1)
                || (local.UsesMaterial && material != local.MaterialAt(level + 1)))
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_CATALOG_MISMATCH"); }
            if (_data.PlayerLevels.At(_observed.exp).level < _data.UnlockLevelOf(local))
            { throw new ApiException(ApiErrorKind.Rejected, "LOCKED"); }
        }

        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            { throw new InvalidOperationException("서버 상태 요청이 이미 진행 중입니다"); }
            _operationGeneration = _tokens.Generation;
            if (_pending != null && _pending.Generation != _operationGeneration) { _pending = null; }
            try { EnsureSession(); }
            catch { Interlocked.Exchange(ref _busy, 0); throw; }
        }

        private void EnsureSession()
        {
            if (_tokens.Generation != _operationGeneration || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }

        private sealed class BoundTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;
            private readonly Action _ensure;
            public BoundTransport(IHttpTransport inner, Action ensure) { _inner = inner; _ensure = ensure; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { _ensure(); return _inner.Send(request, timeoutSeconds, cancellationToken); }
        }

        private static ApiException Blocked() => new(ApiErrorKind.Rejected, Disabled);
        public UniTask<StartBattleResponse> StartBattle(int stageId) => throw Blocked();
        public UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest request) => throw Blocked();
        public UniTask<PlayerSnapshot> Recover(EnergySource source, string idempotencyKey = null) => throw Blocked();
        public UniTask<PlayerSnapshot> ClaimRatingReward(int stageId) => throw Blocked();
        public UniTask<PlayerSnapshot> ClaimMonsterReward(int monsterId) => throw Blocked();
        public UniTask<PlayerSnapshot> ClaimQuest(string questId) => throw Blocked();
        public UniTask<PlayerSnapshot> ClaimQuestChest(string chestId) => throw Blocked();
        public UniTask<PlayerSnapshot> Reroll(ShopRerollKind kind, string idempotencyKey) => throw Blocked();
        public UniTask<PlayerSnapshot> Buy(int slot, string idempotencyKey) => throw Blocked();
        public UniTask<PlayerSnapshot> BuyGoldPack(int index, string idempotencyKey) => throw Blocked();
        public UniTask<PlayerSnapshot> BuyGemPack(int index, string idempotencyKey) => throw Blocked();
        public UniTask<TutorialCompleteResponse> Complete(string tutorialId) => throw Blocked();
        public UniTask DeleteAccount() => throw Blocked();
        public UniTask<string> ChangeNickname(string nickname) => throw Blocked();
        public UniTask LinkAccount(LoginMethod method, string accountId) => throw Blocked();
        public BattleCheckpoint Load() => null;
        public void Save(BattleCheckpoint checkpoint) => throw Blocked();
        public void QueueSave(BattleCheckpoint checkpoint) => throw Blocked();
        public void Flush() { }
        public void Clear() { } // 기존 Local 체크포인트 파일도 지우지 않는다.
    }
}
