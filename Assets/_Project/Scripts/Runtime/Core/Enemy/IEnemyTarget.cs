using Game.Core.Combat;
using UnityEngine;

namespace Game.Core
{
    /// <summary>스킬이 조준하거나 직접 때릴 수 있는 살아 있는 적 한 마리</summary>
    public interface IEnemyTarget : IDamageable
    {
        Vector2 Position { get; }

        /// <summary>죽은 대상은 조준과 피해에서 건너뛴다. 죽음을 모르는 대상(테스트용 대역 등)은 항상 살아 있다</summary>
        bool IsDead => false;

        /// <summary>투사체가 이 대상에 맞으면 관통하지 못하고 멈춘다. 투사체 차단 몬스터가 켠다</summary>
        bool BlocksPierce => false;
    }
}
