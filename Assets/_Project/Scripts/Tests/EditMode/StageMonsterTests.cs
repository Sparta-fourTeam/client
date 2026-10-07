using System.Linq;
using Game.Core;
using Game.View;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class StageMonsterTests
    {
        private static MonsterDisplayTable Table(params (int stageId, int[] ids)[] stages)
        {
            var table = ScriptableObject.CreateInstance<MonsterDisplayTable>();
            table.Configure(
                new[] { 1, 2, 11, 12, 100 }.Select(id => new MonsterDisplayTable.MonsterEntry { monsterId = id, displayName = $"요마{id}" }).ToArray(),
                stages.Select(s => new MonsterDisplayTable.StageEntry { stageId = s.stageId, monsterIds = s.ids }).ToArray());
            return table;
        }

        [Test(Description = "등장 몬스터는 표시 표의 스테이지 목록 순서 그대로, 이름과 보스 여부(서버 몬스터 테이블)와 함께 나온다")]
        public void Build_FollowsStageListOrder()
        {
            var store = new GameDataStore();

            var rows = MonsterRows.Build(Table((1, new[] { 1, 2 }), (3, new[] { 100, 1 })), store, 3);

            CollectionAssert.AreEqual(new[] { "요마100", "요마1" }, rows.Select(r => r.Name).ToList());
            CollectionAssert.AreEqual(new[] { true, false }, rows.Select(r => r.IsBoss).ToList());
        }

        [Test(Description = "스테이지마다 목록이 다르고, 없는 스테이지(0 포함)는 첫 스테이지로 본다")]
        public void Build_DiffersPerStage_AndFallsBackToFirst()
        {
            var store = new GameDataStore();
            var table = Table((1, new[] { 1, 2 }), (3, new[] { 1, 2, 11 }));

            Assert.AreEqual(2, MonsterRows.Build(table, store, 1).Count);
            Assert.AreEqual(3, MonsterRows.Build(table, store, 3).Count);
            Assert.AreEqual(2, MonsterRows.Build(table, store, 0).Count);
        }

        [Test(Description = "표시 표가 없거나 스테이지가 등록되지 않았으면 빈 목록이다")]
        public void Build_EmptyWhenNoTableOrNoStage()
        {
            var store = new GameDataStore();

            Assert.IsEmpty(MonsterRows.Build(null, store, 1));
            Assert.IsEmpty(MonsterRows.Build(Table(), store, 1));
        }

        [Test(Description = "표에 있는 몬스터 ID가 서버 몬스터 테이블에 없으면 데이터 불일치로 거절한다")]
        public void Build_RejectsUnknownMonsterId()
        {
            var store = new GameDataStore();

            Assert.Throws<ApiException>(() => MonsterRows.Build(Table((1, new[] { 9999 })), store, 1));
        }
    }
}
