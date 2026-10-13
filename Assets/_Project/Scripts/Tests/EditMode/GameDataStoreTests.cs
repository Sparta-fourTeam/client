using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Tests
{
    public class GameDataStoreTests
    {

        [Test(Description = "한 전투에서 변경한 스킬 정의가 다음 로딩의 기본 정의를 오염시키지 않는다")]
        public void LoadSkills_ReturnsFreshObjectsEachCall()
        {
            var store = new GameDataStore();
            var first = store.LoadSkills()[0];
            float originalDamage = first.baseStats.cast.baseDamage;
            first.baseStats.cast.baseDamage = originalDamage + 123;

            Assert.AreEqual(originalDamage, store.LoadSkills()[0].baseStats.cast.baseDamage);
        }

        private static WaveDefinition Wave(int monsterA, int countA, int monsterB, int countB) => new()
        {
            Spawns = new List<WaveSpawn>
            {
                new() { MonsterId = monsterA, Count = countA },
                new() { MonsterId = monsterB, Count = countB },
            }
        };

        private static StageDefinition ValidStage()
        {
            var stage = JsonConvert.DeserializeObject<StageDefinition>(
            "{\"Id\":1,\"WallHp\":100,\"Spawn\":{\"IntervalMin\":0.1,\"IntervalMax\":0.5,\"Cooldown\":3},"
            + "\"Waves\":[{\"Spawns\":[{\"MonsterId\":1,\"Count\":3},{\"MonsterId\":2,\"Count\":2}]}]}");
            stage.Waves = Enumerable.Range(0, 20).Select(_ => Wave(1, 3, 2, 2)).ToList();
            return stage;
        }

        [Test]
        public void StageValidate_BadValues_Throw()
        {
            var noHp = ValidStage();
            noHp.WallHp = 0;
            Assert.Throws<InvalidOperationException>(() => noHp.Validate());

            var noWaves = ValidStage();
            noWaves.Waves.Clear();
            Assert.Throws<InvalidOperationException>(() => noWaves.Validate());

            var emptyWave = ValidStage();
            emptyWave.Waves[0].Spawns.Clear();
            Assert.Throws<InvalidOperationException>(() => emptyWave.Validate());

            var zeroCount = ValidStage();
            zeroCount.Waves[0].Spawns[0].Count = 0;
            Assert.Throws<InvalidOperationException>(() => zeroCount.Validate());

            var duplicateInWave = ValidStage();
            duplicateInWave.Waves[0].Spawns[1].MonsterId = duplicateInWave.Waves[0].Spawns[0].MonsterId;
            Assert.Throws<InvalidOperationException>(() => duplicateInWave.Validate());

            var reversedInterval = ValidStage();
            reversedInterval.Spawn.IntervalMin = 1f;
            Assert.Throws<InvalidOperationException>(() => reversedInterval.Validate());
        }

        [Test]
        public void MonsterValidate_BadValues_Throw()
        {
            var monster = new GameDataStore().Monsters.GetOrThrow(1);
            Assert.DoesNotThrow(() => monster.Validate());

            var noHp = JsonConvert.DeserializeObject<MonsterDefinition>(JsonConvert.SerializeObject(monster));
            noHp.Hp = 0;
            Assert.Throws<InvalidOperationException>(() => noHp.Validate());

            var noInterval = JsonConvert.DeserializeObject<MonsterDefinition>(JsonConvert.SerializeObject(monster));
            noInterval.AttackInterval = 0f;
            Assert.Throws<InvalidOperationException>(() => noInterval.Validate());
        }

        [Test]
        public void GeneralCardValidate_BadValues_Throw()
        {
            GeneralCardDefinition Valid() => new GeneralCardDefinition
            {
                Id = "c",
                Name = "카드",
                MaxPicks = 1,
                Effect = new GeneralCardRule { Kind = "wallRepair", Value = 1 },
            };

            Assert.DoesNotThrow(() => Valid().Validate());

            var noPicks = Valid();
            noPicks.MaxPicks = 0;
            Assert.Throws<InvalidOperationException>(() => noPicks.Validate());

            var unknownEffect = Valid();
            unknownEffect.Effect.Kind = "nope";
            Assert.Throws<InvalidOperationException>(() => unknownEffect.Validate());

            var unknownCondition = Valid();
            unknownCondition.Condition = new GeneralCardRule { Kind = "nope" };
            Assert.Throws<InvalidOperationException>(() => unknownCondition.Validate());
        }

        [Test(Description = "강화 비용을 Base·Step으로 줄여 적어도 레벨별 배열로 펼쳐지고, 배열을 적으면 배열이 우선한다")]
        public void Upgrade_ExpandsLinearCostsAndPrefersExplicitArrays()
        {
            var linear = JsonConvert.DeserializeObject<UpgradeDefinition>(
                "{\"UpgradeId\":\"a\",\"MaxLevel\":4,\"CostBase\":100,\"CostStep\":30,\"MaterialItemId\":\"book.arrow\",\"MaterialBase\":2,\"MaterialStep\":3}");
            Assert.That(linear.LevelCosts, Is.EqualTo(new[] { 100, 130, 160, 190 }));
            Assert.That(linear.MaterialCosts, Is.EqualTo(new[] { 2, 5, 8, 11 }));
            linear.Validate();

            var explicitCosts = JsonConvert.DeserializeObject<UpgradeDefinition>(
                "{\"UpgradeId\":\"b\",\"MaxLevel\":2,\"LevelCosts\":[7,9],\"CostBase\":100,\"CostStep\":30}");
            Assert.That(explicitCosts.LevelCosts, Is.EqualTo(new[] { 7, 9 }));
        }

        [Test(Description = "PlayerLevels는 시작값과 증가분으로 레벨별 필요 경험치를 만든다")]
        public void PlayerLevels_BuildsRequiredExpFromBaseAndStep()
        {
            var table = JsonConvert.DeserializeObject<PlayerLevelConfig>("{\"BaseExp\":10,\"StepExp\":5,\"MaxLevel\":4}").ToTable();
            Assert.AreEqual(4, table.MaxLevel);
            Assert.AreEqual(10, table.RequiredToNext(1));
            Assert.AreEqual(20, table.RequiredToNext(3));
            Assert.AreEqual(0, table.RequiredToNext(4));
        }
    }
}
