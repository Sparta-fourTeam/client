using System;

namespace Game.Core
{
    /// <summary>스냅샷과 빌더가 공유하는 스탯 키. 스탯 하나를 더하려면 여기, <see cref="WeaponStatRegistry"/>, 묶음 구조체에 한 줄씩 적는다.</summary>
    internal enum Stat
    {
        Cooldown, Damage, ProjectileCount, CastCount, ReserveCastCount, Form, CastInterval,
        ReserveDistance, ReserveCooldown, ReserveInterval,
        PierceCount, ProjectileSpeed, ProjectileSizeMultiplier, KnockbackDistance,
        FreezeDuration, FreezeChance, FrostbiteRatio, FrostbiteChance, ParalysisDuration, ParalysisChance,
        StunDuration, StunChance, SlowDuration, SlowRatio, VulnerabilityRatio, VulnerabilityDuration,
        BurnDuration, BurnRatio, BurnMaxHpRatio, BurnDeathExplosion, BurnChance,
        ExplosionRadius, ExplosionDamage,
        SplitCount, SplitDamageMultiplier, SplitFrostbiteRatio, SplitParalysisDuration, SplitLightningRatio,
        SplitExplosions, KillLightningRatio, LightningStrikeRatio,
        FieldDuration, FieldDamageRatio, FieldFlatDamage, FieldDamageMultiplier, FieldRadius, FieldSlowRatio,
        AreaRadius, AreaDuration, AreaPulseInterval, AreaMoveSpeed, AreaPull,
        Count
    }

    /// <summary>스탯별 기본값 선언. 선언이 빠진 스탯은 첫 사용 때 바로 예외가 나므로 조용히 0으로 남지 않는다.</summary>
    internal static class WeaponStatRegistry
    {
        private static readonly Func<WeaponBaseStats, float>[] Defaults = Create();

        public static float[] CreateValues(WeaponBaseStats d)
        {
            var values = new float[(int)Stat.Count];
            for (int i = 0; i < values.Length; i++) { values[i] = Defaults[i](d); }
            return values;
        }

