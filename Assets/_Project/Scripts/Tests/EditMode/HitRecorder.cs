using System;
using System.Reflection;
using Game.Core;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>적중 반응이 대상에게 건 값을 기록한다. 투사체의 적중 반응을 직접 실행해 결과 수치를 확인하는 데 쓴다.</summary>
    internal sealed class HitRecorder : IEnemyTarget, IFreezableTarget, IKnockbackTarget, IFrostbiteTarget,
        IParalyzableTarget, IBurnableTarget, IStunnableTarget, ISlowableTarget, IVulnerableTarget
    {
        public Vector2 Position { get; set; }
        /// <summary>true면 죽은 적으로 보인다 (조준과 피해에서 건너뛴다)</summary>
        public bool Dead;
        public bool IsDead => Dead;
        public int Damage;
        public Vector2 KnockbackDirection;
        public float Freeze, KnockbackDistance, Frostbite, Paralysis, BurnDamage, BurnDuration, BurnMaxHpRatio, Stun,
            SlowRatio, SlowDuration, VulnerabilityRatio, VulnerabilityDuration;

        public void TakeDamage(int amount) => Damage += amount;
        public void ApplyFreeze(float duration) => Freeze = duration;
        public void ApplyKnockback(Vector2 direction, float distance) { KnockbackDirection = direction; KnockbackDistance = distance; Position += direction.normalized * distance; }
        public void ApplyFrostbite(float damagePerSecond) => Frostbite = damagePerSecond;
        public void ApplyParalysis(float duration) => Paralysis = duration;
        public void ApplyBurn(float damagePerSecond, float duration, float maxHpRatio = 0, Action<Vector2> onDeath = null)
        {
            BurnDamage = damagePerSecond;
            BurnDuration = duration;
            BurnMaxHpRatio = maxHpRatio;
        }
        public void ApplyStun(float duration) => Stun = duration;
        public void ApplySlow(float ratio, float duration) { SlowRatio = ratio; SlowDuration = duration; }
        public void ApplyVulnerability(float ratio, float duration) { VulnerabilityRatio = ratio; VulnerabilityDuration = duration; }

        /// <summary>투사체가 가진 적중 반응을 새 대상에게 실행한다. random이 작을수록 확률 효과가 걸린다 (기본 0 = 모두 성공)</summary>
        public static HitRecorder Hit(Projectile projectile, float random = 0) => HitAt(projectile, new HitRecorder(), random);

        /// <summary>투사체의 적중 반응(피해·상태이상·폭발)을 지정한 대상의 위치에서 실행한다</summary>
        public static HitRecorder HitAt(Projectile projectile, HitRecorder target, float random = 0)
        {
            var reactions = (AttackReactions)typeof(Projectile).GetField("hitReactions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(projectile);
            reactions.Raise(AttackEvent.Hit, new AttackContext(target.Position, Vector3.up, target, () => random));
            return target;
        }
    }
}
