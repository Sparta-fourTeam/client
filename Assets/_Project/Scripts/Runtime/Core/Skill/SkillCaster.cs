using UnityEngine;

namespace Game.Core
{
    /// <summary>쿨타임과 시전 수를 관리하고 실제 공격은 <see cref="IAttackStrategy"/>에 맡기는 공통 시전기.</summary>
    public sealed class SkillCaster : WeaponBase
    {
        private readonly AttackEnvironment environment;

        public IAttackStrategy Strategy { get; }

        public SkillCaster(WeaponData data, Transform caster, IEnemyTargetProvider targetProvider,
            IAttackStrategy strategy, SkillConfig config = null) : base(data, caster, targetProvider, config)
        {
            Strategy = strategy;
            environment = new AttackEnvironment(caster, targetProvider, FindTargets);
        }

        public override void Dispose()
        {
            base.Dispose();
            Strategy.Dispose();
        }

        protected override void OnTickExtra(float deltaTime) => Strategy.Tick(Config, environment, deltaTime);

        protected override void OnFire() => Strategy.Fire(Config, environment);

        /// <summary>쿨타임과 무관하게 지정한 위치에서 공격 한 번을 낸다. 자식 스킬 시전에 쓴다.</summary>
        public void FireAt(Vector3 position) => Strategy.Fire(Config, environment.At(position));
    }
}
