using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Network
{
    /// <summary>별·도감·퀘스트·튜토리얼의 서버 영수증을 확인한 뒤 현재 계정 상태를 돌려준다.</summary>
    public sealed class ServerClaimApi : IStageApi, ICodexApi, IQuestApi, ITutorialApi
    {
        private readonly HttpApiConfig _config;
        private readonly IHttpTransport _transport;
        private readonly IAuthTokenStore _tokens;
        private readonly HttpPlayerApi _player;
        private readonly PlayerProfile _profile;
        private readonly GameDataStore _data;

        private sealed class Pending
        {
            public string Kind, Id, CreateId, PeriodKey;
            public int TargetRating;
            public QuestPeriod Period;
        }

        public ServerClaimApi(HttpApiConfig config, IHttpTransport transport, IAuthTokenStore tokens, HttpPlayerApi player,
            PlayerProfile profile, GameDataStore data)
        { _config = config; _transport = transport; _tokens = tokens; _player = player; _profile = profile; _data = data; }

        public async UniTask RecoverPending()
        {
            var stage = Load("stage");
            if (stage != null)
            {
                if (!int.TryParse(stage.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int stageId))
                { throw new ApiException(ApiErrorKind.Rejected, "CLAIM_PENDING_INVALID"); }
                _profile.Apply(await ClaimRatingReward(stageId));
            }
            var quest = Load("quest");
            if (quest != null) { _profile.Apply(await ClaimQuest(quest.Id, quest.Kind == "chest")); }
        }

        public async UniTask<PlayerSnapshot> ClaimRatingReward(int stageId)
        {
            long generation = Session();
            var pending = Load("stage");
            if (pending != null && pending.Id != stageId.ToString(CultureInfo.InvariantCulture))
            { throw new ApiException(ApiErrorKind.Rejected, "STAGE_REWARD_PENDING"); }
            if (pending == null)
            {
                int target = _profile.ClearRating(stageId);
                if (target <= _profile.ClaimedRating(stageId))
                { throw new ApiException(ApiErrorKind.Rejected, "NO_REWARD"); }
                pending = new Pending
                {
                    Kind = "stage",
                    Id = stageId.ToString(CultureInfo.InvariantCulture),
                    CreateId = Guid.NewGuid().ToString("D"),
                    TargetRating = target
                };
                Save(pending);
            }
            string json;
            try
            {
                json = await BoundClient(generation).PostJson("api/user/stage-progress/claim", new
                { stageId, createId = pending.CreateId, targetRating = pending.TargetRating });
            }
            catch (ApiException error) when (HttpErrorMapper.IsDefinitiveRequestRejection(error))
            { Ensure(generation); Clear("stage"); throw; }
            Ensure(generation);
            ServerProgressContract.Parse(() =>
            {
                if (ServerProgressContract.Read(json) is not JObject receipt
                    || receipt.Value<string>("createId") != pending.CreateId
                    || receipt["stageId"]?.Type != JTokenType.Integer || receipt.Value<int>("stageId") != stageId
                    || receipt["claimedRating"]?.Type != JTokenType.Integer
                    || receipt.Value<int>("claimedRating") < pending.TargetRating
                    || receipt["clearRating"]?.Type != JTokenType.Integer
                    || receipt.Value<int>("clearRating") < receipt.Value<int>("claimedRating")
                    || receipt["gold"]?.Type != JTokenType.Integer || receipt.Value<int>("gold") < 0
                    || receipt["items"] is not JObject || receipt["completedAt"]?.Type != JTokenType.String)
                { throw ServerProgressContract.Invalid(); }
                return true;
            });
            var snapshot = await Current(generation);
            if ((snapshot.stageProgress.Find(row => row.stageId == stageId)?.claimedRating ?? -1) < pending.TargetRating)
            { throw ServerProgressContract.Invalid(); }
            Clear("stage");
            return snapshot;
        }

        public async UniTask<PlayerSnapshot> ClaimMonsterReward(int monsterId)
        {
            long generation = Session();
            if (!_profile.CanClaimMonsterReward(monsterId))
            { throw new ApiException(ApiErrorKind.Rejected, "NOT_REGISTERED"); }
            var json = await BoundClient(generation).PostJson("api/user/codex/claim", new { monsterId });
            Ensure(generation);
            ServerProgressContract.Parse(() =>
            {
                if (ServerProgressContract.Read(json) is not JObject receipt
                    || receipt["monsterId"]?.Type != JTokenType.Integer || receipt.Value<int>("monsterId") != monsterId
                    || receipt["gold"]?.Type != JTokenType.Integer || receipt.Value<int>("gold") < 0
                    || receipt["claimedAt"]?.Type != JTokenType.String)
                { throw ServerProgressContract.Invalid(); }
                return true;
            });
            return await Current(generation);
        }

        public UniTask<PlayerSnapshot> ClaimQuest(string questId) => ClaimQuest(questId, false);
        public UniTask<PlayerSnapshot> ClaimQuestChest(string chestId) => ClaimQuest(chestId, true);

        private async UniTask<PlayerSnapshot> ClaimQuest(string id, bool chest)
        {
            long generation = Session();
            var pending = Load("quest");
            if (pending != null && (pending.Id != id || pending.Kind != (chest ? "chest" : "quest")))
            { throw new ApiException(ApiErrorKind.Rejected, "QUEST_CLAIM_PENDING"); }
            if (pending == null)
            {
                var board = _data.QuestBoards.FirstOrDefault(row => chest
                    ? row.Chests.Any(c => c.Id == id) : row.Quests.Any(q => q.Id == id));
                var period = board == null ? null : _profile.Snapshot().questPeriods.Find(p => p.period == board.Period);
                if (period == null) { throw new ApiException(ApiErrorKind.Rejected, "QUEST_PERIOD_UNAVAILABLE"); }
                pending = new Pending
                {
                    Kind = chest ? "chest" : "quest",
                    Id = id,
                    Period = board.Period,
                    PeriodKey = period.periodKey
                };
                Save(pending);
            }
            var request = new QuestClaimRequest(id, pending.Period, pending.PeriodKey);
            var api = new HttpQuestApi(BoundClient(generation));
            try
            {
                if (chest) { await api.ClaimChest(request); }
                else { await api.ClaimQuest(request); }
            }
            catch (ApiException error) when (HttpErrorMapper.IsDefinitiveRequestRejection(error))
            { Ensure(generation); Clear("quest"); throw; }
            Ensure(generation);
            var snapshot = await Current(generation);
            Clear("quest");
            return snapshot;
        }

        public async UniTask<TutorialCompleteResponse> Complete(string tutorialId)
        {
            long generation = Session();
            var receipt = await new HttpTutorialApi(BoundClient(generation)).Complete(new TutorialCompletionRequest(tutorialId));
            Ensure(generation);
            var snapshot = await Current(generation);
            var items = new List<ItemAmount>();
            foreach (var pair in receipt.Items) { items.Add(new ItemAmount { itemId = pair.Key, quantity = pair.Value }); }
            return new TutorialCompleteResponse { snapshot = snapshot, rewardGold = receipt.Gold, rewardItems = items };
        }

        private async UniTask<PlayerSnapshot> Current(long generation)
        {
            var snapshot = await _player.GetMe();
            Ensure(generation);
            return snapshot;
        }

        private long Session()
        {
            long generation = _tokens.Generation;
            Ensure(generation);
            return generation;
        }
        private HttpApiClient BoundClient(long generation) => new(_config,
            new BoundTransport(_transport, () => Ensure(generation)), tokens: _tokens);
        private sealed class BoundTransport : IHttpTransport
        {
            private readonly IHttpTransport _inner;
            private readonly Action _check;
            public BoundTransport(IHttpTransport inner, Action check) { _inner = inner; _check = check; }
            public UniTask<HttpResponseData> Send(HttpRequestData request, float timeoutSeconds, CancellationToken cancellationToken)
            { _check(); return _inner.Send(request, timeoutSeconds, cancellationToken); }
        }
        private void Ensure(long generation)
        {
            if (_tokens.Generation != generation || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }
        private string Key(string name) => "server.claim.pending." + ServerAccountIdentity.Subject(_tokens.AccessToken) + "." + name;
        private Pending Load(string name)
        {
            string json = PlayerPrefs.GetString(Key(name), string.Empty);
            if (string.IsNullOrEmpty(json)) { return null; }
            try { return JsonConvert.DeserializeObject<Pending>(json) ?? throw new FormatException(); }
            catch (Exception error) when (error is JsonException or FormatException)
            { throw new ApiException(ApiErrorKind.Rejected, "CLAIM_PENDING_INVALID"); }
        }
        private void Save(Pending pending)
        {
            PlayerPrefs.SetString(Key(pending.Kind == "stage" ? "stage" : "quest"), JsonConvert.SerializeObject(pending));
            PlayerPrefs.Save();
        }
        private void Clear(string name) { PlayerPrefs.DeleteKey(Key(name)); PlayerPrefs.Save(); }
    }
}
