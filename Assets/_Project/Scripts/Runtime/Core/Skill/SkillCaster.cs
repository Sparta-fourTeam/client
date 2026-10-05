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
        /// <summary>쿨타임과 무관하게 지정한 위치에서, 현재 설정을 resolve로 바꿔 공격 한 번을 낸다.
        /// 자식 스킬이 부모에게서 가져온 값(상속, 자식 전용 강화)을 반영할 때 쓴다.</summary>
        /// <param name="exclude">조준하지 않고 지나칠 적 (방금 맞은 적)</param>
        public void FireAt(Vector3 position, System.Func<SkillConfig, SkillConfig> resolve, IEnemyTarget exclude = null) =>
            Strategy.Fire(resolve(Config), environment.At(position, exclude));

        /// <param name="damageScale">이번 시전의 피해 배율. 1이면 현재 설정 그대로다</param>
        public void FireAt(Vector3 position, float damageScale = 1) =>
            Strategy.Fire(Mathf.Approximately(damageScale, 1) ? Config : Config.WithDamageScale(damageScale), environment.At(position));
    }
}
