using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public abstract class WeaponBase : ISkillStatus, System.IDisposable
    {
        private bool disposed;
        protected WeaponData data;
        public WeaponData Data => data;
        protected Transform caster;
        private readonly WeaponUpgradeState upgrades;
        public int Level => upgrades.Level;
        // 최초 습득은 강화 상한에 포함하지 않는다. 기존 표시 레벨은 유지한다.
        public int UpgradeCount => upgrades.UpgradeCount;
        private readonly CastClock castClock = new();
        protected float cooldownTimer
        {
            get => castClock.RemainingCooldown;
            set => castClock.RemainingCooldown = value;
        }

        protected WeaponStats stats;
        private SkillConfig config;
        public SkillConfig Config
        {
            get
            {
                // Protected stats is retained for existing test subclasses.
                if (!ReferenceEquals(config.Stats, stats)) { config = new SkillConfig(stats, config.Attack, config.Bindings, config.ChildCaster); }
                return config;
            }
        }
        protected IEnemyTargetProvider targetProvider;
        private readonly WeaponTargetSelector targets;
        public WeaponStats Stats => stats;
        public bool IsMaxLevel => upgrades.IsMaxLevel;

        int ISkillStatus.Id => data.id;

        string ISkillStatus.IconKey => data.iconKey;

        /// <summary>0이면 발사 가능, 1이면 방금 발사. 강화로 쿨타임이 줄어든 직후에도 1을 넘지 않게 자른다</summary>
        public float CooldownRatio => Stats.Cast.Cooldown <= 0f ? 0f : Mathf.Clamp01(cooldownTimer / Stats.Cast.Cooldown);

        public WeaponBase(WeaponData data, Transform caster, IEnemyTargetProvider targetProvider, SkillConfig config = null)
        {
            this.data = data;
            this.caster = caster;
            this.targetProvider = targetProvider;

            this.config = config ?? SkillConfig.FromDefinition(data);
            stats = this.config.Stats;
            upgrades = new WeaponUpgradeState(data);
            targets = new WeaponTargetSelector(targetProvider);
        }

        /// <summary>자식 스킬 시전 효과가 쓸 시전기를 연결한다</summary>
        public void UseChildCaster(IChildSkillCaster childCaster) => config = Config.WithChildCaster(childCaster);

        public int GetAcquiredCount(string cardId) => upgrades.GetAcquiredCount(cardId);

        public void Tick() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            if (disposed) { return; }
            OnTickExtra(deltaTime);
            var current = Stats.Cast;
            castClock.Tick(deltaTime, current.Cooldown, current.Count, current.Interval, OnFire);
        }

        protected virtual void OnTickExtra(float deltaTime) { }

        public virtual void Dispose() => disposed = true;

        protected abstract void OnFire();

        protected IReadOnlyList<IEnemyTarget> FindTargets(float maxRange) =>
            targets.Select(caster.position, maxRange);

        public bool LevelUp(WeaponUpgradeOption option, int permanentLevel = 0)
        {
            // 공유 강화는 전체 참여 스킬을 준비하는 트랜잭션을 통해서만 적용한다.
            if (!string.IsNullOrEmpty(option?.sharedId)
                || !WeaponUpgradeResolver.TryResolve(option, permanentLevel, out var resolved)
                || !TryPrepareUpgrade(option, resolved.Effects, out var nextStats)) { return false; }
            CommitUpgrade(option, nextStats);
            return true;
        }

        internal bool TryPrepareUpgrade(WeaponUpgradeOption option, List<EffectDef> effects, out SkillConfig nextConfig)
        {
            nextConfig = Config;
            if (!upgrades.CanPrepare(option)) { return false; }
            var builder = new SkillConfigBuilder(Config);
            if (!builder.TryApplyCatalog(effects)) { return false; }
            nextConfig = builder.Build();
            return true;
        }

        internal void CommitUpgrade(WeaponUpgradeOption option, SkillConfig nextConfig)
        {
            config = nextConfig;
            stats = config.Stats;
            upgrades.Commit(option);
        }
    }
}
