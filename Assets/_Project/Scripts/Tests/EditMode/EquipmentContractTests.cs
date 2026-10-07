using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EquipmentContractTests
    {
        private string _path;
        private LocalSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"equipment-contract-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_path);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) { File.Delete(_path); }
        }

        [Test(Description = "강화한 적이 없는 장비는 레벨 0이고, 스냅샷을 받으면 그 레벨을 돌려준다")]
        public void Profile_EquipmentLevel_DefaultsToZeroAndFollowsSnapshot()
        {
            var profile = new PlayerProfile(new GameDataStore());
            Assert.AreEqual(0, profile.EquipmentLevel("equipment.hat"));

            profile.Apply(new PlayerSnapshot { equipments = new List<EquipmentRow> { new() { equipmentId = "equipment.hat", level = 3 } } });

            Assert.AreEqual(3, profile.EquipmentLevel("equipment.hat"));
            Assert.AreEqual(0, profile.EquipmentLevel("equipment.top"));
        }

        [Test(Description = "equipments가 없는 스냅샷(이전 형식)을 받아도 레벨 0으로 처리한다")]
        public void Profile_Apply_ToleratesMissingEquipments()
        {
            var profile = new PlayerProfile(new GameDataStore());

            profile.Apply(new PlayerSnapshot());

            Assert.AreEqual(0, profile.EquipmentLevel("equipment.hat"));
        }

        [Test(Description = "장비 레벨은 저장 파일에 남고 스냅샷으로 내려간다 (재실행 후 유지)")]
        public void LocalSave_PersistsEquipmentsIntoSnapshot()
        {
            var save = _store.Load();
            save.equipments.Add(new EquipmentRow { equipmentId = "equipment.ring", level = 2 });
            _store.Flush(save);

            var reloaded = new LocalSaveStore(_path).Load();
            var snapshot = new LocalPlayerApi(new LocalSaveStore(_path)).GetMe().GetAwaiter().GetResult();

            Assert.AreEqual(2, reloaded.equipments.Find(e => e.equipmentId == "equipment.ring").level);
            Assert.AreEqual(2, snapshot.equipments.Find(e => e.equipmentId == "equipment.ring").level);
        }

        [Test(Description = "equipments가 없는 기존 저장 파일도 빈 목록으로 읽는다")]
        public void LocalSave_OldFileWithoutEquipments_LoadsEmpty()
        {
            File.WriteAllText(_path, "{\"exp\":5}");

            var save = _store.Load();

            Assert.IsNotNull(save.equipments);
            Assert.IsEmpty(save.equipments);
        }

        private static LocalSave SaveWith(string itemId, int quantity) => new()
        {
            items = new List<ItemAmount> { new() { itemId = itemId, quantity = quantity } }
        };

        [Test(Description = "마법북을 소모하면 소지량이 줄고, 0이 되면 행을 지운다")]
        public void Spend_ReducesQuantity_AndRemovesEmptyRow()
        {
            var save = SaveWith(ItemIds.HatBook, 5);

            LocalItemSpend.Spend(save, ItemIds.HatBook, 2);
            Assert.AreEqual(3, LocalItemSpend.Owned(save, ItemIds.HatBook));

            LocalItemSpend.Spend(save, ItemIds.HatBook, 3);
            Assert.AreEqual(0, LocalItemSpend.Owned(save, ItemIds.HatBook));
            Assert.IsEmpty(save.items);
        }

        [Test(Description = "소지량이 모자라거나 없는 아이템이면 INSUFFICIENT_ITEM으로 거절하고 아무것도 바꾸지 않는다")]
        public void Spend_RejectsWhenInsufficient_WithoutChangingAnything()
        {
            var save = SaveWith(ItemIds.HatBook, 1);

            var notEnough = Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, ItemIds.HatBook, 2));
            var missing = Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, ItemIds.TopBook, 1));

            Assert.AreEqual("INSUFFICIENT_ITEM", notEnough.Code);
            Assert.AreEqual("INSUFFICIENT_ITEM", missing.Code);
            Assert.AreEqual(1, LocalItemSpend.Owned(save, ItemIds.HatBook));
        }

        [Test(Description = "소모량이 0 이하이거나 아이템 ID가 비어 있으면 INVALID_ITEM_COST로 거절한다")]
        public void Spend_RejectsInvalidCost()
        {
            var save = SaveWith(ItemIds.HatBook, 1);

            Assert.AreEqual("INVALID_ITEM_COST", Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, ItemIds.HatBook, 0)).Code);
            Assert.AreEqual("INVALID_ITEM_COST", Assert.Throws<ApiException>(() => LocalItemSpend.Spend(save, "", 1)).Code);
        }
    }
}
