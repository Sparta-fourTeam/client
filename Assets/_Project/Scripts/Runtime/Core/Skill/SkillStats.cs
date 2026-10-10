using System;

namespace Game.Core
{
    /// <summary>스냅샷과 빌더가 공유하는 스탯 키. 스탯 하나를 더하려면 여기, <see cref="SkillStatRegistry"/>, 묶음 구조체에 한 줄씩 적는다.</summary>
    internal enum Stat
    {
        Cooldown, Damage, ProjectileCount, CastCount, ReserveCastCount, Form, CastInterval,
        ReserveDistance, ReserveCooldown, ReserveInterval,
        PierceCount, ProjectileSpeed, ProjectileSizeMultiplier, KnockbackDistance,
        FreezeDuration, FreezeChance, FrostbiteRatio, FrostbiteChance, FreezeMaxHpRatio, ParalysisDuration, ParalysisChance,
        StunDuration, StunChance, SlowDuration, SlowRatio, VulnerabilityRatio, VulnerabilityDuration,
        BurnDuration, BurnRatio, BurnMaxHpRatio, BurnDeathExplosion, BurnChance,
        ExplosionRadius, ExplosionDamage,
        KillLightningRatio, LightningStrikeRatio,
        FieldDuration, FieldDamageRatio, FieldFlatDamage, FieldDamageMultiplier, FieldRadius, FieldSlowRatio,
        AreaRadius, AreaDuration, AreaPulseInterval, AreaMoveSpeed, AreaPull, AreaCoreHits, AreaCoreRadiusRatio, AreaCoreFreezeScale,
        BeamLength, BeamWidth, BeamDuration, BeamPulses, BeamFocusBonus, BeamFocusRampMax, BeamFocusBlastRadius, BeamFocusBlastRatio, BeamRefractions, BeamFocusAim,
        ChainBounces, ChainJumpRange, ChainHopInterval, ChainPathWidth,
        Count
    }

    /// <summary>스탯별 기본값 선언. 선언이 빠진 스탯은 첫 사용 때 바로 예외가 나므로 조용히 0으로 남지 않는다.</summary>
    internal static class SkillStatRegistry
    {
        private static readonly Func<SkillBaseStats, float>[] Defaults = Create();

        /// <summary>스탯 이름(대소문자 무시)을 찾는다. 자식의 상속 규칙이 쓴다</summary>
        public static bool TryParse(string name, out Stat stat) =>
            Enum.TryParse(name, true, out stat) && stat != Stat.Count && Enum.IsDefined(typeof(Stat), stat);

        public static float[] CreateValues(SkillBaseStats d)
        {
            var values = new float[(int)Stat.Count];
            for (int i = 0; i < values.Length; i++) { values[i] = Defaults[i](d); }
            return values;
        }

