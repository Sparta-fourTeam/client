using System.Collections.Generic;
using Game.Core;
using Game.Network;
using Newtonsoft.Json;
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
            tutorials = new List<string>(),
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

        [Test(Description = "이전 숫자 아이템은 문자열 ID로 추측하지 않는다")]
        public void LegacyNumericItems_AreNotApplied()
        {
            var snapshot = ServerUserMapper.ToSnapshot(Full(), "n");

            Assert.IsEmpty(snapshot.items);
            Assert.IsEmpty(snapshot.tutorials);
        }

        [TestCase("{\"gem\":12}", 12)]
        [TestCase("{\"jem\":7}", 7)]
        [TestCase("{\"gem\":0,\"jem\":7}", 0)]
        [Test(Description = "신규 gem과 이전 jem을 읽고 명시적인 gem 0을 이전 잔액으로 덮어쓰지 않는다")]
        public void GemContract_UsesNewFieldBeforeLegacy(string json, int expected)
        {
            var snapshot = ServerUserMapper.ToSnapshot(JsonConvert.DeserializeObject<ServerUserData>(json), "n");
            Assert.AreEqual(expected, snapshot.gems);
        }

        [Test(Description = "PR58 문자열 아이템 ID·수량은 그대로 반영하고 이전 숫자 ID는 문자열로 추측하지 않는다")]
        public void Items_StringIdsAreApplied_LegacyNumericIdsAreSkipped()
        {
            var data = JsonConvert.DeserializeObject<ServerUserData>("{\"items\":[{\"itemId\":\"book.arrow\",\"quantity\":5},{\"itemId\":9,\"quantity\":8},{\"itemId\":\"book.weapon\",\"quantity\":0}],\"tutorials\":[]}");
            var snapshot = ServerUserMapper.ToSnapshot(data, "n");
            Assert.AreEqual(2, snapshot.items.Count);
            Assert.AreEqual("book.arrow", snapshot.items[0].itemId);
            Assert.AreEqual(5, snapshot.items[0].quantity);
            Assert.AreEqual("book.weapon", snapshot.items[1].itemId);
            Assert.AreEqual(0, snapshot.items[1].quantity);
            Assert.IsEmpty(snapshot.tutorials);
        }

        [TestCase("[{\"itemId\":\" \" ,\"quantity\":1}]")]
        [TestCase("[{\"itemId\":\"book.arrow\",\"quantity\":-1}]")]
        [TestCase("[{\"itemId\":\"book.arrow\",\"quantity\":1},{\"itemId\":\"book.arrow\",\"quantity\":2}]")]
        [Test(Description = "문자열 아이템의 빈 ID·음수 수량·중복 행으로 인벤토리가 오염되지 않게 응답을 거절한다")]
        public void Items_InvalidStringRowsAreRejected(string rows)
        {
            var data = JsonConvert.DeserializeObject<ServerUserData>("{\"items\":" + rows + "}");
            var ex = Assert.Throws<ApiException>(() => ServerUserMapper.ToSnapshot(data, "n"));
            Assert.AreEqual(HttpErrorMapper.InvalidResponse, ex.Code);
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
