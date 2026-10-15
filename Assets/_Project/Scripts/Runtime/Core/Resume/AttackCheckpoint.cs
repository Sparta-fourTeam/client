using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Combat;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    internal sealed class AttackRestoreContext
    {
        internal IEnemyTargetProvider Targets;
        internal Func<int, IEnemyTarget> Enemy;
        internal IChildSkillCaster Children;
        internal TargetReservations Reservations;
    }

    internal static class SavedAttack
    {
        // 캡처는 메인 스레드에서 순차 실행된다. 임시 컴포넌트 참조는 작업 뒤 비운다.
        private static readonly List<Animator> AnimatorScratch = new();
        internal static int Id(IEnemyTarget target) => target is Enemy enemy ? enemy.Id : 0;
        internal static AttackRuntimeState Capture(string kind, Transform transform, DamageSource source, AttackReactions reactions)
        {
            var s = new AttackRuntimeState
            {
                Kind = kind,
                Config = SavedSkillConfig.Capture(reactions?.SnapshotConfig),
                SkillId = source.SkillId,
                Element = source.Element,
                SourceCastType = source.CastType,
                Position = transform.position,
                Scale = transform.localScale
            };
            try
            {
                transform.GetComponentsInChildren(true, AnimatorScratch);
                for (int i = 0; i < AnimatorScratch.Count; i++)
                {
                    var animator = AnimatorScratch[i];
                    for (int layer = 0; layer < animator.layerCount; layer++)
                    {
                        var state = animator.GetCurrentAnimatorStateInfo(layer);
                        s.Animators.Add(new AnimatorRuntimeState { Index = i, Layer = layer, Hash = state.fullPathHash, Time = state.normalizedTime, Speed = animator.speed });
                    }
                }
            }
            finally { AnimatorScratch.Clear(); }
            return s;
        }
        internal static DamageSource Source(AttackRuntimeState s) => new(s.SkillId, s.Element, s.SourceCastType);
        internal static void Transform(AttackRuntimeState s, Transform transform)
        {
            transform.position = s.Position; transform.localScale = s.Scale;
            var animators = transform.GetComponentsInChildren<Animator>(true);
            foreach (var state in s.Animators)
            {
                if (state.Index >= animators.Length || state.Hash == 0) { continue; }
                animators[state.Index].speed = state.Speed;
                animators[state.Index].Play(state.Hash, state.Layer, state.Time);
            }
        }
        internal static SkillConfig Config(AttackRuntimeState s, AttackRestoreContext context) => SavedSkillConfig.Restore(s.Config, context.Children);
        internal static Action<Vector2> BurnCallback(SkillStats stats, IEnemyTargetProvider targets) =>
            stats.Burn.DeathExplosion ? new SavedBurnExplosion(targets, stats.Explosion.Radius, stats.Explosion.Damage).Invoke : null;
    }

    public partial class Projectile
    {
        internal AttackRuntimeState CaptureAttack()
        {
            var s = SavedAttack.Capture("projectile", transform, sourceSkill, reactions);
            s.Direction = direction; s.Speed = speed; s.Lifetime = lifetime; s.Elapsed = elapsed; s.Radius = hitRadius;
            s.IgnoreId = SavedAttack.Id(ignoredTarget); s.RemainingHits = hitLedger.SavedRemaining;
            hitLedger.CaptureTargets(s.Visited);
            if (reservation != null)
            {
                s.TargetId = SavedAttack.Id(reservation.Target); s.ReservationDamage = reservation.Damage;
                s.ReservationRemaining = reservation.Remaining;
            }
            return s;
        }
        internal void RestoreAttack(AttackRuntimeState s, IObjectPool<Projectile> owner, AttackRestoreContext context)
        {
            var config = SavedAttack.Config(s, context); pool = owner; sourceSkill = SavedAttack.Source(s);
            reactions = config.Reactions; hitReactions = ReactionCompiler.ForProjectile(config.Stats, SavedAttack.BurnCallback(config.Stats, context.Targets), context.Targets);
            targetProvider = context.Targets; randomValue = () => UnityEngine.Random.value;
            direction = s.Direction; speed = s.Speed; lifetime = s.Lifetime; elapsed = s.Elapsed; hitRadius = s.Radius;
            ignoredTarget = context.Enemy(s.IgnoreId); released = false; compareHits ??= CompareHitsAlongSegment;
            ClearReservation(); reservation = context.Reservations.Reserve(context.Enemy(s.TargetId), s.ReservationDamage, s.ReservationRemaining);
            hitLedger.Restore(s.RemainingHits, s.Visited.Select(context.Enemy).Where(e => e != null));
            SetVisualForm(config.Stats.Cast.Form); SavedAttack.Transform(s, transform); ApplyDirectionRoration();
        }
    }

    public sealed partial class ProjectileHitLedger
    {
        internal int SavedRemaining => remainingHits;
        internal void CaptureTargets(List<int> result)
        {
            foreach (var target in hitTargets)
            { if (target is Enemy enemy) { result.Add(enemy.Id); } }
        }
        internal void Restore(int remaining, IEnumerable<IEnemyTarget> targets)
        { remainingHits = remaining; hitTargets.Clear(); foreach (var target in targets) { hitTargets.Add(target); } }
    }

    public sealed partial class AreaZone
    {
        internal AttackRuntimeState CaptureAttack()
        {
            var s = SavedAttack.Capture("area", transform, sourceSkill, reactions);
            s.Radius = radius; s.Lifetime = duration; s.Interval = pulseInterval; s.MoveSpeed = moveSpeed; s.Pull = pull;
            s.Length = length; s.Width = width; s.FocusBonus = focusBonus; s.BaseDamage = baseDamage;
            s.FocusRampMax = focusRampMax; s.BlastRadius = focusBlastRadius; s.BlastRatio = focusBlastRatio;
            s.MainHits = mainHits; s.Refractions = refractions; s.FocusAim = focusAim; s.CoreHits = coreHits;
            s.CoreRadius = coreRadius; s.CoreFreezeScale = coreFreezeScale; s.CoreDone = coreDone;
            s.Points = path.ToList(); s.Visited = visited.Select(SavedAttack.Id).ToList();
            s.Elapsed = elapsed; s.SincePulse = sincePulse; s.MoveTarget = moveTarget; s.TargetId = SavedAttack.Id(aim); s.Direction = beamDirection;
            return s;
        }
        internal void RestoreAttack(AttackRuntimeState s, IObjectPool<AreaZone> owner, AttackRestoreContext context)
        {
            var config = SavedAttack.Config(s, context); pool = owner; provider = context.Targets; sourceSkill = SavedAttack.Source(s);
            reactions = config.Reactions; hitReactions = ReactionCompiler.ForProjectile(config.Stats, null, context.Targets); randomValue = () => UnityEngine.Random.value;
            radius = s.Radius; duration = s.Lifetime; pulseInterval = s.Interval; moveSpeed = s.MoveSpeed; pull = s.Pull;
            length = s.Length; width = s.Width; focusBonus = s.FocusBonus; baseDamage = s.BaseDamage;
            focusRampMax = s.FocusRampMax; focusBlastRadius = s.BlastRadius; focusBlastRatio = s.BlastRatio;
            mainHits = s.MainHits; refractions = s.Refractions; focusAim = s.FocusAim; coreHits = s.CoreHits;
            coreRadius = s.CoreRadius; coreFreezeScale = s.CoreFreezeScale; coreDone = s.CoreDone;
            path.Clear(); path.AddRange(s.Points); visited.Clear(); visited.AddRange(s.Visited.Select(context.Enemy).Where(e => e != null));
            elapsed = s.Elapsed; sincePulse = s.SincePulse; moveTarget = s.MoveTarget; aim = context.Enemy(s.TargetId); beamDirection = s.Direction;
            released = false; SavedAttack.Transform(s, transform); UpdateVisual();
        }
    }

    public sealed partial class ChainBolt
    {
        internal AttackRuntimeState CaptureAttack()
        {
            var s = SavedAttack.Capture("chain", transform, sourceSkill, reactions);
            s.Position = position; s.TargetId = SavedAttack.Id(current); s.RemainingHits = remaining; s.SincePulse = sinceHop; s.Started = started;
            s.Visited = visited.Select(SavedAttack.Id).ToList(); s.Points = points.Select(p => (Vector2)p).ToList();
            return s;
        }
        internal void RestoreAttack(AttackRuntimeState s, IObjectPool<ChainBolt> owner, AttackRestoreContext context)
        {
            var config = SavedAttack.Config(s, context); pool = owner; provider = context.Targets; sourceSkill = SavedAttack.Source(s);
            reactions = config.Reactions; hitReactions = ReactionCompiler.ForChain(config.Stats, context.Targets); settings = ChainSettings.From(config.Stats.Chain);
            randomValue = () => UnityEngine.Random.value; current = context.Enemy(s.TargetId); position = s.Position;
            remaining = s.RemainingHits; sinceHop = s.SincePulse; started = s.Started; released = false;
            visited.Clear(); foreach (var id in s.Visited) { var enemy = context.Enemy(id); if (enemy != null) { visited.Add(enemy); } }
            points.Clear(); points.AddRange(s.Points.Select(p => (Vector3)p)); SavedAttack.Transform(s, transform); RefreshLine();
        }
    }

    public partial class HitscanEffect
    {
        internal AttackRuntimeState CaptureAttack()
        {
            var s = SavedAttack.Capture("hitscan", transform, sourceSkill, reactions);
            s.Config = SavedSkillConfig.Capture(checkpointConfig);
            s.Damage = damage; s.Radius = activeRadius; s.TargetId = SavedAttack.Id(directTarget); s.IgnoreId = SavedAttack.Id(sourceTarget);
            s.Elapsed = elapsed; s.HitDone = hitDone; s.Secondary = directTarget != null;
            return s;
        }
        internal void RestoreAttack(AttackRuntimeState s, IObjectPool<HitscanEffect> owner, AttackReactions restoredReactions, AttackRestoreContext context, SkillConfig restoredConfig)
        {
            pool = owner; sourceSkill = SavedAttack.Source(s); reactions = restoredReactions;
            checkpointConfig = restoredConfig;
            damage = s.Damage; activeRadius = s.Radius; directTarget = context.Enemy(s.TargetId); sourceTarget = context.Enemy(s.IgnoreId);
            elapsed = s.Elapsed; hitDone = s.HitDone; released = false;
            // 죽은 적에게 가던 보조 타격이 광역 타격으로 바뀌지 않게 한다.
            if (s.Secondary && directTarget == null) { hitDone = true; }
            SavedAttack.Transform(s, transform);
        }
    }

    public sealed partial class ElectromagneticField
    {
        internal AttackRuntimeState CaptureAttack()
        {
            var s = SavedAttack.Capture("field", transform, sourceSkill, null);
            s.Radius = radius; s.Damage = damage; s.SlowRatio = slowRatio; s.FieldRemaining = remaining; s.FieldElapsed = elapsed;
            return s;
        }
        internal void RestoreAttack(AttackRuntimeState s, IObjectPool<ElectromagneticField> owner, AttackRestoreContext context)
        {
            ClearSlows(); pool = owner; provider = context.Targets; sourceSkill = SavedAttack.Source(s);
            radius = s.Radius; damage = s.Damage; slowRatio = s.SlowRatio; remaining = s.FieldRemaining; elapsed = s.FieldElapsed; released = false;
            SavedAttack.Transform(s, transform); ShowVisual(radius); RefreshOccupants();
        }
    }
}