        private static Func<SkillBaseStats, float>[] Create()
        {
            var t = new Func<SkillBaseStats, float>[(int)Stat.Count];
            void Def(Stat stat, Func<SkillBaseStats, float> value) => t[(int)stat] = value;
            Def(Stat.Cooldown, d => d.cast.cooldown);
            Def(Stat.Damage, d => d.cast.baseDamage);
            Def(Stat.ProjectileCount, d => Math.Max(1, d.cast.projectileCount));
            Def(Stat.CastCount, d => Math.Max(1, d.cast.castCount));
            Def(Stat.ReserveCastCount, _ => 0);
            Def(Stat.Form, _ => (int)SkillForm.Default);
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
            Def(Stat.FreezeMaxHpRatio, d => d.status.freezeMaxHpRatio);
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
            Def(Stat.AreaCoreHits, d => d.area.coreHits);
            Def(Stat.AreaCoreRadiusRatio, d => d.area.coreRadiusRatio);
            Def(Stat.AreaCoreFreezeScale, d => d.area.coreFreezeScale);
            Def(Stat.BeamLength, d => d.beam.length);
            Def(Stat.BeamWidth, d => d.beam.width);
            Def(Stat.BeamDuration, d => d.beam.duration);
            Def(Stat.BeamPulses, d => d.beam.pulses);
            Def(Stat.BeamFocusBonus, d => d.beam.focusBonus);
            Def(Stat.BeamFocusRampMax, d => d.beam.focusRampMax);
            Def(Stat.BeamFocusBlastRadius, d => d.beam.focusBlastRadius);
            Def(Stat.BeamFocusBlastRatio, d => d.beam.focusBlastRatio);
            Def(Stat.BeamRefractions, d => d.beam.refractions);
            Def(Stat.BeamFocusAim, d => d.beam.focusAim);
            Def(Stat.ChainBounces, d => d.chain.bounces);
            Def(Stat.ChainJumpRange, d => d.chain.jumpRange);
            Def(Stat.ChainHopInterval, d => d.chain.hopInterval);
            Def(Stat.ChainPathWidth, d => d.chain.pathWidth);
            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] == null) { throw new InvalidOperationException($"스탯 {(Stat)i}의 기본값 선언이 없습니다."); }
            }
            return t;
        }
    }

    /// <summary>Immutable combat values, grouped by the behavior that consumes them.</summary>
    public sealed class SkillStats
    {
        private readonly float[] values;
        public CastStats Cast { get; }
        public ProjectileStats Projectile { get; }
        public StatusStats Status { get; }
        public BurnStats Burn { get; }
        public ExplosionStats Explosion { get; }
        public LightningStats Lightning { get; }
        public FieldStats Field { get; }
        public AreaStats Area { get; }
        public BeamStats Beam { get; }
        public ChainStats Chain { get; }

        public static SkillStats FromDefinition(SkillBaseStats data) => new SkillStats(new SkillStatsBuilder(data));

        // 값 배열은 생성 시 한 번 복사하고 밖으로 내보내지 않는다.
        internal SkillStats(SkillStatsBuilder source)
        {
            values = source.CopyValues();
            Cast = new CastStats(values);
            Projectile = new ProjectileStats(values);
            Status = new StatusStats(values);
            Burn = new BurnStats(values);
            Explosion = new ExplosionStats(values);
            Lightning = new LightningStats(values);
            Field = new FieldStats(values);
            Area = new AreaStats(values);
            Beam = new BeamStats(values);
            Chain = new ChainStats(values);
        }

        internal float[] CopyValues() => (float[])values.Clone();

        internal float Get(Stat stat) => values[(int)stat];
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
        public SkillForm Form => (SkillForm)(int)v[(int)Stat.Form];
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
        /// <summary>빙결이 유지되는 동안 매초 입히는 최대 HP 비례 피해 비율</summary>
        public float FreezeMaxHpRatio => v[(int)Stat.FreezeMaxHpRatio];
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

    public readonly struct LightningStats
    {
        private readonly float[] v;
        internal LightningStats(float[] values) => v = values;

        /// <summary>적중할 때 추가로 떨어지는 번개의 피해 비율 (피뢰침)</summary>
        public float StrikeRatio => v[(int)Stat.LightningStrikeRatio];
        /// <summary>처치한 자리 근처 적에게 떨어지는 번개의 피해 비율</summary>
        public float KillRatio => v[(int)Stat.KillLightningRatio];
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
        /// <summary>영역이 시작될 때 중심부에 같은 적중을 더 거는 횟수</summary>
        public int CoreHits => (int)v[(int)Stat.AreaCoreHits];
        public float CoreRadiusRatio => v[(int)Stat.AreaCoreRadiusRatio];
        /// <summary>중심부 적중에서 빙결 지속에 곱하는 배율</summary>
        public float CoreFreezeScale => v[(int)Stat.AreaCoreFreezeScale];
    }

    public readonly struct BeamStats
    {
        private readonly float[] v;
        internal BeamStats(float[] values) => v = values;

        public float Length => v[(int)Stat.BeamLength];
        public float Width => v[(int)Stat.BeamWidth];
        public float Duration => v[(int)Stat.BeamDuration];
        /// <summary>지속 시간 동안의 공격 횟수</summary>
        public float Pulses => v[(int)Stat.BeamPulses];
        /// <summary>메인 대상(겨눈 적)에게 공격마다 더 주는 피해 비율</summary>
        public float FocusBonus => v[(int)Stat.BeamFocusBonus];
        /// <summary>메인 대상을 공격할 때마다 늘어나는 추가 피해 비율의 최대값</summary>
        public float FocusRampMax => v[(int)Stat.BeamFocusRampMax];
        /// <summary>메인 대상 주변 폭발 반경</summary>
        public float FocusBlastRadius => v[(int)Stat.BeamFocusBlastRadius];
        /// <summary>메인 대상 주변 폭발 피해 비율(공격 한 번 피해 대비)</summary>
        public float FocusBlastRatio => v[(int)Stat.BeamFocusBlastRatio];
        /// <summary>광선이 메인 대상에서 꺾여 다른 적으로 이어지는 횟수</summary>
        public int Refractions => (int)v[(int)Stat.BeamRefractions];
        /// <summary>집중 광선 방식인지 (메인 대상까지가 광선 길이, 메인이 죽으면 가장 가까운 적으로 교체)</summary>
        public bool FocusAim => v[(int)Stat.BeamFocusAim] > 0;
    }

    public readonly struct ChainStats
    {
        private readonly float[] v;
        internal ChainStats(float[] values) => v = values;

        public int Bounces => (int)v[(int)Stat.ChainBounces];
        public float JumpRange => v[(int)Stat.ChainJumpRange];
        public float HopInterval => v[(int)Stat.ChainHopInterval];
        public float PathWidth => v[(int)Stat.ChainPathWidth];
    }

    // Only upgrade preparation can mutate values; committed snapshots are read-only.
    internal sealed class SkillStatsBuilder
    {
        private readonly float[] values;

        public SkillStatsBuilder(SkillBaseStats data) => values = SkillStatRegistry.CreateValues(data);

        // 스냅샷을 열어 보지 않고 값 배열째 복사하므로 새 스탯이 복사에서 빠질 수 없다.
        public SkillStatsBuilder(SkillStats source) => values = source.CopyValues();

        public float this[Stat stat]
        {
            get => values[(int)stat];
            set => values[(int)stat] = value;
        }

        public float[] CopyValues() => (float[])values.Clone();
    }
}
