using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>적의 스프라이트 영역을 현재 위치에서 읽는다. 임시 렌더러 목록은 재사용한다.</summary>
    public static class VisualBounds
    {
        /// <summary>root 아래 SpriteRenderer의 상단을 읽는다. ignore 아래의 렌더러는 제외한다.</summary>
        public static float TopY(Transform root, Transform ignore = null)
        {
            GetVerticalExtents(root, root.position.y, out float top, out _, ignore);
            return top;
        }

        /// <summary>sprite가 있는 렌더러의 하단을 읽는다. 대상이 없으면 fallback이며 ignore 아래는 제외한다.</summary>
        public static float BottomY(Transform root, float fallback, Transform ignore = null)
        {
            GetVerticalExtents(root, fallback, out _, out float bottom, ignore);
            return bottom;
        }

        /// <summary>상단은 root 높이 이상, 하단은 sprite를 가진 렌더러의 최하단이다. 하단 대상이 없으면 fallback을 쓴다.</summary>
        public static void GetVerticalExtents(Transform root, float fallback, out float top, out float bottom, Transform ignore = null)
        {
            using var lease = ListPool<SpriteRenderer>.Get(out var renderers);
            root.GetComponentsInChildren(false, renderers);
            top = root.position.y;
            bottom = fallback;
            bool foundBottom = false;
            foreach (var renderer in renderers)
            {
                if (ignore != null && renderer.transform.IsChildOf(ignore)) { continue; }
                var bounds = renderer.bounds;
                top = Mathf.Max(top, bounds.max.y);
                if (renderer.sprite == null) { continue; }
                bottom = foundBottom ? Mathf.Min(bottom, bounds.min.y) : bounds.min.y;
                foundBottom = true;
            }
        }
    }
}
