using UnityEngine;

namespace Game.Core
{
    /// <summary>Spawn geometry and travel budget; independent of damage and visuals.</summary>
    public readonly struct ProjectileLaunchPath
    {
        public Vector3 Start { get; }
        public Vector3 Direction { get; }
        public float Lifetime { get; }
        public int PierceCount { get; }
        public const float RollingWallFrontOffset = .2f;

        private ProjectileLaunchPath(Vector3 start, Vector3 direction, float lifetime, int pierceCount)
        { Start = start; Direction = direction; Lifetime = lifetime; PierceCount = pierceCount; }

        public const float FanHalfAngle = 30f;

        /// <summary>진행 방향 기준 -30도에서 +30도까지 발 수만큼 고르게 퍼지는 부채꼴 (한 발이면 정면).
        /// 맞은 적 말고 노릴 적이 없을 때 분열 조각이 쓴다.</summary>
        public static ProjectileLaunchPath Fan(Vector3 origin, Vector3 direction, int pierceCount, int index, int count)
        {
            float angle = count <= 1 ? 0 : -FanHalfAngle + 2 * FanHalfAngle * index / (count - 1);
            Vector3 rotated = Quaternion.Euler(0, 0, angle) * direction.normalized;
            return new ProjectileLaunchPath(origin, rotated, 3f, pierceCount);
        }

        public static ProjectileLaunchPath Calculate(ProjectilePath path, Vector3 caster, Vector2 target,
            float? wallAttackLineY, float range, float speed, int pierceCount, int index = 0, int count = 1)
        {
            if (path == ProjectilePath.Radial)
            {
                float angle = index * Mathf.PI * 2 / Mathf.Max(1, count);
                return new ProjectileLaunchPath(caster, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0), 3f, pierceCount);
            }

            if (path == ProjectilePath.RollingLane)
            {
                float startY = wallAttackLineY.HasValue ? wallAttackLineY.Value + RollingWallFrontOffset : caster.y;
                return new ProjectileLaunchPath(new Vector3(target.x, startY, caster.z), Vector3.up,
                    range / Mathf.Max(.01f, speed) + .1f, int.MaxValue - 1);
            }
            Vector2 direction = (target - (Vector2)caster).normalized;
            return new ProjectileLaunchPath(caster, new Vector3(direction.x, direction.y, 0), 3f, pierceCount);
        }
    }
}
