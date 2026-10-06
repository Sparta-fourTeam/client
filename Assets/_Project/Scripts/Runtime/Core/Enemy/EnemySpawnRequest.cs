using UnityEngine;

namespace Game.Core
{
    /// <summary>적이 새 적을 만들어 달라고 요청하는 값 (분열, 소환). 만드는 일은 EnemySpawner가 한다</summary>
    public readonly struct EnemySpawnRequest
    {
        public int MonsterId { get; }
        public Vector2 Position { get; }

        public EnemySpawnRequest(int monsterId, Vector2 position)
        {
            MonsterId = monsterId;
            Position = position;
        }
    }
}
