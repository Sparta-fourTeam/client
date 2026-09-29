
using UnityEngine;

namespace Game.Core
{
    public interface IEnemyFactory
    {
        // Enemy데이터를 화면에 표시할 오브젝트를 생성하고 연결
        EnemyModel Create(Vector2 spawnPosition, EnemyType type);
    }
}
