using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>죽을 때 지정한 몬스터를 count마리 만들어 달라고 요청한다 (슬라임의 [불안정])</summary>
    public sealed class SplitOnDeath : IPassive
    {
        // 분열체가 한 점에 겹치지 않도록 가로로 벌리는 간격
        private const float Spread = 0.4f;

        private readonly int _childMonsterId;
        private readonly int _count;

        public SplitOnDeath(int childMonsterId, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "분열 수는 1 이상이어야 합니다.");
            }

            _childMonsterId = childMonsterId;
            _count = count;
        }

        public void OnDied(EnemyModel self)
        {
            for (int i = 0; i < _count; i++)
            {
                float offset = (i - (_count - 1) / 2f) * Spread;
                self.RequestSpawn(new EnemySpawnRequest(_childMonsterId, self.Position + new Vector2(offset, 0f)));
            }
        }
    }
}
