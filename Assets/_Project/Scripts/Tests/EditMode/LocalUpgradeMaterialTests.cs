using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>재료를 쓰는 강화(장비)와 해금 레벨 검증. 스킬 강화도 같은 API를 쓴다</summary>
    public sealed class LocalUpgradeMaterialTests
    {
        private const string Hat = "equipment.hat";   // 해금 플레이어 레벨 2
        private string _path;
        private GameDataStore _data;
        private LocalSaveStore _store;
        private LocalUpgradeApi _api;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"local-upgrade-material-test-{Guid.NewGuid()}.json");
            _data = new GameDataStore();
            _store = new LocalSaveStore(_path);
            _api = new LocalUpgradeApi(_store, _data);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) { File.Delete(_path); }
        }

        // 레벨에 막 도달하는 누적 경험치
        private int ExpFor(int level)
        {
            int exp = 0;
            for (int l = 1; l < level; l++) { exp += _data.PlayerLevels.RequiredToNext(l); }
            return exp;
        }

        private void Seed(int playerLevel = 5, int gold = 10000, int materials = 100, int equipmentLevel = 0)
        {
            var save = _store.Load();
            save.exp = ExpFor(playerLevel);
            save.wallet.gold = gold;
            save.items = materials > 0 ? new List<ItemAmount> { new() { itemId = ItemIds.HatBook, quantity = materials } } : new List<ItemAmount>();
            save.upgrades = equipmentLevel > 0 ? new List<UpgradeRow> { new() { upgradeId = Hat, level = equipmentLevel } } : new List<UpgradeRow>();
            _store.Flush(save);
        }

        private PlayerSnapshot Upgrade(string key = "key-1") => _api.Purchase(Hat, key).GetAwaiter().GetResult();
        private string RejectCode(string key = "key-1") => Assert.Throws<ApiException>(() => Upgrade(key)).Code;

        [Test(Description = "코인과 재료가 충분하면 한 단계 오르고 해당 레벨의 비용만큼 차감한다")]
        public void Upgrade_WithEnoughResources_LevelsUpAndSpends()
        {
            Seed(gold: 1000, materials: 10);
            var def = _data.Upgrades.GetOrThrow(Hat);

            var snap = Upgrade();

            Assert.AreEqual(1, snap.upgrades.Find(u => u.upgradeId == Hat).level);
            Assert.AreEqual(1000 - def.CostAt(1), snap.gold);
            Assert.AreEqual(10 - def.MaterialAt(1), snap.items.Find(i => i.itemId == ItemIds.HatBook).quantity);
        }

        [Test(Description = "이미 강화한 장비는 다음 레벨의 비용을 낸다")]
        public void Upgrade_NextLevelUsesNextCost()
        {
            Seed(gold: 1000, materials: 10, equipmentLevel: 2);
            var def = _data.Upgrades.GetOrThrow(Hat);

            var snap = Upgrade();

            Assert.AreEqual(3, snap.upgrades.Find(u => u.upgradeId == Hat).level);
            Assert.AreEqual(1000 - def.CostAt(3), snap.gold);
        }

        [Test(Description = "플레이어 레벨이 해금 레벨보다 낮으면 LOCKED로 거절한다")]
        public void Upgrade_BelowUnlockLevel_IsLocked()
        {
            Seed(playerLevel: _data.Upgrades.GetOrThrow(Hat).UnlockLevel - 1);

            Assert.AreEqual("LOCKED", RejectCode());
        }

        [Test(Description = "해금 레벨에 도달하면 강화할 수 있다")]
        public void Upgrade_AtUnlockLevel_IsAllowed()
        {
            Seed(playerLevel: _data.Upgrades.GetOrThrow(Hat).UnlockLevel);

            Assert.DoesNotThrow(() => Upgrade());
        }

        [Test(Description = "최대 레벨이면 ALREADY_MAX로 거절한다")]
        public void Upgrade_AtMaxLevel_IsRejected()
        {
            Seed(equipmentLevel: _data.Upgrades.GetOrThrow(Hat).MaxLevel);

            Assert.AreEqual("ALREADY_MAX", RejectCode());
        }

        [Test(Description = "코인이 모자라면 INSUFFICIENT_GOLD로 거절하고 아무것도 바뀌지 않는다")]
        public void Upgrade_WithoutEnoughGold_ChangesNothing()
        {
            Seed(gold: 0, materials: 10);

            Assert.AreEqual("INSUFFICIENT_GOLD", RejectCode());

            var save = _store.Load();
            Assert.AreEqual(0, save.wallet.gold);
            Assert.AreEqual(10, LocalItemSpend.Owned(save, ItemIds.HatBook));
            Assert.IsEmpty(save.upgrades);
        }

        [Test(Description = "재료가 모자라면 INSUFFICIENT_ITEM으로 거절하고 코인도 차감하지 않는다")]
        public void Upgrade_WithoutEnoughMaterials_ChangesNothing()
        {
            Seed(gold: 1000, materials: 0);

            Assert.AreEqual("INSUFFICIENT_ITEM", RejectCode());

            var save = _store.Load();
            Assert.AreEqual(1000, save.wallet.gold);
            Assert.IsEmpty(save.upgrades);
            Assert.IsEmpty(save.ledger);
        }

        [Test(Description = "같은 idempotencyKey로 다시 호출하면 한 번만 강화된다")]
        public void Upgrade_SameKey_AppliesOnce()
        {
            Seed(gold: 1000, materials: 10);

            Upgrade("same-key");
            var second = Upgrade("same-key");

            Assert.AreEqual(1, second.upgrades.Find(u => u.upgradeId == Hat).level);
            Assert.AreEqual(1000 - _data.Upgrades.GetOrThrow(Hat).CostAt(1), second.gold);
        }

        [Test(Description = "강화 결과는 저장 파일에 남아 다시 읽어도 유지된다")]
        public void Upgrade_PersistsAcrossStoreReload()
        {
            Seed(gold: 1000, materials: 10);

            Upgrade();

            var reloaded = new LocalSaveStore(_path).Load();
            Assert.AreEqual(1, reloaded.upgrades.Find(u => u.upgradeId == Hat).level);
        }

        [Test(Description = "없는 장비 ID는 UNKNOWN_DATA_ID로 거절한다")]
        public void Upgrade_UnknownEquipment_IsRejected()
        {
            Seed();

            var ex = Assert.Throws<ApiException>(() => _api.Purchase("equipment.nothing", "key-1").GetAwaiter().GetResult());

            Assert.AreEqual("UNKNOWN_DATA_ID", ex.Code);
        }
    }
}
