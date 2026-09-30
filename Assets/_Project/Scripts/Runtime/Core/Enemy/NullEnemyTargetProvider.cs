using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>실제 구현이 들어오기 전까지 항상 적이 없다고 답하는 스텁. EnemySpawner가 IEnemyTargetProvider를 구현하면 등록을 교체한다</summary>
    public sealed class NullEnemyTargetProvider : IEnemyTargetProvider
    {
        public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
        {
            results.Clear();
            return 0;
        }
    }
}
