using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>초기 데이터가 게시된 격리 MySQL 서버에서만 실행하는 Root 어댑터 실제 HTTP 검증.</summary>
    public sealed class ServerAccountRootLiveTests
    {
        [UnityTest]
        public IEnumerator AccountBattleCheckpointResultAndShopRestoreAcrossSessions() => UniTask.ToCoroutine(async () =>
        {
            string fixturePath = Environment.GetEnvironmentVariable("ACCOUNT532_HTTP_FIXTURE");
            if (string.IsNullOrEmpty(fixturePath)) { Assert.Ignore("격리 서버 fixture가 지정되지 않았습니다"); }
            var fixture = JObject.Parse(File.ReadAllText(fixturePath));
            var endpoint = new Uri(fixture.Value<string>("baseUrl"));
            Assert.AreEqual("127.0.0.1", endpoint.Host);
            Assert.AreEqual("MySQL", fixture.Value<string>("databaseProduct"));
            var config = new HttpApiConfig(endpoint.ToString(), 15);
            var transport = new UnityWebRequestTransport();
            var tokens = new AuthSession();
            tokens.Set(fixture.Value<string>("tokenA"), null);
            var client = new HttpApiClient(config, transport, tokens: tokens);
            var data = new GameDataStore();
            var profile = new PlayerProfile(data);
            var player = new HttpPlayerApi(client, tokens, data,
                new HttpBattleSourceApi(client), new HttpDataVersionApi(client));
            string folder = Path.Combine(Path.GetTempPath(), "account532-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                profile.Apply(await player.GetMe());
                Assert.IsTrue(data.HasServerPlayerLevels);
                Assert.AreEqual(fixture.Value<string>("sourceRevision"), data.BattleSourceRevision);
                Assert.AreEqual(0, profile.Gold);
                int initialEnergy = profile.EnergyStored;
                int stageId = fixture.Value<int>("stageId");
                var stage = new StageContext();
                stage.Set(stageId);
                var save = new LocalSaveStore(Path.Combine(folder, "save.json"), data);
                using var checkpoints = new ServerBattleCheckpointStore(save, null, profile, data, tokens);
                var battle = new ServerBattleApi(config, transport, tokens, data, profile, player, stage, checkpoints);

                var started = await battle.StartBattle(stageId);
                int chargedEnergy = (await player.GetMe()).energyStored;
                var restartedBeforeCheckpoint = await battle.StartBattle(stageId);
                Assert.AreEqual(started.battleId, restartedBeforeCheckpoint.battleId,
                    "첫 체크포인트 전 재시도는 같은 서버 전투를 복원합니다");
                Assert.AreEqual(chargedEnergy, (await player.GetMe()).energyStored);
                checkpoints.Clear();
                battle.OnBattleLaunched(started.battleId, stageId);
                stage.SetBattle(started.battleId);
                var checkpoint = new BattleCheckpoint
                {
                    BattleId = started.battleId,
                    StageId = stageId,
                    RandomState = JsonUtility.ToJson(UnityEngine.Random.state),
                    Stage = new StageRuntimeState
                    {
                        State = StageState.Paused,
                        BeforePause = StageState.Playing,
                        Speed = 1,
                        WallHp = 30,
                        WallMaxHp = 50,
                        ProgressSourceRevision = data.BattleSourceRevision
                    },
                    Spawns = new SpawnRuntimeState()
                };
                checkpoints.Save(checkpoint);

                tokens.Set(fixture.Value<string>("tokenB"), null);
                profile.Clear();
                profile.Apply(await player.GetMe());
                Assert.AreEqual(0, profile.Gold);
                Assert.IsNull(checkpoints.Load(), "다른 계정은 A의 전투를 열 수 없습니다");

                var anonymous = new HttpApiClient(config, transport);
                var login = await anonymous.Post<object, JObject>("api/auth/guest/login",
                    new { guestId = fixture.Value<string>("guestIdA") });
                tokens.Set(login.Value<string>("accessToken"), null);
                profile.Apply(await player.GetMe());
                var restored = checkpoints.Load();
                Assert.IsNotNull(restored);
                Assert.AreEqual(started.battleId, restored.BattleId);
                Assert.AreEqual(data.BattleSourceRevision, restored.RestoreBattleData(data).BattleSourceRevision);
                stage.Checkpoint = restored;
                var request = new SubmitResultRequest
                {
                    battleId = started.battleId,
                    completedWaves = 0,
                    reachedWave = 0,
                    killedMonsters = 0,
                    cleared = false,
                    wallHpPercent = 0
                };
                var result = await battle.SubmitResult(request);
                Assert.Greater(result.rewardGold, 0);
                int awardedGold = profile.Gold;
                Assert.AreEqual(result.rewardGold, awardedGold);
                Assert.Less(profile.EnergyStored, initialEnergy);
                var replay = await battle.SubmitResult(request);
                Assert.AreEqual(result.rewardGold, replay.rewardGold);
                Assert.AreEqual(awardedGold, profile.Gold, "같은 결과 재요청은 재지급하지 않습니다");
                checkpoints.Clear();

                var shopApi = new HttpShopApi(config, transport, tokens);
                var shop = new ServerShopViewSession(shopApi,
                    new ShopPendingRequestStore(Path.Combine(folder, "pending"), tokens), profile);
                await shop.Restore();
                Assert.IsTrue(shop.Account.Daily.Available);
                int slot = Enumerable.Range(0, shop.Account.Daily.Offers.Count)
                    .First(index => shop.Account.Daily.Offers[index].Price <= shop.Account.Gold);
                var purchase = await shop.BuyDaily(shop.Account.Daily, slot);
                Assert.AreEqual(ShopViewSubmissionStatus.Acknowledged, purchase.Status);
                Assert.Contains(slot, shop.Account.Daily.Purchased.ToList());

                login = await anonymous.Post<object, JObject>("api/auth/guest/login",
                    new { guestId = fixture.Value<string>("guestIdA") });
                tokens.Set(login.Value<string>("accessToken"), null);
                profile.Apply(await player.GetMe());
                var reopened = new ServerShopViewSession(new HttpShopApi(config, transport, tokens),
                    new ShopPendingRequestStore(Path.Combine(folder, "pending"), tokens), profile);
                await reopened.Restore();
                Assert.Contains(slot, reopened.Account.Daily.Purchased.ToList(), "다시 로그인해도 구매 상태가 복원됩니다");
            }
            finally { Directory.Delete(folder, true); }
        });
    }
}
