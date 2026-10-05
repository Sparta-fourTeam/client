using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Impact는 범위형 공격이 목표 지점에 한 번 닿았을 때(적 수와 무관하게 1회)다. Hit는 적중한 적마다 발생한다.</summary>
    public enum AttackEvent { Start, Hit, Kill, Expired, Tick, Bounce, Impact }

    public readonly struct AttackContext
    {
        public Vector2 Position { get; }
        public Vector3 Direction { get; }
        public IEnemyTarget Target { get; }
        public Func<float> RandomValue { get; }
        public float DeltaTime { get; }
        public float Elapsed { get; }

        public AttackContext(Vector2 position, Vector3 direction, IEnemyTarget target = null, Func<float> randomValue = null,
            float deltaTime = 0, float elapsed = 0)
        {
            Position = position;
            Direction = direction;
            Target = target;
            RandomValue = randomValue;
            DeltaTime = deltaTime;
            Elapsed = elapsed;
        }
    }

    public interface IAttackReaction
    {
        void Execute(AttackContext context);
    }

    public readonly struct ReactionBinding
    {
        public AttackEvent Event { get; }
        public IAttackReaction Reaction { get; }
        public ReactionBinding(AttackEvent trigger, IAttackReaction reaction)
        {
            Event = trigger;
            Reaction = reaction ?? throw new ArgumentNullException(nameof(reaction));
        }
    }

    /// <summary>Fixed reaction order. Events run locally on an attack, independently of the scene message bus.</summary>
    public sealed class AttackReactions
    {
        private readonly ReactionBinding[] bindings;
        public static readonly AttackReactions Empty = new AttackReactions(Array.Empty<ReactionBinding>());
        public AttackReactions(IEnumerable<ReactionBinding> bindings) => this.bindings = new List<ReactionBinding>(bindings).ToArray();

        /// <summary>이 반응들 뒤에 other를 이어 붙인 새 반응 묶음 (두 묶음은 바뀌지 않는다)</summary>
        public AttackReactions Then(AttackReactions other)
        {
            if (other == null || other.bindings.Length == 0) { return this; }
            if (bindings.Length == 0) { return other; }
            var combined = new List<ReactionBinding>(bindings);
            combined.AddRange(other.bindings);
            return new AttackReactions(combined);
        }

        public void Raise(AttackEvent trigger, AttackContext context)
        {
            foreach (var binding in bindings)
            {
                if (binding.Event == trigger) { binding.Reaction.Execute(context); }
            }
        }
    }

    public sealed class DamageReaction : IAttackReaction
    {
        private readonly int damage;
        public DamageReaction(float damage, bool minimumOne = false) => this.damage = minimumOne ? Mathf.Max(1, (int)damage) : (int)damage;
        public void Execute(AttackContext context) => context.Target?.TakeDamage(damage);
    }

    public sealed class StatusReaction<TTarget> : IAttackReaction where TTarget : class
    {
        private readonly float chance;
        private readonly Action<TTarget, AttackContext> apply;
        public StatusReaction(float chance, Action<TTarget, AttackContext> apply)
        {
            this.chance = chance;
            this.apply = apply;
        }
        public void Execute(AttackContext context)
        {
            if (context.Target is TTarget target && StatusProc.Roll(chance, context.RandomValue)) { apply(target, context); }
        }
    }

    /// <summary>Invokes a child cast at the event position; the child owns its own attack and reactions.</summary>
    public sealed class CastSkillReaction : IAttackReaction
    {
        private readonly Action<AttackContext> cast;
        private readonly int count;
        private readonly float chance;
        public CastSkillReaction(Action<AttackContext> cast, int count = 1, float chance = 1)
        {
            this.cast = cast ?? throw new ArgumentNullException(nameof(cast));
            if (count < 1) { throw new ArgumentOutOfRangeException(nameof(count)); }
            this.count = count;
            this.chance = chance;
        }
        public void Execute(AttackContext context)
        {
            if (!StatusProc.Roll(chance, context.RandomValue)) { return; }
            for (int i = 0; i < count; i++) { cast(context); }
        }
    }

    /// <summary>Uses attack-local elapsed time so the configuration carries no shared timer state.</summary>
    public sealed class PeriodicReaction : IAttackReaction
    {
        private readonly float interval;
        private readonly IAttackReaction reaction;
        public PeriodicReaction(float interval, IAttackReaction reaction)
        {
            if (interval <= 0 || float.IsNaN(interval) || float.IsInfinity(interval)) { throw new ArgumentOutOfRangeException(nameof(interval)); }
            this.interval = interval;
            this.reaction = reaction ?? throw new ArgumentNullException(nameof(reaction));
        }
        public void Execute(AttackContext context)
        {
            int previous = (int)Math.Floor(Math.Max(0, context.Elapsed - context.DeltaTime) / interval);
            int current = (int)Math.Floor(context.Elapsed / interval);
            for (int i = previous; i < current; i++) { reaction.Execute(context); }
        }
    }
}
