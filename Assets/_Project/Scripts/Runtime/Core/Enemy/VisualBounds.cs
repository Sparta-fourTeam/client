using UnityEngine;

namespace Game.Core
{
    /// <summary>적의 스프라이트(자식 조각 포함)가 차지하는 영역을 읽는 도구. 머리 위에 붙는 표시(체력바, 데미지 숫자)가 같이 쓴다</summary>
    public static class VisualBounds
    {
        /// <summary>root 아래 모든 SpriteRenderer 중 가장 높은 곳의 y. 스프라이트가 없으면 root의 y.
        /// ignore 아래의 렌더러(체력바처럼 이 계산에서 빼야 하는 표시)는 건너뛴다</summary>
        public static float TopY(Transform root, Transform ignore = null)
        {
            float top = root.position.y;
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (ignore != null && renderer.transform.IsChildOf(ignore))
                {
                    continue;
                }

                top = Mathf.Max(top, renderer.bounds.max.y);
            }

            return top;
        }
    }
}
