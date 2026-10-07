using System.Linq;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class StageMonsterTests
    {
        private static MonsterDisplayTable Table(params int[] ids)
        {
            var table = ScriptableObject.CreateInstance<MonsterDisplayTable>();
            table.Configure(ids.Select(id => new MonsterDisplayTable.MonsterEntry { monsterId = id, displayName = $"요마{id}" }).ToArray());
            return table;
        }

        [Test(Description = "스테이지의 등장 몬스터는 Stages.json의 MonsterIds 순서 그대로, 몬스터 정의와 함께 나온다")]
        public void StageMonsters_FollowStageMonsterIds()
        {
            var store = new GameDataStore();

            var monsters = store.StageMonsters(3);

            CollectionAssert.AreEqual(store.Stages.GetOrThrow(3).MonsterIds, monsters.Select(m => m.Id).ToList());
            Assert.IsTrue(monsters.Any(m => m.IsBoss));
        }

        [Test(Description = "스테이지마다 목록이 다르고, 없는 스테이지(0 포함)는 첫 스테이지로 본다")]
        public void StageMonsters_DifferPerStage_AndFallBackToFirst()
        {
            var store = new GameDataStore();

            Assert.AreNotEqual(store.StageMonsters(1).Count, store.StageMonsters(3).Count);
            CollectionAssert.AreEqual(store.StageMonsters(store.FirstStageId).Select(m => m.Id), store.StageMonsters(0).Select(m => m.Id));
        }

        [Test(Description = "표시용 줄은 서버 목록 순서·보스 여부에 표시 표의 이름을 붙인다")]
        public void Build_UsesServerListAndDisplayNames()
        {
            var store = new GameDataStore();
            var ids = store.Stages.GetOrThrow(3).MonsterIds;

            var rows = MonsterRows.Build(Table(ids.ToArray()), store, 3);

            CollectionAssert.AreEqual(ids.Select(id => $"요마{id}").ToList(), rows.Select(r => r.Name).ToList());
            CollectionAssert.AreEqual(ids.Select(id => store.Monsters.GetOrThrow(id).IsBoss).ToList(), rows.Select(r => r.IsBoss).ToList());
        }

        [Test(Description = "표시 표가 없거나 표에 없는 몬스터는 아이콘 없이 #ID로 보여주고, 목록 자체는 서버 기준으로 나온다")]
        public void Build_FallsBackWhenDisplayMissing()
        {
            var store = new GameDataStore();
            int first = store.StageMonsters(1)[0].Id;

            var noTable = MonsterRows.Build(null, store, 1);
            var emptyTable = MonsterRows.Build(Table(), store, 1);

            Assert.AreEqual(store.StageMonsters(1).Count, noTable.Count);
            Assert.AreEqual($"#{first}", noTable[0].Name);
            Assert.AreEqual($"#{first}", emptyTable[0].Name);
            Assert.IsNull(noTable[0].Icon);
        }
    }
}
