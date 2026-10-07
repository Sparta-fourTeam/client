using System.Collections.Generic;
using Game.Core;

namespace Game.View
{
    /// <summary>스테이지의 등장 몬스터를 표시용 한 줄(아이콘·이름·보스 여부)로 바꾼다</summary>
    public static class MonsterRows
    {
        /// <summary>목록과 보스 여부는 서버 테이블(Stages의 MonsterIds, Monsters의 IsBoss)이, 이름·아이콘은 표시 표(MonsterDisplayTable)가 정한다.
        /// 표가 없거나 표에 없는 몬스터는 아이콘 없이 "#ID"로 보여준다. 없는 스테이지(0 포함)는 첫 스테이지로 본다</summary>
        public static List<StageMonsterItem> Build(MonsterDisplayTable display, GameDataStore data, int stageId)
        {
            var rows = new List<StageMonsterItem>();
            foreach (var monster in data.StageMonsters(stageId))
            {
                MonsterDisplayTable.MonsterEntry entry = null;
                bool found = display != null && display.TryGetMonster(monster.Id, out entry);
                rows.Add(new StageMonsterItem(found ? entry.icon : null, found ? entry.displayName : $"#{monster.Id}", monster.IsBoss));
            }

            return rows;
        }
    }
}
