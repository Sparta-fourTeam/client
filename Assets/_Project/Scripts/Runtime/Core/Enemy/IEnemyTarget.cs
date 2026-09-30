using Game.Core.Combat;
using UnityEngine;

namespace Game.Core
{
    /// <summary>스킬이 조준하거나 직접 때릴 수 있는 살아 있는 적 한 마리</summary>
    public interface IEnemyTarget : IDamageable
    {
        Vector2 Position { get; }
    }
}
