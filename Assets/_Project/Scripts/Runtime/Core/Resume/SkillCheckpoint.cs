using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    internal static class SavedSkillConfig
    {
        internal static SkillConfigState Capture(SkillConfig config)
        {
            if (config == null) { return null; }
            return config.CheckpointCache ??= Create(config);
        }
        private static SkillConfigState Create(SkillConfig config) => new()
        {
            Stats = config.Stats.CopyValues(),
            Type = config.Attack.Type,
            Path = config.Attack.Path,
            Range = config.Attack.Range,
            Effects = config.SnapshotEffects.ToList(),
            Children = config.Children.ToDictionary(p => p.Key, p => p.Value.Inherit.ToDictionary(r => r.Stat.ToString(), r => r.Scale)),
            Overlays = config.Overlays.ToDictionary(p => p.Key, p => p.Value.ToList())
        };

        internal static SkillConfig Restore(SkillConfigState state, IChildSkillCaster caster)
        {
            if (state == null || state.Stats?.Length != (int)Stat.Count) { throw new InvalidOperationException("스킬 저장 형식이 다릅니다."); }
            var values = new SkillStatsBuilder(new SkillBaseStats());
            for (int i = 0; i < state.Stats.Length; i++) { values[(Stat)i] = state.Stats[i]; }
            var stats = new SkillStats(values);
            var attack = new AttackDefinition(state.Type, state.Path, state.Range);
            var builder = new SkillConfigBuilder(new SkillConfig(stats, attack, Array.Empty<ReactionBinding>(), caster));
            if (!builder.TryApplyCatalog(state.Effects)) { throw new InvalidOperationException("저장된 강화 효과를 복원할 수 없습니다."); }
            var prepared = builder.Build();
            var links = new Dictionary<int, ChildLink>();
            foreach (var pair in state.Children)
            {
                if (!ChildLink.TryParseInherit(pair.Value, out var rules)) { throw new InvalidOperationException("자식 스킬 상속이 잘못되었습니다."); }
                links[pair.Key] = new ChildLink(pair.Key, rules);
            }
            return new SkillConfig(stats, attack, prepared.Bindings, caster, links,
                state.Overlays.ToDictionary(p => p.Key, p => (IReadOnlyList<EffectDef>)p.Value), state.Effects);
        }
    }

    public abstract partial class SkillBase
    {
        internal SkillRuntimeState CaptureSkill(int skillId, bool child) => new()
        {
            SkillId = skillId,
            Child = child,
            Level = upgrades.Level,
            Acquired = new Dictionary<string, int>(upgrades.Counts),
            Cooldown = castClock.RemainingCooldown,
            CastIndex = castClock.SavedIndex,
            RemainingCasts = castClock.SavedRemaining,
            RepeatTimer = castClock.SavedRepeat,
            Config = SavedSkillConfig.Capture(Config)
        };

        internal void RestoreSkill(SkillRuntimeState state, IChildSkillCaster childCaster)
        {
            config = SavedSkillConfig.Restore(state.Config, childCaster);
            stats = config.Stats;
            upgrades.Restore(state.Level, state.Acquired);
            castClock.Restore(state);
        }
    }

    internal sealed partial class SkillUpgradeState
    {
        internal IReadOnlyDictionary<string, int> Counts => acquiredCounts;
        internal void Restore(int level, Dictionary<string, int> counts)
        {
            Level = level; acquiredCounts.Clear();
            foreach (var pair in counts) { acquiredCounts.Add(pair.Key, pair.Value); }
        }
    }

    public sealed partial class CastClock
    {
        internal int SavedIndex => CastIndex;
        internal int SavedRemaining => remainingCasts;
        internal float SavedRepeat => repeatTimer;
        internal void Restore(SkillRuntimeState state)
        {
            RemainingCooldown = state.Cooldown; CastIndex = state.CastIndex;
            remainingCasts = state.RemainingCasts; repeatTimer = state.RepeatTimer;
        }
    }

    public sealed partial class ReactiveCastClock
    {
        internal void Capture(SkillRuntimeState state)
        { state.ReserveCooldown = cooldown; state.ReserveTimer = repeat; state.ReserveCasts = pending; }
        internal void Restore(SkillRuntimeState state)
        { cooldown = state.ReserveCooldown; repeat = state.ReserveTimer; pending = state.ReserveCasts; }
    }

    /// <summary>점화 사망 폭발의 값과 실행 코드를 분리해 재실행 없이 복원한다.</summary>
    internal sealed class SavedBurnExplosion
    {
        private readonly IEnemyTargetProvider targets;
        internal readonly float Radius, Damage;
        internal SavedBurnExplosion(IEnemyTargetProvider targets, float radius, float damage)
        { this.targets = targets; Radius = radius; Damage = damage; }
        internal void Invoke(Vector2 position) => SkillReactionEffects.Explode(targets, position, Radius, Damage, "Explosion");
        internal BurnExplosionState Capture() => new() { Radius = Radius, Damage = Damage };
    }
}

