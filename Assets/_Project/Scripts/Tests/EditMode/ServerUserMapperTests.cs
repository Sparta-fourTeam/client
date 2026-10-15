using System.Collections.Generic;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>서버 GET /api/user/me 응답(ServerUserData)을 PlayerSnapshot으로 바꾸는 규칙</summary>
    public sealed class ServerUserMapperTests
    {
        private static ServerUserData Full() => new()
        {
            gold = 120,
            jem = 7,
            exp = 45,
            energyStored = 30,
            energyUpdatedAt = "2026-10-14T10:00:00",
            stageProgress = new List<ServerStageProgress> { new() { stageId = 2, clearRating = 3, claimedRating = 1 } },
            upgrades = new List<ServerUpgrade> { new() { upgradeId = "shuriken", level = 4 } },
            items = new List<ServerItem> { new() { itemId = 9, quantity = 5 } },
            codex = new List<ServerCodex> { new() { monsterId = 12, claimed = true } },
            tutorials = new List<long> { 3 },
        };

        [Test(Description = "서버 필드 이름(jem)과 목록이 클라이언트 스냅샷 필드로 옮겨지고, 닉네임은 넘긴 값을 쓴다")]
        public void Maps_FieldsAndLists()
        {
            var snapshot = ServerUserMapper.ToSnapshot(Full(), "닌자");

            Assert.AreEqual("닌자", snapshot.nickname);
            Assert.AreEqual(120, snapshot.gold);
            Assert.AreEqual(7, snapshot.gems, "서버 필드 jem은 gems로 옮긴다");
            Assert.AreEqual(45, snapshot.exp);
            Assert.AreEqual(30, snapshot.energyStored);
            Assert.AreEqual("2026-10-14T10:00:00", snapshot.energyUpdatedAt);
            Assert.AreEqual(2, snapshot.stageProgress[0].stageId);
            Assert.AreEqual(3, snapshot.stageProgress[0].clearRating);
            Assert.AreEqual(1, snapshot.stageProgress[0].claimedRating);
            Assert.AreEqual("shuriken", snapshot.upgrades[0].upgradeId);
            Assert.AreEqual(4, snapshot.upgrades[0].level);
            Assert.AreEqual(12, snapshot.codex[0].monsterId);
            Assert.IsTrue(snapshot.codex[0].claimed);
        }

        [Test(Description = "서버가 null로 줄 수 있는 경험치·강화 레벨과 빠진 목록은 0과 빈 목록으로 채운다")]
        public void Nulls_BecomeZeroAndEmptyLists()
        {
            var snapshot = ServerUserMapper.ToSnapshot(new ServerUserData { exp = null, upgrades = new List<ServerUpgrade> { new() { upgradeId = "a", level = null } } }, null);

            Assert.AreEqual(0, snapshot.exp);
            Assert.AreEqual(0, snapshot.upgrades[0].level);
            Assert.IsEmpty(snapshot.stageProgress);
            Assert.IsEmpty(snapshot.codex);
        }

        [Test(Description = "아이템·튜토리얼은 서버와 클라이언트 ID 대응이 정해지기 전까지 옮기지 않고 빈 목록으로 둔다")]
        public void ItemsAndTutorials_AreNotApplied()
        {
            var snapshot = ServerUserMapper.ToSnapshot(Full(), "n");

            Assert.IsEmpty(snapshot.items);
            Assert.IsEmpty(snapshot.tutorials);
        }
        [Test(Description = "int 범위를 넘는 스테이지·몬스터 ID는 잘못된 응답이다")]
        public void OutOfRangeIds_AreRejected()
        {
            var stage = new ServerUserData { stageProgress = new List<ServerStageProgress> { new() { stageId = long.MaxValue } } };
            var codex = new ServerUserData { codex = new List<ServerCodex> { new() { monsterId = (long)int.MaxValue + 1 } } };

            var a = Assert.Throws<ApiException>(() => ServerUserMapper.ToSnapshot(stage, "n"));
            var b = Assert.Throws<ApiException>(() => ServerUserMapper.ToSnapshot(codex, "n"));

            Assert.AreEqual(HttpErrorMapper.InvalidResponse, a.Code);
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, b.Code);
        }

        [Test(Description = "서버가 시간대 없이 주는 에너지 갱신 시각은 클라이언트가 UTC로 해석한다")]
        public void EnergyUpdatedAt_IsReadAsUtc()
        {
            var snapshot = ServerUserMapper.ToSnapshot(Full(), "n");

            var parsed = EnergyRule.ParseUpdatedAt(snapshot.energyUpdatedAt);

            Assert.AreEqual(System.DateTimeKind.Utc, parsed.Kind);
            Assert.AreEqual(new System.DateTime(2026, 10, 14, 10, 0, 0, System.DateTimeKind.Utc), parsed);
        }
    }
}
