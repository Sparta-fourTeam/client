using UnityEngine;

namespace Game.Core
{
    public class AttackSpeedUpgrade : StatDecorator
    {
        private readonly float value;
        public AttackSpeedUpgrade(IWeaponStats inner, float value) : base(inner) => this.value = value;
        public override float Cooldown => Mathf.Max(0.1f, inner.Cooldown * (1f - value * 0.01f));
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
        public override int HitCount => inner.HitCount + Mathf.RoundToInt(value);
    }

    public class HitCountUpgrade : StatDecorator
    {
        private readonly int value;
        public HitCountUpgrade(IWeaponStats inner, int value) : base(inner) => this.value = value;
        public override int HitCount => inner.HitCount + Mathf.RoundToInt(value);
    }
}
