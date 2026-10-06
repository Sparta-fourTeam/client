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

        [Test]
        public void Items_AreAvailableToLocalApiAndExcludeCurrencies()
        {
            var store = new GameDataStore();
            Assert.AreEqual("1", store.Items.GetOrThrow(ItemIds.ArrowBook).TargetId);
            Assert.AreEqual("무기 마법북", store.Items.GetOrThrow(ItemIds.WeaponBook).Name);
            Assert.IsTrue(store.Revisions.ContainsKey("Items"));
            Assert.IsNotEmpty(store.RawJson("Items"));
            Assert.IsFalse(store.Items.Contains("gold"));
            Assert.IsFalse(store.Items.Contains("coin"));
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

            Assert.IsTrue(store.Revisions.ContainsKey("Skills"));
            Assert.IsNotEmpty(store.RawJson("Skills"));
            Assert.IsTrue(store.LoadSkills().Any(w => w.id == 1));
        }

        [Test(Description = "스킬 정의는 가변이라 호출마다 새 객체를 돌려준다")]
        public void LoadSkills_ReturnsFreshObjectsEachCall()
        {
            var store = new GameDataStore();

            Assert.AreNotSame(store.LoadSkills()[0], store.LoadSkills()[0]);
        }

        [Test(Description = "방벽 체력, 스폰, 웨이브가 스테이지 행에서 온다")]
        public void Stages_CarryWallHpSpawnAndWaves()
        {
            var stage = new GameDataStore().Stages.GetOrThrow(1);

            Assert.AreEqual(100, stage.WallHp);
            Assert.AreEqual(0.5f, stage.Spawn.IntervalMax);
            Assert.AreEqual(20, stage.Waves.Count);
            Assert.AreEqual(2, stage.Waves[1].MaxEliteCount);
            Assert.AreEqual(1, stage.Waves[19].MaxBossCount);
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

        private static StageDefinition ValidStage()
        {
            var stage = JsonConvert.DeserializeObject<StageDefinition>(
            "{\"Id\":1,\"WallHp\":100,\"Spawn\":{\"IntervalMin\":0.1,\"IntervalMax\":0.5,\"Cooldown\":3,\"EliteChance\":0.2},"
            + "\"Waves\":[{\"EnemyCount\":5,\"MaxEliteCount\":0,\"MaxBossCount\":0}]}");
            stage.Waves = Enumerable.Range(0, 20).Select(_ => new WaveDefinition { EnemyCount = 5 }).ToList();
            return stage;
        }

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

        [Test(Description = "일반 카드 테이블에 방벽 수리가 조건과 효과와 함께 있다")]
        public void GeneralCards_HaveWallRepairWithConditionAndEffect()
        {
            var card = new GameDataStore().GeneralCards.Single(c => c.Id == "wall_repair");

            Assert.IsTrue(card.Forced);
            Assert.AreEqual("wallHpBelow", card.Condition.Kind);
            Assert.AreEqual(50f, card.Condition.Value);
            Assert.AreEqual("wallRepair", card.Effect.Kind);
            Assert.AreEqual(20f, card.Effect.Value);
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
