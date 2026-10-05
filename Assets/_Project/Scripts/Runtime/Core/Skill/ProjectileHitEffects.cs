using UnityEngine;

namespace Game.Core
{
    /// <summary>Copied hit effects, applied in gameplay order independently of movement and pooling.</summary>
    public struct ProjectileHitEffects
    {
        private float damage;
        private float freezeDuration;
        private float stunDuration, stunChance, slowDuration, slowRatio, vulnerabilityRatio, vulnerabilityDuration;
        private float burnDuration;
        private float burnDamage;
        private float burnMaxHpRatio;
        private System.Action<Vector2> burnOnDeath;
        private float paralysisDuration;
        private float paralysisChance;
        private float freezeChance;
        private float frostbiteChance;
        private float burnChance;
        private System.Func<float> randomValue;
        private float lightningDamage;
        private float knockbackDistance;
        private float frostbiteRatio;

        public ProjectileHitEffects(ProjectileSpawnSettings settings)
        {
            damage = settings.Damage;
            freezeDuration = settings.FreezeDuration;
            stunDuration = settings.StunDuration;
            stunChance = settings.StunChance;
            slowDuration = settings.SlowDuration;
            slowRatio = settings.SlowRatio;
            vulnerabilityRatio = settings.VulnerabilityRatio;
            vulnerabilityDuration = settings.VulnerabilityDuration;
            burnDuration = settings.BurnDuration;
            burnDamage = settings.BurnDamage;
            burnMaxHpRatio = settings.BurnMaxHpRatio;
            burnOnDeath = settings.BurnOnDeath;
            paralysisDuration = settings.ParalysisDuration;
            paralysisChance = settings.ParalysisChance;
            freezeChance = settings.FreezeChance;
            frostbiteChance = settings.FrostbiteChance;
            burnChance = settings.BurnChance;
            lightningDamage = settings.LightningDamage;
            knockbackDistance = settings.KnockbackDistance;
            frostbiteRatio = settings.FrostbiteRatio;
            randomValue = settings.RandomValue ?? (() => Random.value);
        }

        public void Apply(IEnemyTarget candidate, Vector3 direction)
        {
            candidate.TakeDamage((int)damage);
            if (freezeDuration > 0 && candidate is IFreezableTarget freezable && StatusProc.Roll(freezeChance, randomValue)) { freezable.ApplyFreeze(freezeDuration); }
            if (knockbackDistance > 0 && candidate is IKnockbackTarget movable) { movable.ApplyKnockback(direction, knockbackDistance); }
            if (frostbiteRatio > 0 && candidate is IFrostbiteTarget frosted && StatusProc.Roll(frostbiteChance, randomValue)) { frosted.ApplyFrostbite(damage * frostbiteRatio); }
            if (paralysisDuration > 0 && candidate is IParalyzableTarget paralyzed && StatusProc.Roll(paralysisChance, randomValue)) { paralyzed.ApplyParalysis(paralysisDuration); }
            if (lightningDamage > 0) { candidate.TakeDamage(Mathf.Max(1, (int)lightningDamage)); }
            if (burnDuration > 0 && burnDamage > 0 && candidate is IBurnableTarget burning && StatusProc.Roll(burnChance, randomValue)) { burning.ApplyBurn(burnDamage, burnDuration, burnMaxHpRatio, burnOnDeath); }
            if (stunDuration > 0 && candidate is IStunnableTarget stunned && StatusProc.Roll(stunChance, randomValue)) { stunned.ApplyStun(stunDuration); }
            if (slowDuration > 0 && slowRatio > 0 && candidate is ISlowableTarget slowed) { slowed.ApplySlow(slowRatio, slowDuration); }
            if (vulnerabilityDuration > 0 && vulnerabilityRatio > 0 && candidate is IVulnerableTarget vulnerable) { vulnerable.ApplyVulnerability(vulnerabilityRatio, vulnerabilityDuration); }
        }
    }
}
