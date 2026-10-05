using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>사방 발사 경로와 폭발 반응(투사체 적중·Hitscan 타격 지점)</summary>
    public sealed class RadialAndExplosionTests
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

        private sealed class Provider : IEnemyTargetProvider
        {
            public readonly List<IEnemyTarget> Targets = new List<IEnemyTarget>();
            public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
            { results.Clear(); results.AddRange(Targets); return results.Count; }
        }

        [Test]
        public void RadialPath_SpreadsShotsEvenlyAroundTheOrigin()
        {
            var directions = Enumerable.Range(0, 6)
                .Select(i => ProjectileLaunchPath.Calculate(ProjectilePath.Radial, Vector3.zero, Vector2.zero, null, 10, 10, 0, i, 6).Direction)
                .ToList();
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(1f, directions[i].magnitude, .001f);
                Assert.AreEqual(60f, Vector3.Angle(directions[i], directions[(i + 1) % 6]), .01f, "이웃한 발과 60도 간격");
            }
            Assert.AreEqual(6, directions.Select(d => Mathf.RoundToInt(Mathf.Atan2(d.y, d.x) * 100)).Distinct().Count());
        }

        [Test]
        public void RadialStrategy_FiresAllShotsEvenWithNoEnemiesAndStartsAtTheOrigin()
        {
            var prefab = new GameObject("RadialPrefab"); prefab.AddComponent<Projectile>();
            var owner = new GameObject("RadialOwner");
            var data = new WeaponData
            {
                id = 9,
                name = "방사",
                maxLevel = 5,
                upgrades = new List<WeaponUpgradeOption>(),
                projectilePath = ProjectilePath.Radial,
                baseStats = new WeaponBaseStats { cast = { baseDamage = 5, cooldown = 1, range = 10, projectileCount = 6 }, projectile = { speed = 10 } }
            };
            SkillCaster caster = null;
            try
            {
                caster = Casters.Projectile(data, prefab, owner.transform, new Provider());
                caster.FireAt(new Vector3(2, 3, 0), config => config);
                var shots = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)
                    .Where(p => p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy && p.name.Contains("Clone")).ToList();
                Assert.AreEqual(6, shots.Count, "적이 없어도 사방으로 쏜다");
                foreach (var shot in shots) { Assert.AreEqual(new Vector3(2, 3, 0), shot.transform.position); }
                var distinct = shots.Select(p => Mathf.RoundToInt(Mathf.Atan2(((Vector3)typeof(Projectile).GetField("direction", Flags).GetValue(p)).y,
                    ((Vector3)typeof(Projectile).GetField("direction", Flags).GetValue(p)).x) * 100)).Distinct().Count();
                Assert.AreEqual(6, distinct);
            }
            finally
            {
                caster?.Dispose();
                foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None)) { Object.DestroyImmediate(p.gameObject); }
                Object.DestroyImmediate(prefab); Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Explosion_DamagesOnlyEnemiesInsideRadiusAtTheEventPosition()
        {
            var near = new HitRecorder { Position = new Vector2(1, 0) };
            var far = new HitRecorder { Position = new Vector2(5, 0) };
            var provider = new Provider(); provider.Targets.Add(near); provider.Targets.Add(far);
            var reactions = new HitReactionBuilder().Explosion(AttackEvent.Hit, provider, 2, 10).Build();
            reactions.Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up));
            Assert.AreEqual(10, near.Damage);
            Assert.AreEqual(0, far.Damage);
        }

        [Test]
        public void Explosion_IsNotBuiltWithoutRadiusOrProvider()
        {
            var target = new HitRecorder { Position = Vector2.zero };
            var provider = new Provider(); provider.Targets.Add(target);
            new HitReactionBuilder().Explosion(AttackEvent.Hit, provider, 0, 10).Build()
                .Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up));
            new HitReactionBuilder().Explosion(AttackEvent.Hit, null, 2, 10).Build()
                .Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up));
            Assert.AreEqual(0, target.Damage);
        }

        [Test]
        public void ProjectileCompiler_AddsExplosionFromStats()
        {
            var stats = WeaponStats.FromDefinition(new WeaponBaseStats
            {
                cast = { baseDamage = 20 },
                explosion = { radius = 2, damageRatio = .5f }
            });
            var near = new HitRecorder { Position = new Vector2(1, 0) };
            var provider = new Provider(); provider.Targets.Add(near);
            var hit = new HitRecorder { Position = new Vector2(10, 10) };
            ReactionCompiler.ForProjectile(stats, null, provider).Raise(AttackEvent.Hit, new AttackContext(Vector2.zero, Vector3.up, hit));
            Assert.AreEqual(20, hit.Damage, "직접 피해");
            Assert.AreEqual(10, near.Damage, "폭발 피해는 피해 × 비율");
        }

        [Test]
        public void HitscanImpact_CarriesTheStruckTargetAsTheHitEnemy()
        {
            var go = new GameObject("ImpactTargetTest");
            try
            {
                var effect = go.AddComponent<HitscanEffect>();
                var pool = new UnityEngine.Pool.ObjectPool<HitscanEffect>(() => effect);
                var struck = new HitRecorder { Position = Vector2.zero };
                IEnemyTarget seen = null;
                var reactions = new HitReactionBuilder()
                    .On(AttackEvent.Impact, new CastSkillReaction(c => seen = c.Target)).Build();
                pool.Get().Init(pool, Vector3.zero, 10, reactions: reactions, sourceTarget: struck);
                typeof(HitscanEffect).GetMethod("Hit", Flags).Invoke(effect, null);
                Assert.AreSame(struck, seen);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
