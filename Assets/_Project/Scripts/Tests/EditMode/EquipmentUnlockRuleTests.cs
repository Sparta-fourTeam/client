using System;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class EquipmentUnlockRuleTests
    {
        private GameDataStore _data;
        private string _path;
        private LocalSaveStore _store;

        [SetUp]
        public void SetUp()
        {
            _data = new GameDataStore();
            // 테스트 입력: 사원증만 Lv 1, 나머지는 Lv 2에서 열린다.
            foreach (var material in ItemIds.EquipmentMaterials)
            {
                var target = _data.Items.GetOrThrow(material).TargetId;
                _data.Upgrades.GetOrThrow(target).UnlockLevel = material == ItemIds.EmployeeIdBook ? 1 : 2;
            }
            _path = Path.Combine(Path.GetTempPath(), $"equipment-unlock-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_path);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path)) { File.Delete(_path); }
        }

        private int ExpFor(int level)
        {
            int exp = 0;
            for (int l = 1; l < level; l++) { exp += _data.PlayerLevels.RequiredToNext(l); }
            return exp;
        }

        [Test(Description = "랜덤 장비재료는 열린 장비의 재료만 준다")]
        public void RandomEquipmentReward_OnlyFromUnlockedEquipment()
        {
            var save = _store.Load();
            save.exp = ExpFor(1);
            save.wallet.energyStored = _data.Energy.Cost; save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
            _data.StageRewards.GetOrThrow(1).Items = new() { new() { itemId = ItemIds.RandomEquipmentMaterial, finalAmount = 1 } };
            var api = new LocalBattleApi(_store, _data)
            {
                UnlockedSkillIds = () => new[] { 1 },
                UnlockedEquipmentIds = () => EquipmentUnlockRule.UnlockedTargetIds(_data, _store.Load().exp),
            };

            for (int i = 0; i < 3; i++)
            {
                var issued = api.StartBattle(1, 1).GetAwaiter().GetResult();
                var result = api.SubmitResult(new SubmitResultRequest { battleId = issued.battleId, completedWaves = 4, reachedWave = 5 }).GetAwaiter().GetResult();

                var equipment = result.rewardItems.Where(item => ItemIds.EquipmentMaterials.Contains(item.itemId)).ToList();
                Assert.AreEqual(1, equipment.Sum(item => item.quantity));
                Assert.AreEqual(ItemIds.EmployeeIdBook, equipment[0].itemId);

                save = _store.Load();
                save.exp = ExpFor(1); // 반복 보상 검증 중 계정 레벨은 1로 고정한다.
                save.wallet.energyStored = _data.Energy.Cost; save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
                _store.Flush(save);
            }
        }
    }
}
