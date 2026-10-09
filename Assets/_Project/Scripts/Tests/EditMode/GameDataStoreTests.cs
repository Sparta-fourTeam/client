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
        [Test]
        public void Items_AreAvailableToLocalApiAndExcludeCurrencies()
        {
            var store = new GameDataStore();
            Assert.AreEqual("1", store.Items.GetOrThrow(ItemIds.ArrowBook).TargetId);
            Assert.IsTrue(store.Revisions.ContainsKey("Items"));
            Assert.IsNotEmpty(store.RawJson("Items"));
            Assert.IsFalse(store.Items.Contains("gold"));
            Assert.IsFalse(store.Items.Contains("coin"));
        }

        [Test(Description = "스킬 정의는 가변이라 호출마다 새 객체를 돌려준다")]
        public void LoadSkills_ReturnsFreshObjectsEachCall()
        {
            var store = new GameDataStore();

            Assert.AreNotSame(store.LoadSkills()[0], store.LoadSkills()[0]);
        }

        [Test]
        public void StageOrFirst_UnknownOrZero_FallsBackToFirstStage()
        {
            var store = new GameDataStore();

            Assert.AreEqual(store.FirstStageId, store.StageOrFirst(0).Id);
            Assert.AreEqual(store.FirstStageId, store.StageOrFirst(9999).Id);
            Assert.AreEqual(3, store.StageOrFirst(3).Id);
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

        [Test(Description = "등장 몬스터 목록은 웨이브 구성에서 첫 등장 순서로 모은다")]
        public void MonsterIds_CollectedFromWavesInFirstAppearanceOrder()
        {
            var stage = ValidStage();
            stage.Waves[0] = Wave(2, 1, 1, 1);
            stage.Waves[5] = Wave(1, 1, 7, 1);

            CollectionAssert.AreEqual(new[] { 2, 1, 7 }, stage.MonsterIds);
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
    }
}
