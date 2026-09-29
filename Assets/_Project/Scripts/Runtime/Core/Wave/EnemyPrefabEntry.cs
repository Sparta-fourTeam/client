using System;

namespace Game.Core
{
    [Serializable]
    public struct EnemyPrefabEntry
    {
        public EnemyType Type;
        public Enemy Prefab;
        public float Speed; // 해당 프리팹의 이동 속도
    }
}
