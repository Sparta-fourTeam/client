using UnityEngine;

namespace Game.Core
{
    /// <summary>쿨타임과 시전 수를 관리하고 실제 공격은 <see cref="IAttackStrategy"/>에 맡기는 공통 시전기.</summary>
    public sealed class SkillCaster : SkillBase
    {
        private readonly AttackEnvironment environment;

        public IAttackStrategy Strategy { get; }
        /// <summary>주 스킬이 대상을 향해 시전할 때 알린다. 자식 공격은 캐릭터 자세를 다시 재생하지 않는다.</summary>
        public event System.Action Fired;

        public SkillCaster(SkillData data, Transform caster, IEnemyTargetProvider targetProvider,
            IAttackStrategy strategy, SkillConfig config = null) : base(data, caster, targetProvider, config)
        {
            Strategy = strategy;
            environment = new AttackEnvironment(caster, targetProvider, FindTargets);
        }

        public override void Dispose()
        {
            base.Dispose();
            Fired = null;
            Strategy.Dispose();
        }

        protected override void OnTickExtra(float deltaTime)
        {
            using var scope = Game.Core.Combat.DamageAttribution.BeginOutermost(Data.ToDamageSource());
            Strategy.Tick(Config, environment, deltaTime);
        }

        protected override bool OnFire()
        {
            using var scope = Game.Core.Combat.DamageAttribution.BeginOutermost(Data.ToDamageSource());
            bool hasTarget = Fired != null && Strategy.HasTargets(Config, environment);
            bool fired = Strategy.Fire(Config, environment);
            if (hasTarget) { Fired?.Invoke(); }
            return fired;
        }

        /// <summary>쿨타임과 무관하게 지정한 위치에서 공격 한 번을 낸다. 자식 스킬 시전에 쓴다.</summary>
        /// <summary>쿨타임과 무관하게 지정한 위치에서, 현재 설정을 resolve로 바꿔 공격 한 번을 낸다.
        /// 자식 스킬이 부모에게서 가져온 값(상속, 자식 전용 강화)을 반영할 때 쓴다.</summary>
        /// <param name="exclude">조준하지 않고 지나칠 적 (방금 맞은 적)</param>
        /// <param name="direction">부모가 날아온 방향. 조준할 다른 적이 없을 때 이 방향 기준 부채꼴로 쏜다</param>
        public void FireAt(Vector3 position, System.Func<SkillConfig, SkillConfig> resolve, IEnemyTarget exclude = null, Vector3 direction = default)
        {
            using var scope = Game.Core.Combat.DamageAttribution.BeginOutermost(Data.ToDamageSource());
            Strategy.Fire(resolve(Config), environment.At(position, exclude, direction));
        }

        /// <param name="damageScale">이번 시전의 피해 배율. 1이면 현재 설정 그대로다</param>
        public void FireAt(Vector3 position, float damageScale = 1)
        {
            using var scope = Game.Core.Combat.DamageAttribution.BeginOutermost(Data.ToDamageSource());
            Strategy.Fire(Mathf.Approximately(damageScale, 1) ? Config : Config.WithDamageScale(damageScale), environment.At(position));
        }
    }
}
