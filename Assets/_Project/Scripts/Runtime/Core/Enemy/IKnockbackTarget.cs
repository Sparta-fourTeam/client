using UnityEngine;

namespace Game.Core
{
    public interface IKnockbackTarget
    {
        void ApplyKnockback(Vector2 direction, float distance);
    }
}
