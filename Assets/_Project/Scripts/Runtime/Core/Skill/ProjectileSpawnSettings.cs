using UnityEngine;

namespace Game.Core
{
    /// <summary>One spawn snapshot. Unspecified effects stay disabled; proc chances default to 100%.</summary>
    public sealed class ProjectileSpawnSettings
    {
        public Vector3 StartPos { get; set; }
        public Vector3 Direction { get; set; }
        public float Damage { get; set; }
        public float Speed { get; set; }
        public float Lifetime { get; set; }
        public IEnemyTargetProvider TargetProvider { get; set; }
        public int PierceCount { get; set; } = 0;
        public float FreezeDuration { get; set; } = 0;
        public System.Action<Vector2, Vector3, IEnemyTarget> OnHit { get; set; }
        public IEnemyTarget IgnoredTarget { get; set; }
        public AttackReactions Reactions { get; set; } = AttackReactions.Empty;
        public float KnockbackDistance { get; set; } = 0;
        public float FrostbiteRatio { get; set; } = 0;
        public float ParalysisDuration { get; set; } = 0;
        public float LightningDamage { get; set; } = 0;
        public float ParalysisChance { get; set; } = 1;
        public System.Func<float> RandomValue { get; set; }
        public float BurnDuration { get; set; } = 0;
        public float BurnDamage { get; set; } = 0;
        public float BurnMaxHpRatio { get; set; } = 0;
        public System.Action<Vector2> BurnOnDeath { get; set; }
        public float FreezeChance { get; set; } = 1;
        public float FrostbiteChance { get; set; } = 1;
        public float BurnChance { get; set; } = 1;
        public float CollisionRadius { get; set; } = .3f;
        public float StunDuration { get; set; } = 0;
        public float StunChance { get; set; } = 1;
        public float SlowDuration { get; set; } = 0;
        public float SlowRatio { get; set; } = 0;
        public float VulnerabilityRatio { get; set; } = 0;
        public float VulnerabilityDuration { get; set; } = 0;
    }
}
