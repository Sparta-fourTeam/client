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
        private readonly AttackReactions reactions;

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
            reactions = Compile(settings);
        }

        public void Apply(IEnemyTarget candidate, Vector3 direction)
        {
            reactions.Raise(AttackEvent.Hit, new AttackContext(candidate.Position, direction, candidate, randomValue));
        }

        private static AttackReactions Compile(ProjectileSpawnSettings s) => new HitReactionBuilder()
            .Damage(s.Damage)
            .Freeze(s.FreezeDuration, s.FreezeChance)
            .Knockback(s.KnockbackDistance)
            .Frostbite(s.Damage * s.FrostbiteRatio, s.FrostbiteChance)
            .Paralysis(s.ParalysisDuration, s.ParalysisChance)
            .LightningStrike(s.LightningDamage)
            .Burn(s.BurnDamage, s.BurnDuration, s.BurnMaxHpRatio, s.BurnChance, s.BurnOnDeath)
            .Stun(s.StunDuration, s.StunChance)
            .Slow(s.SlowRatio, s.SlowDuration)
            .Vulnerability(s.VulnerabilityRatio, s.VulnerabilityDuration)
            .Build();
    }
}
