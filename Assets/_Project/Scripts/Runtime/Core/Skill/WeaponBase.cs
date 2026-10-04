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
        // 최초 습득은 강화 상한에 포함하지 않는다. 기존 표시 레벨은 유지한다.
        public int UpgradeCount => Level - 1;
        private readonly CastClock castClock = new();
        protected float cooldownTimer
        {
            get => castClock.RemainingCooldown;
            set => castClock.RemainingCooldown = value;
        }

        protected IWeaponStats stats;
        protected IEnemyTargetProvider targetProvider;
        private readonly Dictionary<string, int> acquiredCardCounts = new Dictionary<string, int>();
        private readonly List<IEnemyTarget> targetBuffer = new List<IEnemyTarget>(TargetCandidateCount);
        private const int TargetCandidateCount = 8;

        public bool IsMaxLevel => UpgradeCount >= data.maxLevel;

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
            var option = data.upgrades?.Find(o => o.id == cardId);
            if (!string.IsNullOrEmpty(option?.sharedId)) { cardId = option.sharedId; }
            return acquiredCardCounts.TryGetValue(cardId, out var count) ? count : 0;
        }

        public void Tick() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            castClock.Tick(deltaTime, stats.Cooldown, stats.CastCount, data.baseStats.castInterval, OnFire);
        }

        protected abstract void OnFire();

        // GetNearest로 뽑은 후보 중 사거리 안에서 y값(벽에 가장 가까운 값)이 제일 낮은 적을 고른다
        protected List<IEnemyTarget> FindTargets(float maxRange, int count)
        {
            targetProvider.GetNearest(caster.position, TargetCandidateCount, targetBuffer);

            var inRange = new List<IEnemyTarget>();
            float maxRangeSqr = maxRange * maxRange;
            foreach (var c in targetBuffer)
            {
                if (((Vector2)caster.position - c.Position).sqrMagnitude <= maxRangeSqr)
                {
                    inRange.Add(c);
                }
            }

            inRange.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));  // y 낮은 순 우선순위
            return inRange;
        }

        public bool LevelUp(WeaponUpgradeOption option, int permanentLevel = 0)
        {
            // 공유 강화는 전체 참여 인술을 준비하는 트랜잭션을 통해서만 적용한다.
            if (!string.IsNullOrEmpty(option?.sharedId)
                || !WeaponUpgradeResolver.TryResolve(option, permanentLevel, out var resolved)
                || !TryPrepareUpgrade(option, resolved.Effects, out var nextStats)) { return false; }
            CommitUpgrade(option, nextStats);
            return true;
        }

        internal bool TryPrepareUpgrade(WeaponUpgradeOption option, List<StatEffect> effects, out IWeaponStats nextStats)
        {
            nextStats = stats;
            if (IsMaxLevel || option == null || !option.enabled
                || string.IsNullOrEmpty(option.id) || option.maxPickCount <= 0
                || GetAcquiredCount(string.IsNullOrEmpty(option.sharedId) ? option.id : option.sharedId) >= option.maxPickCount
                || effects == null)
            {
                return false;
            }

            // 효과 전체가 유효한 경우에만 횟수, 레벨과 능력치를 함께 반영한다.
            foreach (var effect in effects)
            {
                if (effect == null || float.IsNaN(effect.value) || float.IsInfinity(effect.value)) { return false; }
                switch (effect.type)
                {
                    case UpgradeType.AttackSpeed: nextStats = new AttackSpeedUpgrade(nextStats, effect.value); break;
                    case UpgradeType.Damage: nextStats = new DamageUpgrade(nextStats, effect.value); break;
                    case UpgradeType.ProjectileCount: nextStats = new ProjectileCountUpgrade(nextStats, effect.value); break;
                    case UpgradeType.HitCount: nextStats = new HitCountUpgrade(nextStats, (int)effect.value); break;
                    case UpgradeType.CastCount: nextStats = new CastCountUpgrade(nextStats, (int)effect.value); break;
                    case UpgradeType.PierceCount: nextStats = new PierceCountUpgrade(nextStats, (int)effect.value); break;
                    case UpgradeType.ProjectileSpeed: nextStats = new ProjectileSpeedUpgrade(nextStats, effect.value); break;
                    case UpgradeType.EnableExplosion:
                        if (effect.value <= 0)
                        {
                            return false;
                        }

                        nextStats = new EnableExplosionUpgrade(nextStats, effect.value); break;
                    case UpgradeType.ExplosionDamage:
                    case UpgradeType.ExplosionRadius:
                        nextStats = new ExplosionUpgrade(nextStats, effect.value, effect.type == UpgradeType.ExplosionRadius); break;
                    case UpgradeType.Frostbite:
                    case UpgradeType.ShardFrostbite:
                        if (effect.value <= 0)
                        {
                            return false;
                        }

                        nextStats = new FrostbiteUpgrade(nextStats, effect.value, effect.type == UpgradeType.ShardFrostbite); break;
                    case UpgradeType.Knockback:
                        nextStats = new KnockbackUpgrade(nextStats, effect.value); break;
                    case UpgradeType.SplitCount:
                        if (effect.value <= 0 || effect.value != System.Math.Round(effect.value))
                        {
                            return false;
                        }

                        nextStats = new SplitCountUpgrade(nextStats, (int)effect.value); break;
                    case UpgradeType.ShardDamage:
                        nextStats = new ShardDamageUpgrade(nextStats, effect.value); break;
                    case UpgradeType.FreezeDuration:
                        if (effect.value <= 0) { return false; }
                        nextStats = new FreezeDurationUpgrade(nextStats, effect.value); break;
                    default: return false;
                }
            }

            return true;
        }

        internal void CommitUpgrade(WeaponUpgradeOption option, IWeaponStats nextStats)
        {
            string key = string.IsNullOrEmpty(option.sharedId) ? option.id : option.sharedId;
            stats = nextStats;
            Level++;
            acquiredCardCounts[key] = GetAcquiredCount(key) + 1;
        }
    }
}
