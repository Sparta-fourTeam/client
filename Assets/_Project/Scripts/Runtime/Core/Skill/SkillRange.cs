using UnityEngine;

namespace Game.Core
{
    public enum SkillRangeShape
    {
        /// <summary>플레이어(시전 위치)를 중심으로 반지름이 사정거리인 반원</summary>
        Arc,
        /// <summary>벽 앞선에서 사정거리만큼 위의 높이에 놓인 가로선</summary>
        HorizontalLine
    }

    /// <summary>스킬 사정거리를 화면에 그리기 위한 정보. 사정거리는 공격이 시전되는 조건(트리거)이지 공격이 닿는 거리가 아니다.</summary>
    public readonly struct SkillRange
    {
        public SkillRangeShape Shape { get; }
        public Vector2 Center { get; }
        public float Radius { get; }
        public float LineY { get; }

        private SkillRange(SkillRangeShape shape, Vector2 center, float radius, float lineY)
        {
            Shape = shape;
            Center = center;
            Radius = radius;
            LineY = lineY;
        }

        public static SkillRange Arc(Vector2 center, float radius) => new SkillRange(SkillRangeShape.Arc, center, radius, 0);

        public static SkillRange Line(float y) => new SkillRange(SkillRangeShape.HorizontalLine, default, 0, y);
    }

    /// <summary>스킬의 사정거리를 재는 방식(시전 판정과 같은 기준)에 맞춰 표시할 모양을 정한다.</summary>
    public static class SkillRangeResolver
    {
        /// <param name="wallAttackLineY">벽 앞선의 높이. 벽이 없으면 null</param>
        public static SkillRange For(SkillData data, float range, Vector2 casterPosition, float? wallAttackLineY) =>
            data.projectilePath == ProjectilePath.RollingLane && wallAttackLineY.HasValue
                ? SkillRange.Line(wallAttackLineY.Value + range)
                : SkillRange.Arc(casterPosition, range);
    }
}
