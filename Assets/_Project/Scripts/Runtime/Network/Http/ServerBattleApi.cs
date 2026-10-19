using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.Network
{
    /// <summary>서버 발급·누적 원장·최초 결과 영수증을 같은 계정 세션으로 묶는다.</summary>
    public sealed class ServerBattleApi : IBattleApi, IBattleProgressApi, IBattleCheckpointProgressApi, IBattleLaunchCheckpointSession
    {
        private readonly HttpApiConfig _config;
        private readonly IHttpTransport _transport;
        private readonly IAuthTokenStore _tokens;
        private readonly GameDataStore _data;
        private readonly PlayerProfile _profile;
        private readonly HttpPlayerApi _player;
        private readonly StageContext _stage;
        private readonly ServerBattleCheckpointStore _checkpoints;
        private IBattleProgressApi _progress;
        private long _progressGeneration = -1;
        private string _issuedBattleId;
        private int _issuedStageId;

        public ServerBattleApi(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens,
            GameDataStore data, PlayerProfile profile, HttpPlayerApi player, StageContext stage,
            ServerBattleCheckpointStore checkpoints)
        {
            _config = config; _transport = transport; _tokens = tokens;
            _data = data; _profile = profile; _player = player; _stage = stage; _checkpoints = checkpoints;
            _checkpoints.ServerBattleDurable += OnBattleDurable;
        }

        public string SourceRevision => _progressGeneration == _tokens.Generation && _progress != null
            ? _progress.SourceRevision : _stage.Checkpoint?.RestoreBattleData(_data).BattleSourceRevision ?? _data.BattleSourceRevision;

        public async UniTask<StartBattleResponse> StartBattle(int stageId)
        {
            if (!_profile.IsLoaded || !_data.HasServerPlayerLevels || !_data.Stages.Contains(stageId))
            { throw new ApiException(ApiErrorKind.Rejected, "BATTLE_SOURCE_UNAVAILABLE"); }
            long generation = _tokens.Generation;
            string subject = ServerAccountIdentity.Subject(_tokens.AccessToken);
            var pending = PendingStart.Load(subject);
            if (pending != null && pending.StageId != stageId)
            { throw new ApiException(ApiErrorKind.Rejected, "BATTLE_START_PENDING"); }
            pending ??= new PendingStart { StageId = stageId, CreateId = Guid.NewGuid().ToString("D") };
            pending.Save(subject); // 전송 전에 같은 계정·같은 요청 ID를 확정한다.
            var client = BoundClient(generation);
            StartBattleResponse issued;
            try { issued = await new HttpBattleSessionApi(client).StartBattle(new BattleStartRequest(pending.CreateId)); }
            catch (ApiException error) when (HttpErrorMapper.IsDefinitiveRequestRejection(error))
            {
                Ensure(generation);
                PendingStart.Clear(subject);
                throw;
            }
            Ensure(generation);
            var progress = new HttpBattleProgressApi(client, stageId, _data);
            await progress.Bind(issued.battleId);
            Ensure(generation);
            if (pending.BattleId != null && pending.BattleId != issued.battleId)
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse); }
            pending.BattleId = issued.battleId;
            pending.Save(subject);
            _progress = new BoundProgress(progress, _tokens, generation);
            _progressGeneration = generation;
            _issuedBattleId = issued.battleId;
            _issuedStageId = stageId;
            return issued;
        }

        private void OnBattleDurable(string battleId, int stageId)
        {
            if (string.IsNullOrEmpty(_tokens.AccessToken)) { return; }
            string subject = ServerAccountIdentity.Subject(_tokens.AccessToken);
            PendingStart pending;
            try { pending = PendingStart.Load(subject); }
            catch (ApiException) { return; }
            if (pending?.BattleId == battleId && pending.StageId == stageId)
            { PendingStart.Clear(subject); }
        }

        public void OnBattleLaunched(string battleId, int stageId)
        {
            if (_progressGeneration != _tokens.Generation || _progress == null
                || _issuedBattleId != battleId || _issuedStageId != stageId)
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
            _checkpoints.AuthorizeServerBattle(battleId, stageId);
        }

        public async UniTask<SubmitResultResponse> SubmitResult(SubmitResultRequest request)
        {
            if (request == null || request.battleId != _stage.BattleId)
            { throw new ApiException(ApiErrorKind.Rejected, "INVALID_BATTLE_ID"); }
            long generation = _tokens.Generation;
            var client = BoundClient(generation);
            int rating = request.cleared ? StageRewardRules.ClearRating(request.wallHpPercent) : 0;
            var receipt = await new HttpBattleSessionApi(client).EndBattle(new BattleEndRequest(
                request.battleId, _stage.StageId, request.completedWaves, rating));
            Ensure(generation);
            var snapshot = await _player.GetMe();
            Ensure(generation);
            _profile.Apply(snapshot);
            OnBattleDurable(request.battleId, _stage.StageId);
            var items = new List<ItemAmount>();
            foreach (var pair in receipt.Items) { items.Add(new ItemAmount { itemId = pair.Key, quantity = pair.Value }); }
            return new SubmitResultResponse
            {
                rewardGold = receipt.Gold,
                rewardExp = receipt.Exp,
                rewardBonusExp = receipt.ThreeStarBonusExp,
                clearRating = receipt.ClearRating,
                rewardItems = items
            };
        }

        public IBattleProgressApi ForCheckpoint(BattleCheckpoint checkpoint)
        {
            if (checkpoint == null) { throw new ArgumentNullException(nameof(checkpoint)); }
            long generation = _tokens.Generation;
            var source = checkpoint.RestoreBattleData(_data);
            return new BoundProgress(new HttpBattleProgressApi(BoundClient(generation), checkpoint.StageId, source), _tokens, generation);
        }

        public UniTask<BattleProgressAck> ReportProgress(BattleProgressRequest request) => Progress().ReportProgress(request);
        public async UniTask<BattleProgressAck> AbandonProgress(BattleProgressRequest request)
        {
            var ack = await Progress().AbandonProgress(request);
            OnBattleDurable(request.battleId, _stage.StageId);
            return ack;
        }

        private IBattleProgressApi Progress()
        {
            if (_progressGeneration == _tokens.Generation && _progress != null) { return _progress; }
            long generation = _tokens.Generation;
            var checkpoint = _stage.Checkpoint;
            var source = checkpoint?.RestoreBattleData(_data) ?? _data;
            _progress = new BoundProgress(new HttpBattleProgressApi(BoundClient(generation), _stage.StageId, source), _tokens, generation);
            _progressGeneration = generation;
            return _progress;
        }

        private HttpApiClient BoundClient(long generation) => new(_config,
            new SessionTransport(_transport, () => Ensure(generation)), tokens: _tokens);

        private void Ensure(long generation)
        {
            if (_tokens.Generation != generation || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }

        private sealed class SessionTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;
            private readonly Action _ensure;
            public SessionTransport(IHttpTransport inner, Action ensure) { _inner = inner; _ensure = ensure; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, System.Threading.CancellationToken cancellationToken)
            { _ensure(); return _inner.Send(request, timeoutSeconds, cancellationToken); }
        }

        private sealed class BoundProgress : IBattleProgressApi
        {
            private readonly IBattleProgressApi _inner;
            private readonly IAuthTokenStore _tokens;
            private readonly long _generation;
            public BoundProgress(IBattleProgressApi inner, IAuthTokenStore tokens, long generation)
            { _inner = inner; _tokens = tokens; _generation = generation; }
            public string SourceRevision => _inner.SourceRevision;
            public async UniTask<BattleProgressAck> ReportProgress(BattleProgressRequest request)
            { Ensure(); var result = await _inner.ReportProgress(request); Ensure(); return result; }
            public async UniTask<BattleProgressAck> AbandonProgress(BattleProgressRequest request)
            { Ensure(); var result = await _inner.AbandonProgress(request); Ensure(); return result; }
            private void Ensure()
            {
                if (_tokens.Generation != _generation || string.IsNullOrEmpty(_tokens.AccessToken))
                { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
            }
        }

        private sealed class PendingStart
        {
            public int StageId;
            public string CreateId, BattleId;
            private static string Key(string subject) => "server.battle.start." + subject;
            public static PendingStart Load(string subject)
            {
                string json = PlayerPrefs.GetString(Key(subject), string.Empty);
                if (string.IsNullOrEmpty(json)) { return null; }
                try
                {
                    var pending = JsonConvert.DeserializeObject<PendingStart>(json);
                    if (pending == null || pending.StageId <= 0 || !Guid.TryParse(pending.CreateId, out _)
                        || pending.BattleId != null && !Guid.TryParse(pending.BattleId, out _))
                    { throw new FormatException(); }
                    return pending;
                }
                catch (Exception error) when (error is JsonException or FormatException)
                { throw new ApiException(ApiErrorKind.Rejected, "BATTLE_START_PENDING_INVALID"); }
            }
            public void Save(string subject)
            { PlayerPrefs.SetString(Key(subject), JsonConvert.SerializeObject(this)); PlayerPrefs.Save(); }
            public static void Clear(string subject)
            { PlayerPrefs.DeleteKey(Key(subject)); PlayerPrefs.Save(); }
        }
    }
}
