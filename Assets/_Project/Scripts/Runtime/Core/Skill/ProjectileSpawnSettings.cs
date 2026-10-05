using UnityEngine;

namespace Game.Core
{
    /// <summary>One spawn snapshot. 적중 피해와 상태이상은 <see cref="HitReactions"/> 하나로 받는다
    /// (스탯에서는 <see cref="ReactionCompiler"/>, 보조 투사체는 <see cref="HitReactionBuilder"/>로 만든다).</summary>
    public sealed class ProjectileSpawnSettings
    {
        public Vector3 StartPos { get; set; }
        public Vector3 Direction { get; set; }
        public float Speed { get; set; }
        public float Lifetime { get; set; }
        public IEnemyTargetProvider TargetProvider { get; set; }
        public int PierceCount { get; set; } = 0;
        public System.Action<Vector2, Vector3, IEnemyTarget> OnHit { get; set; }
        public IEnemyTarget IgnoredTarget { get; set; }
        /// <summary>적중 때 가장 먼저 실행하는 피해·상태이상 반응</summary>
        public AttackReactions HitReactions { get; set; } = AttackReactions.Empty;
        /// <summary>강화 카드가 덧붙인 반응(시작·틱·적중·처치·소멸)</summary>
        public AttackReactions Reactions { get; set; } = AttackReactions.Empty;
        public System.Func<float> RandomValue { get; set; }
        public float CollisionRadius { get; set; } = .3f;
    }
}
