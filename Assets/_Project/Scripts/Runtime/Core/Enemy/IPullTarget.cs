using UnityEngine;

namespace Game.Core
{
    /// <summary>장판의 끌어당기기를 받는 대상. 밀치기(IKnockbackTarget)는 항상 위로만 밀지만 끌어당기기는 방향 그대로 움직인다</summary>
    public interface IPullTarget
    {
        /// <param name="direction">현재 위치에서 당길 중심까지의 벡터. 단위 방향이 아니라 남은 거리도 포함한다.</param>
        /// <param name="distance">이번 적중이 당길 최대 거리.</param>
        void ApplyPull(Vector2 direction, float distance);
    }
}
