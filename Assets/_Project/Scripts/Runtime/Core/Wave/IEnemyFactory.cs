
using UnityEngine;

namespace Game.Core
{
    public interface IEnemyFactory
    {
        // Enemy데이터를 화면에 표시할 오브젝트를 생성하고 연결
        Enemy Create(Vector2 spawnPosition, EnemyType type);

        // 분열·소환처럼 몬스터를 Id로 직접 지정해 만든다. 생긴 적의 사망은 웨이브 게이지에 세지 않는다
        Enemy CreateByMonsterId(int monsterId, Vector2 spawnPosition);
    }
}
