using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class ItemContractTests
    {

        [Test]
        public void Profile_SeparatesWalletFromBooks_AndReplacesPreviousInventory()
        {
            var profile = new PlayerProfile(new GameDataStore());
            Assert.AreEqual(0, profile.ItemQuantity("book.example"));
            profile.Apply(new PlayerSnapshot
            {
                gold = 100,
                items = new()
                {
                    new ItemAmount { itemId = ItemIds.ArrowBook, quantity = 2 },
                    new ItemAmount { itemId = ItemIds.FireballBook, quantity = 7 },
                    new ItemAmount { itemId = ItemIds.HatBook, quantity = 3 },
                    new ItemAmount { itemId = ItemIds.RingBook, quantity = 5 }
                }
            });
            Assert.AreEqual(100, profile.Gold);
            Assert.AreEqual(0, profile.ItemQuantity("gold"));
            Assert.AreEqual(2, profile.ItemQuantity("book.arrow"));
            Assert.AreEqual(7, profile.ItemQuantity("book.fireball"));
            Assert.AreEqual(3, profile.ItemQuantity("book.hat"));
            Assert.AreEqual(5, profile.ItemQuantity("book.ring"));
            Assert.AreEqual(0, profile.ItemQuantity("unknown"));
            profile.Apply(new PlayerSnapshot { gold = 50 });
            Assert.AreEqual(50, profile.Gold);
            Assert.AreEqual(0, profile.ItemQuantity("book.arrow"));
            Assert.AreEqual(0, profile.ItemQuantity("book.fireball"));
            Assert.AreEqual(0, profile.ItemQuantity("book.hat"));
            Assert.AreEqual(0, profile.ItemQuantity("book.ring"));
        }

        [Test(Description = "장비 대상은 강화의 재료 연결에서 찾고, 중복·불일치·없는 재료로는 일부 대상을 바꾸지 않는다")]
        public void EquipmentTargets_AreInferredAndInvalidLinksDoNotPartiallyApply()
        {
            var material = new ItemDefinition { Id = "material", Name = "재료" };
            var items = new Dictionary<string, ItemDefinition> { [material.Id] = material };
            var upgrade = new UpgradeDefinition { UpgradeId = "equipment.a", MaterialItemId = material.Id };
            var duplicate = new UpgradeDefinition { UpgradeId = "equipment.b", MaterialItemId = material.Id };
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ResolveEquipmentMaterialTargets(new[] { upgrade, duplicate }, items));
            Assert.IsNull(material.TargetId);
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ResolveEquipmentMaterialTargets(new[] { upgrade }, new Dictionary<string, ItemDefinition>()));
            material.TargetId = "equipment.other";
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ResolveEquipmentMaterialTargets(new[] { upgrade }, items));
            Assert.AreEqual("equipment.other", material.TargetId);
            material.TargetId = null;
            GameDataStore.ResolveEquipmentMaterialTargets(new[] { upgrade }, items);
            Assert.IsNull(JObject.Parse(JsonConvert.SerializeObject(material))["TargetId"], "계산한 장비 대상은 다시 JSON에 싣지 않는다");
            var skill = new ItemDefinition { Id = "skill.material", Name = "스킬 재료", TargetId = "13" };
            Assert.AreEqual("13", (string)JObject.Parse(JsonConvert.SerializeObject(skill))["TargetId"], "스킬 재료의 독립 대상은 유지한다");
        }

        [Test]
        public void LocalInventory_LoadsLegacySave_AndRoundTripsIntoPlayerSnapshot()
        {
            var path = Path.Combine(Path.GetTempPath(), $"item-contract-{Guid.NewGuid()}.json");
            try
            {
                File.WriteAllText(path, "{\"wallet\":{\"gold\":123,\"energyStored\":5,\"energyUpdatedAt\":\"2026-10-06T00:00:00Z\"}}");
                var store = new LocalSaveStore(path);
                var save = store.Load();
                Assert.IsEmpty(save.items);
                Assert.AreEqual(123, save.wallet.gold);
                save.items.Add(new ItemAmount { itemId = "book.example", quantity = 4 });
                save.stageProgress[0].clearRating = 2;
                save.stageProgress[0].claimedRating = 1;
                store.Flush(save);
                var storedProgress = JObject.Parse(File.ReadAllText(path))["stageProgress"][0];
                Assert.IsNull(storedProgress["IsCleared"]);
                Assert.IsNull(storedProgress["HasUnclaimedReward"]);
                var snapshot = new LocalPlayerApi(store).GetMe().GetAwaiter().GetResult();
                Assert.AreEqual(123, snapshot.gold);
                Assert.AreEqual(5, snapshot.energyStored);
                Assert.AreEqual("2026-10-06T00:00:00Z", snapshot.energyUpdatedAt);
                Assert.AreEqual(1, snapshot.items.Count);
                Assert.AreEqual("book.example", snapshot.items[0].itemId);
                Assert.AreEqual(4, snapshot.items[0].quantity);
                Assert.IsTrue(snapshot.stageProgress[0].IsCleared);
                Assert.IsTrue(snapshot.stageProgress[0].HasUnclaimedReward);
            }
            finally
            {
                if (File.Exists(path)) { File.Delete(path); }
            }
        }
    }
}
