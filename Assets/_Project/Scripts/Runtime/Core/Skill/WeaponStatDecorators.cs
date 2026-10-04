using System;

namespace Game.Core
{
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
