using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanCaster : WeaponBase
    {
        private ObjectPool<HitscanEffect> pool;
        private ObjectPool<Projectile> orbPool;

        public HitscanCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider) : base(data, caster, targetProvider)
        {
            var secondary = prefab.GetComponent<HitscanEffect>()?.SecondaryProjectilePrefab;
            if (secondary != null)
            {
                orbPool = new ObjectPool<Projectile>(
                    () => Object.Instantiate(secondary).GetComponent<Projectile>(),
                    p => p.gameObject.SetActive(true), p => p.gameObject.SetActive(false),
                    p => { if (p != null) { Object.Destroy(p.gameObject); } }, true, 6, 120);
            }
            pool = new ObjectPool<HitscanEffect>(
                createFunc: () => Object.Instantiate(prefab).GetComponent<HitscanEffect>(),
                actionOnGet: p => p.gameObject.SetActive(true),
                actionOnRelease: p => p.gameObject.SetActive(false),
                actionOnDestroy: p => { if (p != null) { Object.Destroy(p.gameObject); } },
                collectionCheck: true,
                defaultCapacity: 5,
                maxSize: 20
            );
        }

        protected override void OnFire()
        {
            var targets = FindTargets(data.baseStats.range, stats.ProjectileCount);
            if (targets.Count == 0) { return; }

            for (int i = 0; i < stats.ProjectileCount; i++)
            {
                var target = targets[i % targets.Count];

                var effect = pool.Get();
                float explosionRadius = stats.ExplosionRadius;
                float explosionDamage = stats.ExplosionDamage;
                System.Action<Vector2> onHit = explosionRadius > 0
                    ? position => AreaDamage.Apply(targetProvider, position, explosionRadius, explosionDamage)
                    : null;
                onHit += CreateOrbCallback(target);
                float duration = stats.ParalysisDuration;
                float chance = data.baseStats.paralysisChance;
                System.Action<Enemy> onTargetHit = duration > 0
                    ? enemy => { if (StatusProc.Roll(chance)) { enemy.ApplyParalysis(duration); } }
                : null;
                effect.Init(pool, new Vector3(target.Position.x, target.Position.y, 0f), stats.Damage, onHit, onTargetHit);
            }
        }
        private System.Action<Vector2> CreateOrbCallback(IEnemyTarget sourceTarget)
        {
            int count = stats.SplitCount;
            if (orbPool == null || count <= 0) { return null; }
            float damage = stats.Damage * 0.5f * stats.ShardDamageMultiplier;
            float paralysis = stats.AuxiliaryParalysisDuration;
            float chance = data.baseStats.paralysisChance;
            return position =>
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2 / count;
                    var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    orbPool.Get().Init(orbPool, position, direction, damage, 10, 3, targetProvider,
                        ignoredTarget: sourceTarget, paralysisDuration: paralysis, paralysisChance: chance);
                }
            };
        }
    }
}
