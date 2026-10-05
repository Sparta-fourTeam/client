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

        private static AttackReactions Compile(ProjectileSpawnSettings s)
        {
            var bindings = new System.Collections.Generic.List<ReactionBinding>();
            void Add(IAttackReaction reaction) => bindings.Add(new ReactionBinding(AttackEvent.Hit, reaction));
            Add(new DamageReaction(s.Damage));
            float freeze = s.FreezeDuration;
            if (freeze > 0) { Add(new StatusReaction<IFreezableTarget>(s.FreezeChance, (t, _) => t.ApplyFreeze(freeze))); }
            float knockback = s.KnockbackDistance;
            if (knockback > 0) { Add(new StatusReaction<IKnockbackTarget>(1, (t, c) => t.ApplyKnockback(c.Direction, knockback))); }
            float frostbite = s.Damage * s.FrostbiteRatio;
            if (s.FrostbiteRatio > 0) { Add(new StatusReaction<IFrostbiteTarget>(s.FrostbiteChance, (t, _) => t.ApplyFrostbite(frostbite))); }
            float paralysis = s.ParalysisDuration;
            if (paralysis > 0) { Add(new StatusReaction<IParalyzableTarget>(s.ParalysisChance, (t, _) => t.ApplyParalysis(paralysis))); }
            if (s.LightningDamage > 0)
            {
                Add(new DamageReaction(s.LightningDamage, minimumOne: true));
                Add(new CastSkillReaction(c => SkillReactionEffects.Lightning(c.Position)));
            }
            float burn = s.BurnDamage, burnTime = s.BurnDuration, maxHp = s.BurnMaxHpRatio;
            var onDeath = s.BurnOnDeath;
            if (burn > 0 && burnTime > 0) { Add(new StatusReaction<IBurnableTarget>(s.BurnChance, (t, _) => t.ApplyBurn(burn, burnTime, maxHp, onDeath))); }
            float stun = s.StunDuration;
            if (stun > 0) { Add(new StatusReaction<IStunnableTarget>(s.StunChance, (t, _) => t.ApplyStun(stun))); }
            float slowTime = s.SlowDuration, slow = s.SlowRatio;
            if (slowTime > 0 && slow > 0) { Add(new StatusReaction<ISlowableTarget>(1, (t, _) => t.ApplySlow(slow, slowTime))); }
            float vulnerable = s.VulnerabilityRatio, vulnerableTime = s.VulnerabilityDuration;
            if (vulnerable > 0 && vulnerableTime > 0) { Add(new StatusReaction<IVulnerableTarget>(1, (t, _) => t.ApplyVulnerability(vulnerable, vulnerableTime))); }
            return new AttackReactions(bindings);
        }
    }
}
