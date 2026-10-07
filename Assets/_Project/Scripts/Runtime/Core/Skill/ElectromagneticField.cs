using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public sealed class ElectromagneticField : MonoBehaviour
    {
        private IObjectPool<ElectromagneticField> pool;
        private IEnemyTargetProvider provider;
        private float radius, damage, slowRatio;
        private double remaining, elapsed;
        private readonly List<IEnemyTarget> candidates = new();
        private readonly HashSet<IEnemyTarget> occupants = new();
        private readonly HashSet<IAreaSlowTarget> slowed = new();
        private bool released;
        private int sourceSkill;   // 이 전자기장을 만든 스킬 (피해 집계용)
        private GameObject visual;

        public void Init(IObjectPool<ElectromagneticField> pool, IEnemyTargetProvider provider, Vector2 position,
            float radius, float damage, float duration, float slowRatio)
        {
            sourceSkill = Game.Core.Combat.DamageAttribution.Current;
            ClearSlows();
            this.pool = pool; this.provider = provider;
            this.radius = radius; this.damage = damage; this.slowRatio = slowRatio;
            remaining = duration; elapsed = 0; released = false;
            transform.position = position;
            ShowVisual(radius);
            RefreshOccupants();
        }

        private void ShowVisual(float radius)
        {
            if (visual == null)
            {
                var prefab = Resources.Load<GameObject>("VFX/ElectricField");
                if (prefab != null) { visual = Instantiate(prefab, transform); }
            }
            if (visual == null) { return; }
            visual.transform.localScale = Vector3.one * (radius * 2.1f);
            foreach (var effect in visual.GetComponentsInChildren<ParticleSystem>())
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                effect.Play(true);
            }
        }

        private void Update() => Tick(Time.deltaTime);

        internal void Tick(float deltaTime)
        {
            using var scope = Game.Core.Combat.DamageAttribution.Begin(sourceSkill);
            if (released || pool == null) { return; }
            if (deltaTime < 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { throw new ArgumentOutOfRangeException(nameof(deltaTime)); }
            RefreshOccupants();
            double activeTime = Math.Min(deltaTime, remaining);
            remaining -= activeTime; elapsed += activeTime;
            while (elapsed + 0.0000001 >= 1)
            {
                elapsed = Math.Max(0, elapsed - 1);
                foreach (var target in occupants)
                {
                    if (target is EnemyModel model && model.IsDead) { continue; }
                    target.TakeDamage(Mathf.Max(1, (int)damage));
                }
            }
            if (remaining <= 0)
            {
                released = true; ClearSlows(); pool.Release(this);
            }
        }

        private void RefreshOccupants()
        {
            candidates.Clear(); occupants.Clear();
            provider.GetNearest(transform.position, int.MaxValue, candidates);
            var current = new HashSet<IAreaSlowTarget>();
            foreach (var target in candidates)
            {
                if (target == null || target is EnemyModel model && model.IsDead) { continue; }
                float distanceSquared = (target.Position - (Vector2)transform.position).sqrMagnitude;
                float radiusSquared = radius * radius;
                if ((distanceSquared > radiusSquared && !Mathf.Approximately(distanceSquared, radiusSquared))
                    || !occupants.Add(target)) { continue; }
                if (target is IAreaSlowTarget slow) { slow.SetAreaSlow(this, slowRatio); current.Add(slow); }
            }
            foreach (var old in slowed) { if (!current.Contains(old)) { old.RemoveAreaSlow(this); } }
            slowed.Clear(); foreach (var slow in current) { slowed.Add(slow); }
        }

        private void ClearSlows()
        {
            foreach (var target in slowed) { target.RemoveAreaSlow(this); }
            slowed.Clear(); occupants.Clear();
        }
        private void OnDisable() => ClearSlows();
        private void OnDestroy() => ClearSlows();
    }
}
