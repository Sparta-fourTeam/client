using System;
using System.Linq;
using Game.Core;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Tests
{
    public class GameDataStoreTests
    {
        [Test(Description = "MockData의 몬스터/스테이지/업그레이드/에너지 테이블을 읽어온다")]
        public void Constructor_LoadsAllTables()
        {
            var store = new GameDataStore();

            Assert.AreEqual(999, store.Monsters.GetOrThrow(1).Hp);
            Assert.IsTrue(store.Monsters.GetOrThrow(100).IsBoss);
            Assert.AreEqual(100, store.Stages.GetOrThrow(1).ClearGold);
            Assert.AreEqual(3, store.Upgrades.GetOrThrow("atk").MaxLevel);
            Assert.AreEqual(5, store.Energy.Cost);
        }

        [Test(Description = "UpgradeDefinition.CostAt은 1부터 시작하는 레벨로 LevelCosts를 찾는다")]
        public void UpgradeDefinition_CostAt_UsesOneBasedLevel()
        {
            var store = new GameDataStore();

            Assert.AreEqual(100, store.Upgrades.GetOrThrow("atk").CostAt(1));
            Assert.AreEqual(500, store.Upgrades.GetOrThrow("atk").CostAt(3));
        }

        [Test(Description = "스킬 테이블도 스토어가 들고 있어 버전 목록과 원문 조회에 나온다")]
        public void Weapons_AreManagedLikeOtherTables()
        {
            var store = new GameDataStore();

            Assert.IsTrue(store.Revisions.ContainsKey("Weapons"));
            Assert.IsNotEmpty(store.RawJson("Weapons"));
            Assert.IsTrue(store.LoadWeapons().Any(w => w.id == 1));
        }

        [Test(Description = "스킬 정의는 가변이라 호출마다 새 객체를 돌려준다")]
        public void LoadWeapons_ReturnsFreshObjectsEachCall()
        {
            var store = new GameDataStore();

            Assert.AreNotSame(store.LoadWeapons()[0], store.LoadWeapons()[0]);
        }

        [Test(Description = "방벽 체력, 스폰, 웨이브가 스테이지 행에서 온다")]
        public void Stages_CarryWallHpSpawnAndWaves()
        {
            var stage = new GameDataStore().Stages.GetOrThrow(1);

            Assert.AreEqual(100, stage.WallHp);
            Assert.AreEqual(0.5f, stage.Spawn.IntervalMax);
            Assert.AreEqual(3, stage.Waves.Count);
            Assert.AreEqual(2, stage.Waves[1].MaxEliteCount);
            Assert.AreEqual(1, stage.Waves[2].MaxBossCount);
        }

        [Test]
        public void StageOrFirst_UnknownOrZero_FallsBackToFirstStage()
        {
            var store = new GameDataStore();

            Assert.AreEqual(store.FirstStageId, store.StageOrFirst(0).Id);
            Assert.AreEqual(store.FirstStageId, store.StageOrFirst(9999).Id);
            Assert.AreEqual(3, store.StageOrFirst(3).Id);
        }

        [Test]
        public void SpawnConfig_FromDefinition_CopiesValues()
        {
            var config = EnemySpawnConfig.From(new GameDataStore().Stages.GetOrThrow(1).Spawn);

            Assert.AreEqual(0.1f, config.SpawnIntervalMin);
            Assert.AreEqual(3f, config.SpawnCooldown);
            Assert.AreEqual(0.2f, config.EliteSpawnChance);
        }

        private static StageDefinition ValidStage() => JsonConvert.DeserializeObject<StageDefinition>(
            "{\"Id\":1,\"WallHp\":100,\"Spawn\":{\"IntervalMin\":0.1,\"IntervalMax\":0.5,\"Cooldown\":3,\"EliteChance\":0.2},"
            + "\"Waves\":[{\"EnemyCount\":5,\"MaxEliteCount\":0,\"MaxBossCount\":0}]}");

        [Test]
        public void StageValidate_ValidStage_Passes()
        {
            Assert.DoesNotThrow(() => ValidStage().Validate());
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
            emptyWave.Waves[0].EnemyCount = 0;
            Assert.Throws<InvalidOperationException>(() => emptyWave.Validate());

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
    }
}
