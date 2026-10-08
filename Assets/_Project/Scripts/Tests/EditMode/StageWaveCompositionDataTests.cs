using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>실제 Stages.json의 웨이브 구성이 약속을 지키는지 본다</summary>
    public class StageWaveCompositionDataTests
    {
        private static readonly GameDataStore Store = new();

        [Test(Description = "모든 스테이지가 20웨이브이고 웨이브마다 몬스터 구성이 있다")]
        public void EveryWave_HasComposition()
        {
            foreach (var stage in Store.Stages.Values)
            {
                Assert.AreEqual(20, stage.Waves.Count, $"스테이지 {stage.Id}");
                foreach (var wave in stage.Waves) { Assert.Greater(wave.EnemyCount, 0); }
            }
        }

        [Test(Description = "엘리트·보스는 웨이브마다 1마리만 적혀 있다 (반복 스폰에 끼지 않는다)")]
        public void EliteAndBoss_AreSingleInEachWave()
        {
            foreach (var stage in Store.Stages.Values)
            {
                foreach (var spawn in stage.Waves.SelectMany(w => w.Spawns).Where(s => s.Type != EnemyType.Normal))
                {
                    Assert.AreEqual(1, spawn.Count, $"스테이지 {stage.Id} 몬스터 {spawn.MonsterId}");
                }
            }
        }

        [Test(Description = "등급은 데이터에 적지 않고 Monsters에서 채워진다")]
        public void SpawnType_IsResolvedFromMonsters()
        {
            foreach (var stage in Store.Stages.Values)
            {
                foreach (var spawn in stage.Waves.SelectMany(w => w.Spawns))
                {
                    Assert.AreEqual(Store.Monsters.GetOrThrow(spawn.MonsterId).GetEnemyType(), spawn.Type);
                }
            }
        }

        [Test(Description = "3스테이지의 보스는 마지막 웨이브에 한 번만 나온다")]
        public void Stage3_BossAppearsOnlyInFinalWave()
        {
            var stage = Store.Stages.GetOrThrow(3);
            var bossWaves = Enumerable.Range(0, stage.Waves.Count)
                .Where(i => stage.Waves[i].Spawns.Any(s => s.Type == EnemyType.Boss)).ToList();

            CollectionAssert.AreEqual(new[] { 19 }, bossWaves);
        }
    }
}
