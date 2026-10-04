using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanCaster : WeaponBase
    {
        private ObjectPool<HitscanEffect> pool;
        private ObjectPool<Projectile> orbPool;
        private readonly ObjectPool<ElectromagneticField> fieldPool;
        private readonly Vector3 originalScale;

        public HitscanCaster(WeaponData data, GameObject prefab, Transform caster, IEnemyTargetProvider targetProvider) : base(data, caster, targetProvider)
        {
            fieldPool = new ObjectPool<ElectromagneticField>(
                () => new GameObject("ElectromagneticField_Prototype").AddComponent<ElectromagneticField>(),
                field => field.gameObject.SetActive(true), field => field.gameObject.SetActive(false),
                field => { if (field != null) { Object.Destroy(field.gameObject); } }, true, 8, 120);
            originalScale = prefab.transform.localScale;
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

                if (stats.FieldDuration > 0)
                {
                    float fieldDamage = (stats.Damage * stats.FieldDamageRatio + stats.FieldFlatDamage) * stats.FieldDamageMultiplier;
                    fieldPool.Get().Init(fieldPool, targetProvider, target.Position, stats.FieldRadius, fieldDamage, stats.FieldDuration, stats.FieldSlowRatio);
                }
                var effect = pool.Get();
                effect.transform.localScale = stats.Form == WeaponForm.JudgementThunder ? originalScale * 1.5f : originalScale;
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
                effect.Init(pool, new Vector3(target.Position.x, target.Position.y, 0f), stats.Damage, onHit, onTargetHit, CreateKillLightningCallback());
            }
        }
        private System.Action<Vector2> CreateKillLightningCallback()
        {
            float damage = stats.Damage * stats.KillLightningRatio;
            if (damage <= 0) { return null; }
            float range = data.baseStats.range;
            return position =>
            {
                var candidates = new System.Collections.Generic.List<IEnemyTarget>();
                targetProvider.GetNearest(position, int.MaxValue, candidates);
                IEnemyTarget nearest = null;
                float nearestDistance = range * range;
                foreach (var candidate in candidates)
                {
                    if (candidate == null || candidate is EnemyModel model && model.IsDead) { continue; }
                    float distance = (candidate.Position - position).sqrMagnitude;
                    if (distance <= nearestDistance) { nearest = candidate; nearestDistance = distance; }
                }
                if (nearest == null) { return; }
                var secondary = pool.Get();
                secondary.transform.localScale = originalScale * 0.5f;
                secondary.Init(pool, nearest.Position, damage, directTarget: nearest);
            };
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
