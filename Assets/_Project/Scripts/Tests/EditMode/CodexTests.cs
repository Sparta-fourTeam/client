using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using Game.Network;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>도감: 전투에서 마주친 몬스터의 등록과 등록 보상 수령. 중복 수령과 미등록 수령이 막혀야 재화가 두 번 나가지 않는다</summary>
    public class CodexTests
    {
        private string _filePath;
        private LocalSaveStore _store;
        private GameDataStore _data;
        private LocalBattleApi _battle;
        private LocalCodexApi _codex;

        [SetUp]
        public void SetUp()
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"codex-test-{Guid.NewGuid()}.json");
            _store = new LocalSaveStore(_filePath);
            _data = new GameDataStore();
            _battle = new LocalBattleApi(_store, _data);
            _codex = new LocalCodexApi(_store, _data);
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }

        /// <summary>해당 등급의 몬스터 ID들. 데이터에 그 등급이 아직 없을 수 있어 호출하는 쪽이 개수를 확인한다</summary>
        private List<int> MonstersOf(EnemyType grade) => _data.Monsters.Values.Where(m => m.GetEnemyType() == grade).Select(m => m.Id).ToList();

        private void Register(params int[] monsterIds)
        {
            var save = _store.Load();
            save.codex.AddRange(monsterIds.Select(id => new CodexRow { monsterId = id }));
            _store.Flush(save);
        }

        [Test(Description = "결과를 제출하면 마주친 몬스터가 도감에 등록되고, 패배해도 등록된다. 없는 ID는 무시한다")]
        public void SubmitResult_RegistersEncounteredMonsters_EvenWhenFailed()
        {
            var save = _store.Load();
            save.wallet.energyStored = _data.Energy.Cost * 2;
            save.wallet.energyUpdatedAt = DateTime.UtcNow.ToString("O");
            _store.Flush(save);
            var start = _battle.StartBattle(1).GetAwaiter().GetResult();
            int normal = MonstersOf(EnemyType.Normal)[0];

            _battle.SubmitResult(new SubmitResultRequest
            {
                battleId = start.battleId,
                cleared = false,
                completedWaves = 0,
                reachedWave = 1,
                encounteredMonsterIds = new List<int> { normal, normal, 987654 }
            }).GetAwaiter().GetResult();

            var codex = _store.Load().codex;
            Assert.AreEqual(1, codex.Count);
            Assert.AreEqual(normal, codex[0].monsterId);
            Assert.IsFalse(codex[0].claimed);
        }

        [Test(Description = "몬스터마다 등급 보상 골드를 한 번씩만 지급하고, 같은 몬스터를 다시 받으려 하면 ALREADY_CLAIMED로 거절한다")]
        public void ClaimMonsterReward_PaysGradeRewardOnce()
        {
            var normals = MonstersOf(EnemyType.Normal);
            Register(normals[0], normals[1]);
            int before = _store.Load().wallet.gold;

            var first = _codex.ClaimMonsterReward(normals[0]).GetAwaiter().GetResult();
            var second = _codex.ClaimMonsterReward(normals[1]).GetAwaiter().GetResult();
            var again = Assert.Throws<ApiException>(() => _codex.ClaimMonsterReward(normals[0]).GetAwaiter().GetResult());

            Assert.AreEqual(before + _data.CodexRewardGold[EnemyType.Normal], first.gold);
            Assert.AreEqual(first.gold + _data.CodexRewardGold[EnemyType.Normal], second.gold);
            Assert.AreEqual("ALREADY_CLAIMED", again.Code);
            Assert.AreEqual(second.gold, _store.Load().wallet.gold);
        }

        [Test(Description = "마주친 적 없는 몬스터의 보상은 NOT_REGISTERED로 거절하고 골드를 바꾸지 않는다")]
        public void ClaimMonsterReward_NotRegistered_Rejected()
        {
            int before = _store.Load().wallet.gold;

            var error = Assert.Throws<ApiException>(() => _codex.ClaimMonsterReward(MonstersOf(EnemyType.Normal)[0]).GetAwaiter().GetResult());

            Assert.AreEqual("NOT_REGISTERED", error.Code);
            Assert.AreEqual(before, _store.Load().wallet.gold);
        }

        [Test(Description = "스테이지의 몬스터 강화는 능력치별로 함께 적용되고, 정수 능력치는 올림하며, 강화가 없으면 기본값 그대로다")]
        public void MonsterBonuses_ScaleEachStatTogether_AndEmptyKeepsBase()
        {
            var stage = new StageDefinition
            {
                MonsterBonuses = new List<MonsterBonus>
                {
                    new() { Stat = "Hp", Percent = 9 },
                    new() { Stat = "Damage", Percent = 50 },
                    new() { Stat = "Speed", Percent = 10 },
                }
            };

            Assert.AreEqual(109, stage.ScaleMonsterHp(100));
            Assert.AreEqual(11, stage.ScaleMonsterHp(10));
            Assert.AreEqual(3, stage.ScaleMonsterDamage(2));
            Assert.AreEqual(0.55f, stage.ScaleMonsterSpeed(0.5f), 1e-5f);
            Assert.AreEqual(37, new StageDefinition().ScaleMonsterHp(37));
            Assert.AreEqual(2, new StageDefinition().ScaleMonsterDamage(2));
        }
    }
}
