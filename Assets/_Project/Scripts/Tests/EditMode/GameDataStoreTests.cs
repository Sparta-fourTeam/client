using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Network;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Tests
{
    public class GameDataStoreTests
    {
        [Test]
        public void TutorialStartSchema_IsPublishedAfterRevisionTwo()
        {
            var api = new LocalDataApi(new GameDataStore());
            var versions = api.GetVersions().GetAwaiter().GetResult();
            var table = api.GetTable("Tutorials").GetAwaiter().GetResult();
            var tutorials = JsonConvert.DeserializeObject<List<TutorialDefinition>>(table.json);

            Assert.That(tutorials.Any(t => t.Start?.Kind == TutorialCondition.FeatureUnlocked), Is.True);
            Assert.That(versions.revisions["Tutorials"], Is.GreaterThan(2),
                "Start를 포함하는 스키마를 이전 개정 2로 배포하면 캐시가 갱신되지 않는다");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidCombatTempo_IsRejectedBeforeEnteringBattle(float timeScale)
        {
            var combat = new CombatConfig { BaseTimeScale = timeScale };
            Assert.Throws<InvalidOperationException>(() => combat.Validate());
        }


        [Test(Description = "한 전투에서 변경한 스킬 정의가 다음 로딩의 기본 정의를 오염시키지 않는다")]
        public void LoadSkills_ReturnsFreshObjectsEachCall()
        {
            var store = new GameDataStore();
            var first = store.LoadSkills()[0];
            float originalDamage = first.baseStats.cast.baseDamage;
            first.baseStats.cast.baseDamage = originalDamage + 123;

            Assert.AreEqual(originalDamage, store.LoadSkills()[0].baseStats.cast.baseDamage);
        }

        private sealed class NumericCardState : IUpgradeState
        {
            public readonly Dictionary<int, int> Levels = new();
            public readonly Dictionary<int, int> Permanent = new();
            public readonly Dictionary<(int, string), int> Counts = new();
            public int GetWeaponLevel(int id) => Levels.TryGetValue(id, out int value) ? value : 0;
            public int GetPermanentWeaponLevel(int id) => Permanent.TryGetValue(id, out int value) ? value : 0;
            public int GetAcquiredCount(int id, string card) => Counts.TryGetValue((id, card), out int value) ? value : 0;
        }

        [Test(Description = "서버 숫자 카드 ID를 직접 읽으며, cardId만으로 소속을 찾고 생략한 count와 다른 스킬·2회·배타 조건이 실제 선택 가능 여부에 적용된다")]
        public void ParseSkills_NumericCardIdsPreserveOptionalAndScopedConditions()
        {
            string json = @"[
                { 'id':13, 'name':'광선', 'castType':'Beam', 'maxLevel':3, 'baseStats':{},
                  'upgrades':[
                    { 'id':92, 'name':'선행', 'effects':[{'kind':'damage','value':20}] },
                    { 'id':100, 'name':'자식 시전', 'effects':[{'kind':'onEvent','trigger':'Start','skillId':14}],
                      'requiredWeaponIds':[16],
                      'requiredCardCounts':[{'cardId':92},{'cardId':117,'count':2}],
                      'exclusions':[{'cardId':118,'belowPermanentLevel':3}] }
                  ] },
                { 'id':14, 'name':'보조 광선', 'castType':'Beam', 'childOnly':true,
                  'maxLevel':1, 'baseStats':{'cast':{'baseDamage':10}}, 'mirrorCards':[{'cardId':92}], 'upgrades':[] },
                { 'id':16, 'name':'집중 광선', 'castType':'Beam', 'maxLevel':3, 'baseStats':{},
                  'upgrades':[
                    { 'id':117, 'name':'선행', 'maxPickCount':2, 'effects':[{'kind':'damage','value':5}] },
                    { 'id':118, 'name':'배타', 'effects':[{'kind':'damage','value':10}] }
                  ] }
            ]";
            var catalog = GameDataStore.ParseSkills(json);
            var card = catalog.Single(w => w.id == 13).upgrades.Single(c => c.id == "100");
            var state = new NumericCardState();
            state.Levels[13] = 1;
            state.Counts[(13, "92")] = 1;
            state.Counts[(16, "117")] = 2;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 13, state), "보유해야 하는 스킬의 서버 ID를 직접 사용한다");
            state.Levels[16] = 1;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(card, 13, state));
            state.Counts[(13, "92")] = 0;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 13, state), "cardId로 같은 스킬을 찾으며 생략한 count는 1회다");
            state.Counts[(13, "92")] = 1;
            state.Counts[(16, "117")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 13, state), "다른 스킬의 명시한 2회 조건을 지킨다");
            state.Counts[(16, "117")] = 2;
            state.Counts[(16, "118")] = 1;
            Assert.IsFalse(UpgradeEligibility.CanAcquire(card, 13, state));
            state.Permanent[13] = 3;
            Assert.IsTrue(UpgradeEligibility.CanAcquire(card, 13, state), "배타 조건은 그 조건을 가진 스킬의 영구 레벨을 사용한다");
            var byId = catalog.ToDictionary(w => w.id);
            var builder = new SkillConfigBuilder(SkillConfig.FromDefinition(byId[14]));
            Assert.IsTrue(builder.TryApplyCatalog(MirroredEffects.For(byId[14], byId, state)));
            Assert.AreEqual(12, builder.Build().Stats.Cast.Damage, 0.0001f, "cardId만으로 다른 스킬에서 고른 카드의 효과를 가져온다");
            foreach (var references in new object[] { card.requiredCardCounts, card.exclusions, byId[14].mirrorCards })
            {
                StringAssert.DoesNotContain("skillId", JsonConvert.SerializeObject(references));
                StringAssert.DoesNotContain("OwnerSkillId", JsonConvert.SerializeObject(references));
            }
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
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<MonsterDefinition>("{\"IsBoss\":true}"), "등급 누락을 일반 몬스터로 추측하지 않는다");
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<MonsterDefinition>("{\"Grade\":\"Unknown\"}"));
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

        [Test]
        public void PlayerLevels_RejectsUnsupportedSizeBeforeAllocatingRows()
        {
            foreach (int size in new[] { PlayerLevelConfig.MaxSupportedLevel + 1, int.MaxValue })
            {
                var config = new PlayerLevelConfig { BaseExp = 1, StepExp = 0, MaxLevel = size };
                Assert.Throws<System.InvalidOperationException>(() => config.ToTable());
            }
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

        [Test(Description = "표시 문구는 클라이언트 표가 id로 채우고, 이름이 없는 스킬·카드는 데이터 오류로 거부한다")]
        public void DisplayTexts_FillNamesByIdAndRejectMissingNames()
        {
            var texts = JsonConvert.DeserializeObject<DisplayTexts>(
                "{\"skills\":{\"1\":{\"name\":\"스킬\",\"desc\":\"설명\"}},\"cards\":{\"a\":{\"name\":\"카드\"}}}");
            string json = "[{\"id\":1,\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[{\"id\":\"a\",\"effects\":[]}]}]";

            var skill = GameDataStore.ParseSkills(json, texts).Single();
            Assert.AreEqual("스킬", skill.name);
            Assert.AreEqual("설명", skill.desc);
            Assert.AreEqual("카드", skill.upgrades[0].name);

            var missing = JsonConvert.DeserializeObject<DisplayTexts>("{\"skills\":{\"1\":{\"name\":\"스킬\"}}}");
            Assert.Throws<InvalidOperationException>(() => GameDataStore.ParseSkills(json, missing));
        }
    }
}
