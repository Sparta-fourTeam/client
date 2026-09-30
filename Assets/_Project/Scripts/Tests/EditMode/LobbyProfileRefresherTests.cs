using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class LobbyProfileRefresherTests
    {
        private sealed class FakePlayerApi : IPlayerApi
        {
            public PlayerSnapshot Response;
            public Exception ToThrow;

            public UniTask<PlayerSnapshot> GetMe()
            {
                if (ToThrow != null)
                {
                    throw ToThrow;
                }

                return UniTask.FromResult(Response);
            }
        }

        private PlayerProfile _profile;
        private FakePlayerApi _api;
        private LobbyProfileRefresher _refresher;

        [SetUp]
        public void SetUp()
        {
            _profile = new PlayerProfile(new GameDataStore());
            _profile.Apply(Snapshot(gold: 100, energy: 50, stages: 1));
            _api = new FakePlayerApi();
            _refresher = new LobbyProfileRefresher(_api, _profile);
        }

        private static PlayerSnapshot Snapshot(int gold, int energy, int stages)
        {
            var progress = new List<StageProgressRow>();
            for (int id = 1; id <= stages; id++)
            {
                progress.Add(new StageProgressRow { stageId = id });
            }

            return new PlayerSnapshot
            {
                gold = gold,
                energyStored = energy,
                energyUpdatedAt = DateTime.UtcNow.ToString("O"),
                stageProgress = progress,
                upgrades = new(),
            };
        }

        [Test(Description = "로비에 들어오면 저장소의 최신 상태(전투 후 해금·재화)로 프로필을 바꾼다")]
        public void Start_AppliesLatestSnapshot()
        {
            _api.Response = Snapshot(gold: 130, energy: 45, stages: 2);

            _refresher.Start();

            Assert.AreEqual(2, _profile.HighestUnlockedStage);
            Assert.IsTrue(_profile.IsStageUnlocked(2));
            Assert.AreEqual(130, _profile.Gold);
            Assert.AreEqual(45, _profile.EnergyStored);
            Assert.AreEqual(UniTaskStatus.Succeeded, _refresher.WhenLoaded.Status);
        }

        [Test(Description = "조회에 실패해도 WhenLoaded는 끝나 입력이 영원히 막히지 않고, 프로필은 그대로 둔다")]
        public void Start_WhenGetMeFails_CompletesAndKeepsProfile()
        {
            _api.ToThrow = new ApiException(ApiErrorKind.Rejected, "TEST");
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("TEST"));

            _refresher.Start();

            Assert.AreEqual(UniTaskStatus.Succeeded, _refresher.WhenLoaded.Status);
            Assert.AreEqual(1, _profile.HighestUnlockedStage);
            Assert.AreEqual(100, _profile.Gold);
        }
    }
}
