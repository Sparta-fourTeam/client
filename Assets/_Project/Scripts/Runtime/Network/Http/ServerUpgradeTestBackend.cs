using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>명시적으로 켠 개발 모드 전용. 서버 조회·단건 강화·별 보상 외 모든 Local 상태 변경과 체크포인트 접근을 막는다.
    /// pending은 메모리 수명이며 앱 종료 후 복구 원장이 아니다. 서버 영수증/revision이나 카탈로그 일치를 보장하지 않는다.</summary>
    public sealed class ServerUpgradeTestBackend : IPlayerApi, IUpgradeApi, IUpgradeRecovery, IStageRewardRecovery, IBattleApi, IEnergyApi,
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
        private PendingClaim _claim;
        private sealed class PendingClaim
        {
            public readonly int StageId, TargetRating;
            public readonly long Generation;
            public bool PostAttempted;
            public int AcknowledgedRating;
            public PendingClaim(int stageId, int targetRating, long generation)
            { StageId = stageId; TargetRating = targetRating; Generation = generation; }
        }
        private sealed class ClaimResponse { public int? stageId, clearRating, claimedRating; }
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
            public int? maxLevel, unlockLevel, effectiveUnlockLevel, costBase, costStep, materialBase, materialStep;
            public int?[] levelCosts, materialCosts;
        }

        private sealed class PlayerLevelsResponse
        {
            public int? baseExp, stepExp, maxLevel, version;
        }

        public ServerUpgradeTestBackend(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens, GameDataStore data)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _data.InvalidateServerPlayerLevels();
            _client = new HttpApiClient(config, new BoundTransport(transport, EnsureSession), tokens: tokens);
        }

        public bool HasPendingPurchase => _pending != null && _pending.Generation == _tokens.Generation;
        public string PendingUpgradeId => HasPendingPurchase ? _pending.upgradeId : null;
        public bool HasPendingStageReward => _claim != null && _claim.Generation == _tokens.Generation;
        public int PendingStageId => HasPendingStageReward ? _claim.StageId : 0;
        public int PendingTargetRating => HasPendingStageReward ? _claim.TargetRating : 0;
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
                if (_claim != null) { throw new ApiException(ApiErrorKind.Rejected, "STAGE_REWARD_PENDING"); }
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
                        bool definitive = IsDefinitiveRejection(error);
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

        public UniTask<PlayerSnapshot> ClaimRatingReward(int stageId) => ClaimOrRecover(stageId, false);
        public UniTask<PlayerSnapshot> RecoverStageReward() => ClaimOrRecover(0, true);

        private async UniTask<PlayerSnapshot> ClaimOrRecover(int stageId, bool recover)
        {
            Enter();
            try
            {
                if (_pending != null) { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_RECOVERY_REQUIRED"); }
                if (recover && _claim == null) { throw new InvalidOperationException("확인할 별 보상 요청이 없습니다"); }
                if (_claim == null)
                {
                    if (_observed == null || _observedGeneration != _operationGeneration)
                    { throw new ApiException(ApiErrorKind.Rejected, "PLAYER_NOT_LOADED"); }
                    var progress = _observed.stageProgress.Find(row => row.stageId == stageId);
                    if (progress == null || progress.clearRating <= progress.claimedRating)
                    { throw new ApiException(ApiErrorKind.Rejected, "NO_REWARD"); }
                    _claim = new PendingClaim(stageId, progress.clearRating, _operationGeneration);
                }
                else if (!recover && stageId != _claim.StageId)
                { throw new ApiException(ApiErrorKind.Rejected, "STAGE_REWARD_PENDING"); }

                if (!_claim.PostAttempted)
                {
                    _claim.PostAttempted = true;
                    try
                    {
                        var response = await _client.Post<object, ClaimResponse>("api/user/stage-progress/claim",
                            new { stageId = _claim.StageId }, isValid: value => value.stageId == _claim.StageId
                                && value.clearRating >= _claim.TargetRating && value.clearRating <= 3
                                && value.claimedRating >= _claim.TargetRating && value.claimedRating <= value.clearRating);
                        EnsureSession();
                        _claim.AcknowledgedRating = response.claimedRating.Value;
                    }
                    catch (ApiException error)
                    {
                        if (IsDefinitiveRejection(error)) { _claim = null; throw; }
                        // 409/5xx/유실/잘못된 성공 본문 뒤에는 GET만 한다. 같은 stageId 재전송도 새 등급까지 수령할 수 있다.
                    }
                }

                var snapshot = await ReadState();
                var observed = snapshot.stageProgress.Find(row => row.stageId == _claim.StageId);
                if (observed == null || observed.claimedRating < Math.Max(_claim.TargetRating, _claim.AcknowledgedRating))
                { throw new ApiException(ApiErrorKind.Rejected, "STAGE_REWARD_PENDING"); }
                _claim = null;
                return snapshot; // 수령 상태의 확인이지 이 POST의 지급 영수증이 아니다.
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

        private static bool IsDefinitiveRejection(ApiException error) => error.Kind == ApiErrorKind.Rejected
            && error.Code != null && error.Code.StartsWith("HTTP_", StringComparison.Ordinal)
            && int.TryParse(error.Code.Substring(5), out int status) && status >= 400 && status < 500 && status != 409;

        private async UniTask<PlayerSnapshot> ReadState()
        {
            await RefreshPlayerLevels();
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
            await RefreshPlayerLevels();
            var local = _data.Upgrades.GetOrThrow(id);
            var rows = await _client.Get<List<CatalogRow>>("api/upgrade");
            EnsureSession();
            var matches = rows.Where(row => row != null && row.upgradeId == id).ToList();
            int level = _observed.upgrades.Find(row => row.upgradeId == id)?.level ?? 0;
            if (matches.Count != 1 || level >= local.MaxLevel)
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_CATALOG_MISMATCH"); }
            var server = matches[0];
            if (!TryCost(server.levelCosts, server.costBase, server.costStep, level, server.maxLevel, out long coin)
                || !TryCost(server.materialCosts, server.materialBase, server.materialStep, level, server.maxLevel, out long material)
                || server.maxLevel != local.MaxLevel || server.effectiveUnlockLevel != _data.UnlockLevelOf(local)
                || (server.materialItemId ?? "") != (local.MaterialItemId ?? "") || coin != local.CostAt(level + 1)
                || (local.UsesMaterial && material != local.MaterialAt(level + 1)))
            { throw new ApiException(ApiErrorKind.Rejected, "UPGRADE_CATALOG_MISMATCH"); }
            if (_data.PlayerLevels.At(_observed.exp).level < _data.UnlockLevelOf(local))
            { throw new ApiException(ApiErrorKind.Rejected, "LOCKED"); }
        }

        private async UniTask RefreshPlayerLevels()
        {
            _data.InvalidateServerPlayerLevels();
            EnsureSession();
            var response = await _client.Get<PlayerLevelsResponse>("api/player-levels", isValid: value =>
                value.baseExp > 0 && value.stepExp >= 0 && value.maxLevel >= 2
                && value.maxLevel <= PlayerLevelConfig.MaxSupportedLevel && value.version > 0);
            EnsureSession();
            try
            {
                _data.ApplyServerPlayerLevels(new PlayerLevelConfig
                {
                    BaseExp = response.baseExp.Value,
                    StepExp = response.stepExp.Value,
                    MaxLevel = response.maxLevel.Value
                }, response.version.Value);
            }
            catch (Exception error) when (error is InvalidOperationException || error is OverflowException || error is ArgumentException)
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse); }
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

        private void Enter()
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            { throw new InvalidOperationException("서버 상태 요청이 이미 진행 중입니다"); }
            _operationGeneration = _tokens.Generation;
            if (_pending != null && _pending.Generation != _operationGeneration) { _pending = null; }
            if (_claim != null && _claim.Generation != _operationGeneration) { _claim = null; }
            try { EnsureSession(); }
            catch { Interlocked.Exchange(ref _busy, 0); throw; }
        }

        private void EnsureSession()
        {
            if (_tokens.Generation != _operationGeneration || string.IsNullOrEmpty(_tokens.AccessToken))
            {
                _data.InvalidateServerPlayerLevels();
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired);
            }
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
