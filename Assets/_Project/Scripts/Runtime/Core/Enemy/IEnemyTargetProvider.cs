using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>스킬이 살아 있는 적을 찾는 창구. 구현은 적/웨이브 쪽(EnemySpawner)이 맡고, 스킬은 이 인터페이스만 안다</summary>
    public interface IEnemyTargetProvider
    {
        /// <summary>from에서 가까운 순으로 최대 count마리를 results에 채우고 채운 수를 돌려준다. results는 먼저 비운다</summary>
        int GetNearest(Vector2 from, int count, List<IEnemyTarget> results);
    }
}
