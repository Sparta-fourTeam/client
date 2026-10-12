using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Core
{
    /// <summary>적끼리 겹칠 때 화면 아래쪽(발끝이 낮은) 적이 앞에 그려지도록 비주얼 SortingGroup의 순서를 정한다.
    /// 발끝은 Bind 때 스프라이트 아래쪽 끝으로 한 번 재고, 이후에는 위치만 본다.
    /// 순서는 EnemyDepthSorter가 적 전체를 한 줄로 세워 정하고, Bind 직후 첫 순서만 높이로 바로 계산한다.
    /// 순서는 항상 FrontOrder 이하라서 투사체·이펙트(0 이상)는 지금처럼 적 위에, 배경(-500)은 아래에 그려진다</summary>
    [DefaultExecutionOrder(-100)] // 상태이상 이펙트(EnemyStatusEffects)가 같은 프레임에 정해진 몸 순서를 읽도록 먼저 돈다
    public sealed class EnemyDepthSort : MonoBehaviour
    {
        public const int FrontOrder = -2;
        public const int BackOrder = -402;
        private const float MinY = -10f;          // 이 높이(이하)의 발끝이 FrontOrder
        private const float UnitsPerStep = 0.05f; // 순서 한 칸에 해당하는 높이

        private SortingGroup _group;
        private float _footOffset;
        private int _applied = int.MinValue;

        /// <summary>발끝 높이에 해당하는 그리기 순서. 낮을수록 큰 값(앞)이고 [BackOrder, FrontOrder]로 자른다</summary>
        public static int OrderAt(float footY) =>
            Mathf.Clamp(FrontOrder - Mathf.RoundToInt((footY - MinY) / UnitsPerStep), BackOrder, FrontOrder);

        /// <summary>지금 적용된 순서. SortingGroup이 없으면 0</summary>
        public int Order => _group != null ? _group.sortingOrder : 0;

        /// <summary>지금 발끝의 월드 높이</summary>
        public float FootY => transform.position.y + _footOffset;

        /// <summary>자식의 SortingGroup(비주얼)을 찾고 발끝 위치를 잰 뒤 정렬 줄에 넣는다. SortingGroup이 없는 적은 아무것도 하지 않는다</summary>
        public void Bind()
        {
            _group = GetComponentInChildren<SortingGroup>(true);
            _applied = int.MinValue;
            if (_group == null)
            {
                return;
            }

            var shadow = TryGetComponent<EnemyShadow>(out var s) ? s.Shadow : null;
            _footOffset = VisualBounds.BottomY(_group.transform, transform.position.y, shadow) - transform.position.y;
            Apply(OrderAt(FootY));
            EnemyDepthSorter.Add(this);
        }

        private void LateUpdate() => EnemyDepthSorter.SortOncePerFrame();

        private void OnDestroy() => EnemyDepthSorter.Remove(this);

        /// <summary>순서가 바뀔 때만 SortingGroup에 쓴다</summary>
        internal void Apply(int order)
        {
            if (_group == null || order == _applied)
            {
                return;
            }

            _group.sortingOrder = order;
            _applied = order;
        }
    }
}
