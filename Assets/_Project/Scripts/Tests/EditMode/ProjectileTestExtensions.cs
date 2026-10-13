using System;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Tests
{
    using Game.Core;

    /// <summary>테스트에서 수치를 나열해 투사체를 띄우는 편의 함수. 수치는 운영 코드와 같은 HitReactionBuilder로 반응이 된다.</summary>
    internal static class ProjectileTestExtensions
    {
        public static void Init(this Projectile projectile, IObjectPool<Projectile> pool, Vector3 startPos, Vector3 direction,
            float damage, float speed, float lifetime, IEnemyTargetProvider targetProvider, int pierceCount = 0,
            float freezeDuration = 0, Action<Vector2, Vector3, IEnemyTarget> onHit = null, IEnemyTarget ignoredTarget = null,
            float knockbackDistance = 0, float frostbiteRatio = 0, float paralysisDuration = 0, float lightningDamage = 0,
            float paralysisChance = 1, Func<float> randomValue = null, float burnDuration = 0, float burnDamage = 0,
            float burnMaxHpRatio = 0, Action<Vector2> burnOnDeath = null, float freezeChance = 1, float frostbiteChance = 1,
            float burnChance = 1, float collisionRadius = .3f, float stunDuration = 0, float stunChance = 1,
            float slowDuration = 0, float slowRatio = 0, float vulnerabilityRatio = 0, float vulnerabilityDuration = 0)
        {
            projectile.Init(pool, new ProjectileSpawnSettings
            {
                StartPos = startPos,
                Direction = direction,
                Speed = speed,
                Lifetime = lifetime,
                TargetProvider = targetProvider,
                PierceCount = pierceCount,
                IgnoredTarget = ignoredTarget,
                RandomValue = randomValue,
                CollisionRadius = collisionRadius,
                HitReactions = new HitReactionBuilder()
                    .Damage(damage)
                    .Freeze(freezeDuration, freezeChance)
                    .Knockback(knockbackDistance)
                    .Frostbite(damage * frostbiteRatio, frostbiteChance)
                    .Paralysis(paralysisDuration, paralysisChance)
                    .LightningStrike(lightningDamage)
                    .Burn(burnDamage, burnDuration, burnMaxHpRatio, burnChance, burnOnDeath)
                    .Stun(stunDuration, stunChance)
                    .Slow(slowRatio, slowDuration)
                    .Vulnerability(vulnerabilityRatio, vulnerabilityDuration)
                    .On(AttackEvent.Hit, new CastSkillReaction(c => onHit?.Invoke(c.Position, c.Direction, c.Target)))
                    .Build()
            });
        }
    }
}
