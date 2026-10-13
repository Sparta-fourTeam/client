using UnityEngine;

namespace Game.Core
{
    /// <summary>적이 새 적을 만들어 달라고 요청하는 값 (분열, 소환). 만드는 일은 EnemySpawner가 한다</summary>
    public readonly struct EnemySpawnRequest
    {
        public int MonsterId { get; }
        public Vector2 Position { get; }

        /// <summary>주기 소환으로 생긴 요청이면 true. 분열은 false다. 소환체는 웨이브 게이지에 세지 않는다</summary>
        public bool IsSummon { get; }

        public EnemySpawnRequest(int monsterId, Vector2 position, bool isSummon = false)
        {
            MonsterId = monsterId;
            Position = position;
            IsSummon = isSummon;
        }
    }
}
