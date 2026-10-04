using System;

namespace Game.Core
{
    public sealed class ProjectileSizeUpgrade : StatDecorator
    {
        private readonly float factor;
        public ProjectileSizeUpgrade(IWeaponStats inner, float percent) : base(inner) => factor = 1 + percent * .01f;
        public override float ProjectileSizeMultiplier => inner.ProjectileSizeMultiplier * factor;
    }

    public sealed class FieldUpgrade : StatDecorator
    {
        private readonly UpgradeType type;
        private readonly float value;
        public FieldUpgrade(IWeaponStats inner, UpgradeType type, float value) : base(inner) { this.type = type; this.value = value; }
        public override float FieldDuration => inner.FieldDuration + (type == UpgradeType.FieldDuration ? value : 0);
        public override float FieldFlatDamage => inner.FieldFlatDamage + (type == UpgradeType.FieldDamageFlat ? value : 0);
        public override float FieldDamageMultiplier => inner.FieldDamageMultiplier * (type == UpgradeType.FieldDamageMultiplier ? 1 + value * .01f : 1);
    }

    public sealed class KillLightningUpgrade : StatDecorator
    {
        private readonly float ratio;
        public KillLightningUpgrade(IWeaponStats inner, float percent) : base(inner) => ratio = percent * 0.01f;
        public override float KillLightningRatio => ratio;
    }

    public sealed class AuxiliaryParalysisUpgrade : StatDecorator
    {
        private readonly float duration;
        public AuxiliaryParalysisUpgrade(IWeaponStats inner, float duration) : base(inner) => this.duration = duration;
        public override float AuxiliaryParalysisDuration => Math.Max(inner.AuxiliaryParalysisDuration, duration);
    }

    public sealed class ParalysisDurationUpgrade : StatDecorator
    {
        private readonly float duration;
        public ParalysisDurationUpgrade(IWeaponStats inner, float duration) : base(inner) => this.duration = duration;
        public override float ParalysisDuration => inner.ParalysisDuration + duration;
    }

    public sealed class FormUpgrade : StatDecorator
    {
        private readonly WeaponForm form;
        public FormUpgrade(IWeaponStats inner, WeaponForm form) : base(inner) => this.form = form;
        public override WeaponForm Form => form;
    }

    public sealed class ImpactDamageUpgrade : StatDecorator
    {
        private readonly float factor;
        public ImpactDamageUpgrade(IWeaponStats inner, float percent) : base(inner) => factor = 1 + percent * 0.01f;
        public override float Damage => inner.Damage * factor;
    }

    public sealed class BurnSpecialUpgrade : StatDecorator
    {
        private readonly float ratio;
        private readonly bool death;
        public BurnSpecialUpgrade(IWeaponStats inner, float value, bool death) : base(inner) { ratio = value * 0.01f; this.death = death; }
        public override float BurnMaxHpRatio => death ? inner.BurnMaxHpRatio : ratio;
        public override bool BurnDeathExplosion => death || inner.BurnDeathExplosion;
    }

    public sealed class BurnUpgrade : StatDecorator
    {
        private readonly float value;
        private readonly bool duration;
        public BurnUpgrade(IWeaponStats inner, float value, bool duration) : base(inner) { this.value = value; this.duration = duration; }
        public override float BurnDuration => duration ? value : inner.BurnDuration;
        public override float BurnRatio => duration ? inner.BurnRatio : value * 0.01f;
    }

    public sealed class AuxiliaryExplosionUpgrade : StatDecorator
    {
        public AuxiliaryExplosionUpgrade(IWeaponStats inner) : base(inner) { }
        public override bool AuxiliaryExplosions => true;
    }

    public sealed class LightningHitUpgrade : StatDecorator
    {
        private readonly float value;
        private readonly UpgradeType type;
        public LightningHitUpgrade(IWeaponStats inner, float value, UpgradeType type) : base(inner) { this.value = value; this.type = type; }
        public override float ParalysisDuration => type == UpgradeType.Paralysis ? Math.Max(inner.ParalysisDuration, value) : inner.ParalysisDuration;
        public override float LightningStrikeRatio => type == UpgradeType.LightningStrike ? value * 0.01f : inner.LightningStrikeRatio;
        public override float AuxiliaryLightningRatio => type == UpgradeType.AuxiliaryLightning ? value * 0.01f : inner.AuxiliaryLightningRatio;
    }

