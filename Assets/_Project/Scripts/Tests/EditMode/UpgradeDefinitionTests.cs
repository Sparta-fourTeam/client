using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class UpgradeDefinitionTests
    {
        private static UpgradeDefinition Valid() => new()
        {
            UpgradeId = "equipment.hat",
            MaterialItemId = ItemIds.HatBook,
            UnlockLevel = 2,
            MaxLevel = 3,
            LevelCosts = new[] { 100, 115, 130 },
            MaterialCosts = new[] { 1, 2, 3 },
        };

        [Test(Description = "MockData의 장비 테이블은 로드와 검증을 통과하고, 장비마다 대응하는 재료 아이템이 있다")]
        public void Store_LoadsEquipment_WithMatchingMaterials()
        {
            var store = new GameDataStore();

            foreach (string materialId in ItemIds.EquipmentMaterials)
            {
                string equipmentId = store.Items.GetOrThrow(materialId).TargetId;
                Assert.AreEqual(materialId, store.Upgrades.GetOrThrow(equipmentId).MaterialItemId, equipmentId);
            }

            Assert.IsTrue(store.Revisions.ContainsKey("Upgrades"));
        }

        [Test(Description = "도달 레벨별 코인·재료 비용을 돌려준다 (첫 강화는 1레벨 비용)")]
        public void CostAt_ReturnsCostToReachLevel()
        {
            var def = Valid();

            Assert.AreEqual(100, def.CostAt(1));
            Assert.AreEqual(130, def.CostAt(3));
            Assert.AreEqual(2, def.MaterialAt(2));
        }

        [Test(Description = "비용 배열 길이가 MaxLevel과 다르거나 비용이 0 이하이거나 해금·최대 레벨이 1 미만이면 거절한다")]
        public void Validate_BadValues_Throw()
        {
            var shortCosts = Valid(); shortCosts.LevelCosts = new[] { 100 };
            var zeroCoin = Valid(); zeroCoin.LevelCosts = new[] { 100, 0, 130 };
            var zeroMaterial = Valid(); zeroMaterial.MaterialCosts = new[] { 1, 2, 0 };
            var noUnlock = Valid(); noUnlock.UnlockLevel = 0;
            var noMax = Valid(); noMax.MaxLevel = 0;
            var noMaterial = Valid(); noMaterial.MaterialItemId = " ";
            var noKind = Valid(); noKind.Effects.Add(new UpgradeStatEffect { Kind = "" });

            foreach (var bad in new[] { shortCosts, zeroCoin, zeroMaterial, noUnlock, noMax, noMaterial, noKind })
            {
                Assert.Throws<InvalidOperationException>(() => bad.Validate());
            }
        }

        [Test(Description = "코인만 쓰는 기존 강화(atk)는 재료·해금 레벨 없이 그대로 유효하다")]
        public void CoinOnlyUpgrade_StaysValid_AndAlwaysUnlocked()
        {
            var atk = new GameDataStore().Upgrades.GetOrThrow("atk");

            Assert.DoesNotThrow(() => atk.Validate());
            Assert.IsFalse(atk.UsesMaterial);
            Assert.AreEqual(1, atk.UnlockLevel);
        }

        [Test(Description = "재료 ID 없이 재료 비용만 있으면 거절한다")]
        public void Validate_MaterialCostsWithoutItem_Throws()
        {
            var def = Valid();
            def.MaterialItemId = null;

            Assert.Throws<InvalidOperationException>(() => def.Validate());
        }
    }
}