namespace Game.Core
{
    public partial class SkillController
    {
        public List<SkillRuntimeState> CaptureRuntime()
        {
            var result = new List<SkillRuntimeState>(skills.Count + childCaster.SavedCount);
            foreach (var skill in skills) { result.Add(CaptureCaster((SkillCaster)skill, false)); }
            foreach (var skill in childCaster.SavedChildren) { if (skill != null) { result.Add(CaptureCaster(skill, true)); } }
            return result;
        }
        private static SkillRuntimeState CaptureCaster(SkillCaster skill, bool child)
        {
            var state = skill.CaptureSkill(skill.Data.id, child);
            switch (skill.Strategy)
            {
                case ProjectileStrategy s: s.CaptureRuntime(state); break;
                case HitscanStrategy s: s.CaptureRuntime(state); break;
                case AreaStrategy s: s.CaptureRuntime(state); break;
                case BeamStrategy s: s.CaptureRuntime(state); break;
                case ChainStrategy s: s.CaptureRuntime(state); break;
                default: throw new InvalidOperationException("저장할 수 없는 공격 전략입니다.");
            }
            return state;
        }
        public void RestoreRuntime(List<SkillRuntimeState> states, Func<int, IEnemyTarget> enemy)
        {
            ClearWeapons(); childCaster.ClearSavedChildren();
            var restored = new List<(SkillCaster Skill, SkillRuntimeState State)>();
            foreach (var state in states)
            {
                SkillCaster skill;
                if (state.Child) { skill = childCaster.RestoreChild(state.SkillId); }
                else
                {
                    if (!AddWeapon(state.SkillId)) { throw new InvalidOperationException("저장된 스킬을 생성하지 못했습니다."); }
                    skill = (SkillCaster)skills[^1];
                }
                if (skill == null) { throw new InvalidOperationException("저장된 자식 스킬을 생성하지 못했습니다."); }
                skill.RestoreSkill(state, childCaster); restored.Add((skill, state));
            }
            var context = new AttackRestoreContext { Targets = targetProvider, Enemy = enemy, Children = childCaster, Reservations = reservations };
            foreach (var (skill, state) in restored)
            {
                switch (skill.Strategy)
                {
                    case ProjectileStrategy s: s.RestoreRuntime(state, context); break;
                    case HitscanStrategy s: s.RestoreRuntime(state, context); break;
                    case AreaStrategy s: s.RestoreRuntime(state, context); break;
                    case BeamStrategy s: s.RestoreRuntime(state, context); break;
                    case ChainStrategy s: s.RestoreRuntime(state, context); break;
                }
            }
            PublishSkills();
        }
        internal SavedChoice SaveChoice(UpgradeChoice choice) => new()
        {
            SkillId = choice.IsNewWeapon ? choice.newSkillData.id : choice.skill?.Data.id ?? 0,
            CardId = choice.IsGeneral ? choice.GeneralCard.Id : choice.Option?.id,
            NewWeapon = choice.IsNewWeapon,
            General = choice.IsGeneral
        };
        internal UpgradeChoice RestoreChoice(SavedChoice s, GameDataStore data)
        {
            if (s.General)
            {
                var general = data.GeneralCards.Single(c => c.Id == s.CardId);
                return new UpgradeChoice { GeneralCard = general, DisplayName = general.Name, DisplayDescription = general.Desc };
            }
            if (s.NewWeapon)
            {
                var definition = dataTable[s.SkillId];
                return new UpgradeChoice { IsNewWeapon = true, newSkillData = definition, DisplayName = definition.name, DisplayDescription = definition.desc };
            }
            var owned = skills.Single(w => w.Data.id == s.SkillId);
            var option = owned.Data.upgrades.Single(o => o.id == s.CardId);
            SkillUpgradeResolver.TryResolve(option, GetPermanentWeaponLevel(s.SkillId), out var resolved);
            FormHintFinder.TryGetForm(option, GetPermanentWeaponLevel(s.SkillId), out var form);
            return new UpgradeChoice { skill = owned, Option = option, DisplayName = resolved.Name, DisplayDescription = resolved.Description, TargetForm = form };
        }
    }

    public sealed partial class ChildSkillCaster
    {
        internal Dictionary<int, SkillCaster>.ValueCollection SavedChildren => children.Values;
        internal int SavedCount => children.Count;
        internal void ClearSavedChildren() => Dispose();
        internal SkillCaster RestoreChild(int id)
        {
            var child = create(id); children[id] = child; return child;
        }
    }

