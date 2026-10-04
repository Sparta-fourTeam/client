using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public static class AreaDamage
    {
        public static int Apply(IEnemyTargetProvider provider, Vector2 center, float radius, float damage, System.Action<IEnemyTarget> onHit = null)
        {
            if (provider == null || radius <= 0 || damage <= 0 || float.IsNaN(radius) || float.IsInfinity(radius)
                || float.IsNaN(damage) || float.IsInfinity(damage))
            {
                return 0;
            }

            var targets = new List<IEnemyTarget>();
            provider.GetNearest(center, int.MaxValue, targets);
            var hit = new HashSet<IEnemyTarget>();
            foreach (var target in targets)
            {
                if (target == null || (target.Position - center).sqrMagnitude > radius * radius || !hit.Add(target))
                {
                    continue;
                }

                target.TakeDamage(Mathf.Max(1, (int)damage));
                onHit?.Invoke(target);
            }
            return hit.Count;
        }
    }
}
