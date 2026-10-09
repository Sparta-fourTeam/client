using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>실제 Stages.json의 웨이브 구성이 약속을 지키는지 본다</summary>
    public class StageWaveCompositionDataTests
    {
        private static readonly GameDataStore Store = new();

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
    }
}
