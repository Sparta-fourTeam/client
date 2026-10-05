namespace Game.Core
{
    /// <summary>Immutable combat values, grouped by the behavior that consumes them.</summary>
    public sealed class WeaponStats
    {
        public CastStats Cast { get; }
        public ProjectileStats Projectile { get; }
        public StatusStats Status { get; }
        public BurnStats Burn { get; }
        public ExplosionStats Explosion { get; }
        public SecondaryStats Secondary { get; }
        public FieldStats Field { get; }

        public static WeaponStats FromDefinition(WeaponBaseStats data) => new WeaponStats(new WeaponStatsBuilder(data));

        internal WeaponStats(WeaponStatsBuilder source)
        {
            Cast = new CastStats(source);
            Projectile = new ProjectileStats(source);
            Status = new StatusStats(source);
            Burn = new BurnStats(source);
            Explosion = new ExplosionStats(source);
            Secondary = new SecondaryStats(source);
            Field = new FieldStats(source);
        }

    }

    public readonly struct CastStats
    {
        public float Cooldown { get; }
        public float Damage { get; }
        public int ProjectileCount { get; }
        public int Count { get; }
        public int ReserveCount { get; }
        public WeaponForm Form { get; }

        internal CastStats(WeaponStatsBuilder source)
        {
            Cooldown = source.Cooldown;
            Damage = source.Damage;
            ProjectileCount = source.ProjectileCount;
            Count = source.CastCount;
            ReserveCount = source.ReserveCastCount;
            Form = source.Form;
        }
    }

    public readonly struct ProjectileStats
    {
        public int PierceCount { get; }
        public float Speed { get; }
        public float SizeMultiplier { get; }
        public float KnockbackDistance { get; }

        internal ProjectileStats(WeaponStatsBuilder source)
        {
            PierceCount = source.PierceCount;
            Speed = source.ProjectileSpeed;
            SizeMultiplier = source.ProjectileSizeMultiplier;
            KnockbackDistance = source.KnockbackDistance;
        }
    }

    public readonly struct StatusStats
    {
        public float FreezeDuration { get; }
        public float FrostbiteRatio { get; }
        public float ParalysisDuration { get; }
        public float StunDuration { get; }
        public float SlowDuration { get; }
        public float SlowRatio { get; }
        public float VulnerabilityRatio { get; }
        public float VulnerabilityDuration { get; }

        internal StatusStats(WeaponStatsBuilder source)
        {
            FreezeDuration = source.FreezeDuration;
            FrostbiteRatio = source.FrostbiteRatio;
            ParalysisDuration = source.ParalysisDuration;
            StunDuration = source.StunDuration;
            SlowDuration = source.SlowDuration;
            SlowRatio = source.SlowRatio;
            VulnerabilityRatio = source.VulnerabilityRatio;
            VulnerabilityDuration = source.VulnerabilityDuration;
        }
    }

    public readonly struct BurnStats
    {
        public float Duration { get; }
        public float DamageRatio { get; }
        public float MaxHpRatio { get; }
        public bool DeathExplosion { get; }

        internal BurnStats(WeaponStatsBuilder source)
        {
            Duration = source.BurnDuration;
            DamageRatio = source.BurnRatio;
            MaxHpRatio = source.BurnMaxHpRatio;
            DeathExplosion = source.BurnDeathExplosion;
        }
    }

    public readonly struct ExplosionStats
    {
        public float Radius { get; }
        public float Damage { get; }

        internal ExplosionStats(WeaponStatsBuilder source)
        {
            Radius = source.ExplosionRadius;
            Damage = source.ExplosionDamage;
        }
    }

    public readonly struct SecondaryStats
    {
        public int Count { get; }
        public float DamageMultiplier { get; }
        public float FrostbiteRatio { get; }
        public float ParalysisDuration { get; }
        public float LightningRatio { get; }
        public bool Explosions { get; }
        public float KillLightningRatio { get; }
        public float LightningStrikeRatio { get; }

        internal SecondaryStats(WeaponStatsBuilder source)
        {
            Count = source.SplitCount;
            DamageMultiplier = source.ShardDamageMultiplier;
            FrostbiteRatio = source.ShardFrostbiteRatio;
            ParalysisDuration = source.AuxiliaryParalysisDuration;
            LightningRatio = source.AuxiliaryLightningRatio;
            Explosions = source.AuxiliaryExplosions;
            KillLightningRatio = source.KillLightningRatio;
            LightningStrikeRatio = source.LightningStrikeRatio;
        }
    }

    public readonly struct FieldStats
    {
        public float Duration { get; }
        public float DamageRatio { get; }
        public float FlatDamage { get; }
        public float DamageMultiplier { get; }
        public float Radius { get; }
        public float SlowRatio { get; }

        internal FieldStats(WeaponStatsBuilder source)
        {
            Duration = source.FieldDuration;
            DamageRatio = source.FieldDamageRatio;
            FlatDamage = source.FieldFlatDamage;
            DamageMultiplier = source.FieldDamageMultiplier;
            Radius = source.FieldRadius;
            SlowRatio = source.FieldSlowRatio;
        }
    }

    // Only upgrade preparation can mutate values; committed snapshots are read-only.
    internal sealed class WeaponStatsBuilder
    {
        public float Cooldown;
        public float Damage;
        public int ProjectileCount;
        public int CastCount;
        public int PierceCount;
        public float ProjectileSpeed;
        public float ProjectileSizeMultiplier;
        public float FreezeDuration;
        public float KnockbackDistance;
        public float ExplosionRadius;
        public float ExplosionDamage;
        public float FrostbiteRatio;
        public float ShardFrostbiteRatio;
        public int SplitCount;
        public float ShardDamageMultiplier;
        public float ParalysisDuration;
        public float AuxiliaryParalysisDuration;
        public float KillLightningRatio;
        public float LightningStrikeRatio;
        public float AuxiliaryLightningRatio;
        public bool AuxiliaryExplosions;
        public float BurnDuration;
        public float BurnRatio;
        public float BurnMaxHpRatio;
        public bool BurnDeathExplosion;
        public float FieldDuration;
        public float FieldDamageRatio;
        public float FieldFlatDamage;
        public float FieldDamageMultiplier;
        public float FieldRadius;
        public float FieldSlowRatio;
        public float StunDuration;
        public float SlowDuration;
        public float SlowRatio;
        public float VulnerabilityRatio;
        public float VulnerabilityDuration;
        public int ReserveCastCount;
        public WeaponForm Form;

        public WeaponStatsBuilder(WeaponBaseStats data)
        {
            Cooldown = data.cooldown;
            Damage = data.baseDamage;
            ProjectileCount = System.Math.Max(1, data.hitCount);
            CastCount = System.Math.Max(1, data.castCount);
            PierceCount = System.Math.Max(0, data.pierceCount);
            ProjectileSpeed = data.speed;
            ProjectileSizeMultiplier = 1;
            FreezeDuration = data.freezeDuration;
            KnockbackDistance = data.knockbackDistance;
            ExplosionRadius = data.explosionRadius;
            ExplosionDamage = data.baseDamage * data.explosionDamageRatio;
            FrostbiteRatio = 0;
            ShardFrostbiteRatio = 0;
            SplitCount = 0;
            ShardDamageMultiplier = 1;
            ParalysisDuration = data.paralysisDuration;
            AuxiliaryParalysisDuration = 0;
            KillLightningRatio = 0;
            LightningStrikeRatio = 0;
            AuxiliaryLightningRatio = 0;
            AuxiliaryExplosions = false;
            BurnDuration = 0;
            BurnRatio = 0;
            BurnMaxHpRatio = 0;
            BurnDeathExplosion = false;
            FieldDuration = 0;
            FieldDamageRatio = data.fieldDamageRatio;
            FieldFlatDamage = 0;
            FieldDamageMultiplier = 1;
            FieldRadius = data.fieldRadius;
            FieldSlowRatio = data.fieldSlowRatio;
            StunDuration = data.stunDuration;
            SlowDuration = data.slowDuration;
            SlowRatio = data.slowRatio;
            VulnerabilityRatio = 0;
            VulnerabilityDuration = 0;
            ReserveCastCount = 0;
            Form = WeaponForm.Default;
        }

        public WeaponStatsBuilder(WeaponStats source)
        {
            Cooldown = source.Cast.Cooldown;
            Damage = source.Cast.Damage;
            ProjectileCount = source.Cast.ProjectileCount;
            CastCount = source.Cast.Count;
            PierceCount = source.Projectile.PierceCount;
            ProjectileSpeed = source.Projectile.Speed;
            ProjectileSizeMultiplier = source.Projectile.SizeMultiplier;
            FreezeDuration = source.Status.FreezeDuration;
            KnockbackDistance = source.Projectile.KnockbackDistance;
            ExplosionRadius = source.Explosion.Radius;
            ExplosionDamage = source.Explosion.Damage;
            FrostbiteRatio = source.Status.FrostbiteRatio;
            ShardFrostbiteRatio = source.Secondary.FrostbiteRatio;
            SplitCount = source.Secondary.Count;
            ShardDamageMultiplier = source.Secondary.DamageMultiplier;
            ParalysisDuration = source.Status.ParalysisDuration;
            AuxiliaryParalysisDuration = source.Secondary.ParalysisDuration;
            KillLightningRatio = source.Secondary.KillLightningRatio;
            LightningStrikeRatio = source.Secondary.LightningStrikeRatio;
            AuxiliaryLightningRatio = source.Secondary.LightningRatio;
            AuxiliaryExplosions = source.Secondary.Explosions;
            BurnDuration = source.Burn.Duration;
            BurnRatio = source.Burn.DamageRatio;
            BurnMaxHpRatio = source.Burn.MaxHpRatio;
            BurnDeathExplosion = source.Burn.DeathExplosion;
            FieldDuration = source.Field.Duration;
            FieldDamageRatio = source.Field.DamageRatio;
            FieldFlatDamage = source.Field.FlatDamage;
            FieldDamageMultiplier = source.Field.DamageMultiplier;
            FieldRadius = source.Field.Radius;
            FieldSlowRatio = source.Field.SlowRatio;
            StunDuration = source.Status.StunDuration;
            SlowDuration = source.Status.SlowDuration;
            SlowRatio = source.Status.SlowRatio;
            VulnerabilityRatio = source.Status.VulnerabilityRatio;
            VulnerabilityDuration = source.Status.VulnerabilityDuration;
            ReserveCastCount = source.Cast.ReserveCount;
            Form = source.Cast.Form;
        }
    }
}
