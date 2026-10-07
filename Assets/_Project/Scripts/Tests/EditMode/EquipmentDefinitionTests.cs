using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EquipmentDefinitionTests
    {
        private static EquipmentDefinition Valid() => new()
        {
            Id = "equipment.hat",
            MaterialItemId = ItemIds.HatBook,
            UnlockLevel = 2,
            MaxLevel = 3,
            CoinCosts = new[] { 100, 115, 130 },
            MaterialCosts = new[] { 1, 2, 3 },
        };

        [Test(Description = "MockData의 장비 테이블은 로드와 검증을 통과하고, 장비마다 대응하는 재료 아이템이 있다")]
        public void Store_LoadsEquipment_WithMatchingMaterials()
        {
            var store = new GameDataStore();

            foreach (string materialId in ItemIds.EquipmentMaterials)
            {
                string equipmentId = store.Items.GetOrThrow(materialId).TargetId;
                Assert.AreEqual(materialId, store.Equipment.GetOrThrow(equipmentId).MaterialItemId, equipmentId);
            }

            Assert.IsTrue(store.Revisions.ContainsKey("Equipment"));
        }

        [Test(Description = "도달 레벨별 코인·재료 비용을 돌려준다 (첫 강화는 1레벨 비용)")]
        public void CostAt_ReturnsCostToReachLevel()
        {
            var def = Valid();

            Assert.AreEqual(100, def.CoinAt(1));
            Assert.AreEqual(130, def.CoinAt(3));
            Assert.AreEqual(2, def.MaterialAt(2));
        }

        [Test(Description = "올바른 정의는 통과한다")]
        public void Validate_ValidDefinition_Passes()
        {
            Assert.DoesNotThrow(() => Valid().Validate());
        }

        [Test(Description = "비용 배열 길이가 MaxLevel과 다르거나 비용이 0 이하이거나 해금·최대 레벨이 1 미만이면 거절한다")]
        public void Validate_BadValues_Throw()
        {
            var shortCosts = Valid(); shortCosts.CoinCosts = new[] { 100 };
            var zeroCoin = Valid(); zeroCoin.CoinCosts = new[] { 100, 0, 130 };
            var zeroMaterial = Valid(); zeroMaterial.MaterialCosts = new[] { 1, 2, 0 };
            var noUnlock = Valid(); noUnlock.UnlockLevel = 0;
            var noMax = Valid(); noMax.MaxLevel = 0;
            var noMaterial = Valid(); noMaterial.MaterialItemId = " ";
            var noKind = Valid(); noKind.Effects.Add(new EquipmentEffectDefinition { Kind = "" });

            foreach (var bad in new[] { shortCosts, zeroCoin, zeroMaterial, noUnlock, noMax, noMaterial, noKind })
            {
                Assert.Throws<InvalidOperationException>(() => bad.Validate());
            }
        }
    }
}