    public sealed partial class ProjectileStrategy
    {
        internal void CaptureRuntime(SkillRuntimeState state)
        {
            reserveClock.Capture(state);
            ownedPool.CaptureActive(state.Attacks, p => p.CaptureAttack());
            foreach (var pool in formPools.Values) { pool.CaptureActive(state.Attacks, p => p.CaptureAttack()); }
        }
        private UnityEngine.Pool.ObjectPool<Projectile> RestorePool(SkillForm form)
        {
            if (!formPrefabs.TryGetValue(form, out var prefab)) { return Pool; }
            if (!formPools.TryGetValue(form, out var pool))
            {
                pool = new SkillObjectPool<Projectile>(() => UnityEngine.Object.Instantiate(prefab).GetComponent<Projectile>(), 10, 10);
                formPools[form] = pool;
            }
            return pool.Pool;
        }
        internal void RestoreRuntime(SkillRuntimeState state, AttackRestoreContext context)
        {
            reserveClock.Restore(state);
            foreach (var attack in state.Attacks)
            {
                var config = SavedAttack.Config(attack, context); var pool = RestorePool(config.Stats.Cast.Form);
                pool.Get().RestoreAttack(attack, pool, context);
            }
        }
    }

    public sealed partial class AreaStrategy
    {
        internal void CaptureRuntime(SkillRuntimeState state)
        {
            foreach (var target in burstTargets) { state.BurstTargets.Add(SavedAttack.Id(target)); }
            ownedPool.CaptureActive(state.Attacks, p => p.CaptureAttack());
        }
        internal void RestoreRuntime(SkillRuntimeState state, AttackRestoreContext context)
        {
            burstTargets.Clear(); foreach (var id in state.BurstTargets) { var enemy = context.Enemy(id); if (enemy != null) { burstTargets.Add(enemy); } }
            foreach (var attack in state.Attacks) { Pool.Get().RestoreAttack(attack, Pool, context); }
        }
    }
    public sealed partial class BeamStrategy
    {
        internal void CaptureRuntime(SkillRuntimeState state) => ownedPool.CaptureActive(state.Attacks, p => p.CaptureAttack());
        internal void RestoreRuntime(SkillRuntimeState state, AttackRestoreContext context)
        { foreach (var attack in state.Attacks) { Pool.Get().RestoreAttack(attack, Pool, context); } }
    }
    public sealed partial class ChainStrategy
    {
        internal void CaptureRuntime(SkillRuntimeState state) => ownedPool.CaptureActive(state.Attacks, p => p.CaptureAttack());
        internal void RestoreRuntime(SkillRuntimeState state, AttackRestoreContext context)
        { foreach (var attack in state.Attacks) { Pool.Get().RestoreAttack(attack, Pool, context); } }
    }
    public sealed partial class HitscanStrategy
    {
        internal void CaptureRuntime(SkillRuntimeState state)
        {
            ownedPool.CaptureActive(state.Attacks, p => p.CaptureAttack());
            foreach (var pool in formPools.Values) { pool.CaptureActive(state.Attacks, p => p.CaptureAttack()); }
            ownedFieldPool.CaptureActive(state.Attacks, p => p.CaptureAttack());
        }
        private UnityEngine.Pool.ObjectPool<HitscanEffect> RestorePool(SkillForm form)
        {
            if (!formPrefabs.TryGetValue(form, out var prefab)) { return Pool; }
            if (!formPools.TryGetValue(form, out var pool))
            {
                pool = new SkillObjectPool<HitscanEffect>(() => UnityEngine.Object.Instantiate(prefab).GetComponent<HitscanEffect>(), 5, 20);
                formPools[form] = pool;
            }
            return pool.Pool;
        }
        internal void RestoreRuntime(SkillRuntimeState state, AttackRestoreContext context)
        {
            foreach (var attack in state.Attacks)
            {
                if (attack.Kind == "field") { fieldPool.Get().RestoreAttack(attack, fieldPool, context); continue; }
                var config = attack.Config == null ? null : SavedAttack.Config(attack, context);
                var pool = attack.Secondary ? Pool : RestorePool(config.Stats.Cast.Form);
                var reactions = AttackReactions.Empty;
                if (config != null)
                {
                    var compiled = new HitscanCastEffects(config.Stats, config.Attack.Range, context.Targets, pool, fieldPool, originalScale,
                        config.Reactions, Pool, originalScale);
                    reactions = compiled.CompileReactions().Then(config.Reactions);
                }
                pool.Get().RestoreAttack(attack, pool, reactions, context);
            }
        }
    }
}
