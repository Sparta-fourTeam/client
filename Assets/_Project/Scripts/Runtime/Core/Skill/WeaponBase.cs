using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public abstract class WeaponBase : ISkillStatus
    {
        protected WeaponData data;
        public WeaponData Data => data;
        protected Transform caster;
        public int Level { get; private set; } = 1;
        protected float cooldownTimer = 0;

        protected IWeaponStats stats;
        protected IEnemyTargetProvider targetProvider;
        private readonly Dictionary<string, int> acquiredCardCounts = new Dictionary<string, int>();
        private readonly List<IEnemyTarget> targetBuffer = new List<IEnemyTarget>(TargetCandidateCount);
        private const int TargetCandidateCount = 8;

        public bool IsMaxLevel => Level >= data.maxLevel;

        int ISkillStatus.Id => data.id;

        string ISkillStatus.IconKey => data.iconKey;

        /// <summary>0이면 발사 가능, 1이면 방금 발사. 강화로 쿨타임이 줄어든 직후에도 1을 넘지 않게 자른다</summary>
        public float CooldownRatio => stats.Cooldown <= 0f ? 0f : Mathf.Clamp01(cooldownTimer / stats.Cooldown);

        public WeaponBase(WeaponData data, Transform caster, IEnemyTargetProvider targetProvider)
        {
            this.data = data;
            this.caster = caster;
            this.targetProvider = targetProvider;

            stats = new BaseWeaponStats(data.baseStats);
        }

        public int GetAcquiredCount(string cardId)
        {
            return acquiredCardCounts.TryGetValue(cardId, out var count) ? count : 0;
        }

        public void Tick()
        {
            cooldownTimer -= Time.deltaTime;

            if (cooldownTimer <= 0)
            {
                OnFire();
                cooldownTimer = stats.Cooldown;
            }
        }

        protected abstract void OnFire();

        // GetNearest로 뽑은 후보 중 사거리 안에서 y값(벽에 가장 가까운 값)이 제일 낮은 적을 고른다
        protected IEnemyTarget FindTarget(float maxRange)
        {
            targetProvider.GetNearest(caster.position, TargetCandidateCount, targetBuffer);

            IEnemyTarget best = null;
            float bestY = float.MaxValue;
            float maxRangeSqr = maxRange * maxRange;

            foreach (var candidate in targetBuffer)
            {
                float distSqr = ((Vector2)caster.position - candidate.Position).sqrMagnitude;
                if (distSqr > maxRangeSqr)
                {
                    continue;
                }

                if (candidate.Position.y < bestY)
                {
                    bestY = candidate.Position.y;
                    best = candidate;
                }
            }

            return best;
        }

        public void LevelUp(WeaponUpgradeOption option)
        {
            if (Level >= data.maxLevel)
            {
                return;
            }

            Level++;

            acquiredCardCounts[option.id] = GetAcquiredCount(option.id) + 1;

            foreach (var effect in option.effects)
            {
                stats = effect.type switch
                {
                    UpgradeType.AttackSpeed => new AttackSpeedUpgrade(stats, effect.value),
                    UpgradeType.Damage => new DamageUpgrade(stats, effect.value),
                    UpgradeType.ProjectileCount => new ProjectileCountUpgrade(stats, effect.value),
                    UpgradeType.HitCount => new HitCountUpgrade(stats, (int)effect.value),
                    _ => throw new System.NotImplementedException()
                };
            }
        }
    }
}
