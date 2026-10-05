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

        public static ProjectileLaunchPath Calculate(ProjectilePath path, Vector3 caster, Vector2 target,
            float? wallAttackLineY, float range, float speed, int pierceCount)
        {
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
