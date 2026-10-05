using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public static class LightningOrbSpawner
    {
        public static System.Action<Vector2> CreateCallback(WeaponStats stats, WeaponBaseStats definition, IEnemyTargetProvider targetProvider, ObjectPool<Projectile> orbPool, IEnemyTarget sourceTarget)
        {
            var current = stats;
            int count = current.Secondary.Count;
            if (orbPool == null || count <= 0) { return null; }
            float damage = current.Cast.Damage * 0.5f * current.Secondary.DamageMultiplier;
            float paralysis = current.Secondary.ParalysisDuration;
            float chance = definition.paralysisChance;
            return position =>
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2 / count;
                    var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    orbPool.Get().Init(orbPool, new ProjectileSpawnSettings
                    {
                        StartPos = position,
                        Direction = direction,
                        Damage = damage,
                        Speed = 10,
                        Lifetime = 3,
                        TargetProvider = targetProvider,
                        IgnoredTarget = sourceTarget,
                        ParalysisDuration = paralysis,
                        ParalysisChance = chance
                    });
                }
            };
        }
    }
}