        private static Func<WeaponBaseStats, float>[] Create()
        {
            var t = new Func<WeaponBaseStats, float>[(int)Stat.Count];
            void Def(Stat stat, Func<WeaponBaseStats, float> value) => t[(int)stat] = value;
            Def(Stat.Cooldown, d => d.cast.cooldown);
            Def(Stat.Damage, d => d.cast.baseDamage);
            Def(Stat.ProjectileCount, d => Math.Max(1, d.cast.projectileCount));
            Def(Stat.CastCount, d => Math.Max(1, d.cast.castCount));
            Def(Stat.ReserveCastCount, _ => 0);
            Def(Stat.Form, _ => (int)WeaponForm.Default);
            Def(Stat.CastInterval, d => d.cast.castInterval);
            Def(Stat.ReserveDistance, d => d.reserve.distance);
            Def(Stat.ReserveCooldown, d => d.reserve.cooldown);
            Def(Stat.ReserveInterval, d => d.reserve.interval);
            Def(Stat.PierceCount, d => Math.Max(0, d.projectile.pierceCount));
            Def(Stat.ProjectileSpeed, d => d.projectile.speed);
            Def(Stat.ProjectileSizeMultiplier, _ => 1);
            Def(Stat.KnockbackDistance, d => d.projectile.knockbackDistance);
            Def(Stat.FreezeDuration, d => d.status.freezeDuration);
            Def(Stat.FreezeChance, d => d.status.freezeChance);
            Def(Stat.FrostbiteRatio, _ => 0);
            Def(Stat.FrostbiteChance, d => d.status.frostbiteChance);
            Def(Stat.ParalysisDuration, d => d.status.paralysisDuration);
            Def(Stat.ParalysisChance, d => d.status.paralysisChance);
            Def(Stat.StunDuration, d => d.status.stunDuration);
            Def(Stat.StunChance, d => d.status.stunChance);
            Def(Stat.SlowDuration, d => d.status.slowDuration);
            Def(Stat.SlowRatio, d => d.status.slowRatio);
            Def(Stat.VulnerabilityRatio, _ => 0);
            Def(Stat.VulnerabilityDuration, _ => 0);
            Def(Stat.BurnDuration, _ => 0);
            Def(Stat.BurnRatio, _ => 0);
            Def(Stat.BurnMaxHpRatio, _ => 0);
            Def(Stat.BurnDeathExplosion, _ => 0);
            Def(Stat.BurnChance, d => d.status.burnChance);
            Def(Stat.ExplosionRadius, d => d.explosion.radius);
            Def(Stat.ExplosionDamage, d => d.cast.baseDamage * d.explosion.damageRatio);
            Def(Stat.SplitCount, _ => 0);
            Def(Stat.SplitDamageMultiplier, _ => 1);
            Def(Stat.SplitFrostbiteRatio, _ => 0);
            Def(Stat.SplitParalysisDuration, _ => 0);
            Def(Stat.SplitLightningRatio, _ => 0);
            Def(Stat.SplitExplosions, _ => 0);
            Def(Stat.KillLightningRatio, _ => 0);
            Def(Stat.LightningStrikeRatio, _ => 0);
            Def(Stat.FieldDuration, _ => 0);
            Def(Stat.FieldDamageRatio, d => d.field.damageRatio);
            Def(Stat.FieldFlatDamage, _ => 0);
            Def(Stat.FieldDamageMultiplier, _ => 1);
            Def(Stat.FieldRadius, d => d.field.radius);
            Def(Stat.FieldSlowRatio, d => d.field.slowRatio);
            Def(Stat.AreaRadius, d => d.area.radius);
            Def(Stat.AreaDuration, d => d.area.duration);
            Def(Stat.AreaPulseInterval, d => d.area.pulseInterval);
            Def(Stat.AreaMoveSpeed, d => d.area.moveSpeed);
            Def(Stat.AreaPull, d => d.area.pull);
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == null) { throw new InvalidOperationException($"스탯 {(Stat)i}의 기본값 선언이 없습니다."); }
            }
            return t;
        }
    }

    /// <summary>Immutable combat values, grouped by the behavior that consumes them.</summary>
    public sealed class WeaponStats
    {
        private readonly float[] values;
        public CastStats Cast { get; }
        public ProjectileStats Projectile { get; }
        public StatusStats Status { get; }
        public BurnStats Burn { get; }
        public ExplosionStats Explosion { get; }
        public SecondaryStats Secondary { get; }
        public FieldStats Field { get; }
        public AreaStats Area { get; }

        public static WeaponStats FromDefinition(WeaponBaseStats data) => new WeaponStats(new WeaponStatsBuilder(data));

        // 값 배열은 생성 시 한 번 복사하고 밖으로 내보내지 않는다.
        internal WeaponStats(WeaponStatsBuilder source)
        {
            values = source.CopyValues();
            Cast = new CastStats(values);
            Projectile = new ProjectileStats(values);
            Status = new StatusStats(values);
            Burn = new BurnStats(values);
            Explosion = new ExplosionStats(values);
            Secondary = new SecondaryStats(values);
            Field = new FieldStats(values);
            Area = new AreaStats(values);
        }

        internal float[] CopyValues() => (float[])values.Clone();
    }

    public readonly struct CastStats
    {
        private readonly float[] v;
        internal CastStats(float[] values) => v = values;

        public float Cooldown => v[(int)Stat.Cooldown];
        public float Damage => v[(int)Stat.Damage];
        public int ProjectileCount => (int)v[(int)Stat.ProjectileCount];
        public int Count => (int)v[(int)Stat.CastCount];
        public int ReserveCount => (int)v[(int)Stat.ReserveCastCount];
        public WeaponForm Form => (WeaponForm)(int)v[(int)Stat.Form];
        public float Interval => v[(int)Stat.CastInterval];
        public float ReserveDistance => v[(int)Stat.ReserveDistance];
        public float ReserveCooldown => v[(int)Stat.ReserveCooldown];
        public float ReserveInterval => v[(int)Stat.ReserveInterval];
    }

    public readonly struct ProjectileStats
    {
        private readonly float[] v;
        internal ProjectileStats(float[] values) => v = values;

        public int PierceCount => (int)v[(int)Stat.PierceCount];
        public float Speed => v[(int)Stat.ProjectileSpeed];
        public float SizeMultiplier => v[(int)Stat.ProjectileSizeMultiplier];
        public float KnockbackDistance => v[(int)Stat.KnockbackDistance];
    }

    public readonly struct StatusStats
    {
        private readonly float[] v;
        internal StatusStats(float[] values) => v = values;

        public float FreezeDuration => v[(int)Stat.FreezeDuration];
        public float FreezeChance => v[(int)Stat.FreezeChance];
        public float FrostbiteRatio => v[(int)Stat.FrostbiteRatio];
        public float FrostbiteChance => v[(int)Stat.FrostbiteChance];
        public float ParalysisDuration => v[(int)Stat.ParalysisDuration];
        public float ParalysisChance => v[(int)Stat.ParalysisChance];
        public float StunDuration => v[(int)Stat.StunDuration];
        public float StunChance => v[(int)Stat.StunChance];
        public float SlowDuration => v[(int)Stat.SlowDuration];
        public float SlowRatio => v[(int)Stat.SlowRatio];
        public float VulnerabilityRatio => v[(int)Stat.VulnerabilityRatio];
        public float VulnerabilityDuration => v[(int)Stat.VulnerabilityDuration];
    }

    public readonly struct BurnStats
    {
        private readonly float[] v;
        internal BurnStats(float[] values) => v = values;

        public float Duration => v[(int)Stat.BurnDuration];
        public float DamageRatio => v[(int)Stat.BurnRatio];
        public float MaxHpRatio => v[(int)Stat.BurnMaxHpRatio];
        public bool DeathExplosion => v[(int)Stat.BurnDeathExplosion] != 0;
        public float Chance => v[(int)Stat.BurnChance];
    }

    public readonly struct ExplosionStats
    {
        private readonly float[] v;
        internal ExplosionStats(float[] values) => v = values;

        public float Radius => v[(int)Stat.ExplosionRadius];
        public float Damage => v[(int)Stat.ExplosionDamage];
    }

    public readonly struct SecondaryStats
    {
        private readonly float[] v;
        internal SecondaryStats(float[] values) => v = values;

        public int Count => (int)v[(int)Stat.SplitCount];
        public float DamageMultiplier => v[(int)Stat.SplitDamageMultiplier];
        public float FrostbiteRatio => v[(int)Stat.SplitFrostbiteRatio];
        public float ParalysisDuration => v[(int)Stat.SplitParalysisDuration];
        public float LightningRatio => v[(int)Stat.SplitLightningRatio];
        public bool Explosions => v[(int)Stat.SplitExplosions] != 0;
        public float KillLightningRatio => v[(int)Stat.KillLightningRatio];
        public float LightningStrikeRatio => v[(int)Stat.LightningStrikeRatio];
    }

    public readonly struct FieldStats
    {
        private readonly float[] v;
        internal FieldStats(float[] values) => v = values;

        public float Duration => v[(int)Stat.FieldDuration];
        public float DamageRatio => v[(int)Stat.FieldDamageRatio];
        public float FlatDamage => v[(int)Stat.FieldFlatDamage];
        public float DamageMultiplier => v[(int)Stat.FieldDamageMultiplier];
        public float Radius => v[(int)Stat.FieldRadius];
        public float SlowRatio => v[(int)Stat.FieldSlowRatio];
    }

    public readonly struct AreaStats
    {
        private readonly float[] v;
        internal AreaStats(float[] values) => v = values;

        public float Radius => v[(int)Stat.AreaRadius];
        public float Duration => v[(int)Stat.AreaDuration];
        public float PulseInterval => v[(int)Stat.AreaPulseInterval];
        public float MoveSpeed => v[(int)Stat.AreaMoveSpeed];
        public float Pull => v[(int)Stat.AreaPull];
    }

    // Only upgrade preparation can mutate values; committed snapshots are read-only.
    internal sealed class WeaponStatsBuilder
    {
        private readonly float[] values;

        public WeaponStatsBuilder(WeaponBaseStats data) => values = WeaponStatRegistry.CreateValues(data);

        // 스냅샷을 열어 보지 않고 값 배열째 복사하므로 새 스탯이 복사에서 빠질 수 없다.
        public WeaponStatsBuilder(WeaponStats source) => values = source.CopyValues();

        public float this[Stat stat]
        {
            get => values[(int)stat];
            set => values[(int)stat] = value;
        }

        public float[] CopyValues() => (float[])values.Clone();
    }
}
