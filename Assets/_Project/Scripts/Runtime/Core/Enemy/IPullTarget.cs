using UnityEngine;

namespace Game.Core
{
    /// <summary>장판의 끌어당기기를 받는 대상. 밀치기(IKnockbackTarget)는 항상 위로만 밀지만 끌어당기기는 방향 그대로 움직인다</summary>
    public interface IPullTarget
    {
        void ApplyPull(Vector2 direction, float distance);
    }
}
