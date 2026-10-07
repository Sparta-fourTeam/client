using System.Collections.Generic;
using Game.Core;

namespace Game.View
{
    /// <summary>스테이지의 등장 몬스터를 표시용 한 줄(아이콘·이름·보스 여부)로 바꾼다</summary>
    public static class MonsterRows
    {
        /// <summary>목록은 표시 표(MonsterDisplayTable)가, 보스 여부는 서버 몬스터 테이블이 정한다.
        /// 표에 있는 ID가 몬스터 테이블에 없으면 데이터가 어긋난 것이라 예외로 알린다. 없는 스테이지(0 포함)는 첫 스테이지로 본다</summary>
        public static List<StageMonsterItem> Build(MonsterDisplayTable display, GameDataStore data, int stageId)
        {
            var rows = new List<StageMonsterItem>();
            if (display == null)
            {
                return rows;
            }

            foreach (int monsterId in display.StageMonsterIds(data.StageOrFirst(stageId).Id))
            {
                var monster = data.Monsters.GetOrThrow(monsterId);
                bool found = display.TryGetMonster(monsterId, out var entry);
                rows.Add(new StageMonsterItem(found ? entry.icon : null, found ? entry.displayName : $"#{monsterId}", monster.IsBoss));
            }

            return rows;
        }
    }
}
