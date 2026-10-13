using UnityEngine;

namespace Game.Core
{
    public interface IEnemyFactory
    {
        // 몬스터 Id 하나로 적을 만든다. 프리팹, 등급, 공격 방식은 Id로 찾는다.
        // isSummoned: 주기 소환으로 생긴 적이면 true. 사망이 웨이브 게이지에 세어지지 않는다 (분열체는 false)
        Enemy Create(int monsterId, Vector2 spawnPosition, bool isSummoned = false);
    }
}