    public sealed class EnableExplosionUpgrade : StatDecorator
    {
        private readonly float radius;
        public EnableExplosionUpgrade(IWeaponStats inner, float radius) : base(inner) => this.radius = radius;
        public override float ExplosionRadius => radius;
    }

    public sealed class ExplosionUpgrade : StatDecorator
    {
        private readonly float factor;
        private readonly bool radius;
        public ExplosionUpgrade(IWeaponStats inner, float value, bool radius) : base(inner) { factor = 1 + value * 0.01f; this.radius = radius; }
        public override float ExplosionDamage => radius ? inner.ExplosionDamage : inner.ExplosionDamage * factor;
        public override float ExplosionRadius => radius ? inner.ExplosionRadius * factor : inner.ExplosionRadius;
    }

    public sealed class FrostbiteUpgrade : StatDecorator
    {
        private readonly float ratio;
        private readonly bool shard;
        public FrostbiteUpgrade(IWeaponStats inner, float percent, bool shard) : base(inner) { ratio = percent * 0.01f; this.shard = shard; }
        public override float FrostbiteRatio => shard ? inner.FrostbiteRatio : ratio;
        public override float ShardFrostbiteRatio => shard ? ratio : inner.ShardFrostbiteRatio;
    }

    public sealed class KnockbackUpgrade : StatDecorator
    {
        private readonly float value;
        public KnockbackUpgrade(IWeaponStats inner, float value) : base(inner) => this.value = value;
        public override float KnockbackDistance => Math.Max(0, inner.KnockbackDistance * (1 + value * 0.01f));
    }

    public sealed class SplitCountUpgrade : StatDecorator
    {
        private readonly int count;
        public SplitCountUpgrade(IWeaponStats inner, int count) : base(inner) => this.count = count;
        public override int SplitCount => inner.SplitCount + count;
    }

    public sealed class ShardDamageUpgrade : StatDecorator
    {
        private readonly float value;
        public ShardDamageUpgrade(IWeaponStats inner, float value) : base(inner) => this.value = value;
        public override float ShardDamageMultiplier => inner.ShardDamageMultiplier * (1 + value * 0.01f);
    }

    public sealed class FreezeDurationUpgrade : StatDecorator
    {
        private readonly float duration;
        public FreezeDurationUpgrade(IWeaponStats inner, float duration) : base(inner) => this.duration = duration;
        public override float FreezeDuration => Math.Max(inner.FreezeDuration, duration);
    }

    public class AttackSpeedUpgrade : StatDecorator
    {
        private readonly float value;
        public AttackSpeedUpgrade(IWeaponStats inner, float value) : base(inner) => this.value = value;
        public override float Cooldown => Math.Max(0.1f, inner.Cooldown * (1f - value * 0.01f));
    }

    public class DamageUpgrade : StatDecorator
    {
        private readonly float value;
        public DamageUpgrade(IWeaponStats inner, float value) : base(inner) => this.value = value;
        public override float Damage => inner.Damage * (1f + value * 0.01f);
        public override float ExplosionDamage => inner.ExplosionDamage * (1f + value * 0.01f);
    }

    public class ProjectileCountUpgrade : StatDecorator
    {
        private readonly float value;
        public ProjectileCountUpgrade(IWeaponStats inner, float value) : base(inner) => this.value = value;
        public override int ProjectileCount => Math.Max(1, inner.ProjectileCount + (int)Math.Round(value));
    }

    public class HitCountUpgrade : StatDecorator
    {
        private readonly int value;
        public HitCountUpgrade(IWeaponStats inner, int value) : base(inner) => this.value = value;
        public override int ProjectileCount => Math.Max(1, inner.ProjectileCount + value);
    }

    public sealed class CastCountUpgrade : StatDecorator
    {
        private readonly int value;
        public CastCountUpgrade(IWeaponStats inner, int value) : base(inner) => this.value = value;
        public override int CastCount => Math.Max(1, inner.CastCount + value);
    }

    public sealed class PierceCountUpgrade : StatDecorator
    {
        private readonly int value;
        public PierceCountUpgrade(IWeaponStats inner, int value) : base(inner) => this.value = value;
        public override int PierceCount => Math.Max(0, inner.PierceCount + value);
    }

    public sealed class ProjectileSpeedUpgrade : StatDecorator
    {
        private readonly float value;
        public ProjectileSpeedUpgrade(IWeaponStats inner, float value) : base(inner) => this.value = value;
        public override float ProjectileSpeed => Math.Max(0f, inner.ProjectileSpeed * (1f + value * 0.01f));
    }
}
