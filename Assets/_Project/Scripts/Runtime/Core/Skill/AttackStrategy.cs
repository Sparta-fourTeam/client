using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>공격 한 번을 시전하는 방법. 풀링과 자원 정리는 전략이 갖고, 시전 시점(쿨타임·시전 수)은 <see cref="SkillCaster"/>가 관리한다.
    /// 새 공격 종류는 전략 하나를 만들어 <see cref="SkillFactory"/>에 등록한다.</summary>
    public interface IAttackStrategy : IDisposable
    {
        /// <summary>현재 설정으로 공격 한 번을 낸다. 실제로 시전했으면 true, 쏠 대상이 없어 아무것도 내지 않았으면 false.
        /// 시전기는 false이면 쿨타임을 쓰지 않고 대상이 생길 때까지 기다린다</summary>
        bool Fire(SkillConfig config, AttackEnvironment environment);

        /// <summary>매 프레임 추가로 처리할 일(예: 예비 시전). 없으면 비워 둔다</summary>
        void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime);
    }

    /// <summary>시전에 필요한 주변 정보. Origin이 시전 위치이며, 자식 스킬은 이벤트 위치를 Origin으로 시전한다.</summary>
    public sealed class AttackEnvironment
    {
        private readonly Transform caster;
        private readonly Func<Vector2, float, IReadOnlyList<IEnemyTarget>> select;
        private readonly Vector3? origin;
        private List<IEnemyTarget> filtered;

        public IEnemyTargetProvider Targets { get; }

        /// <summary>조준 대상에서 빼고 공격이 지나치게 할 적 (자식 스킬이 방금 맞은 적을 피할 때). 없으면 null</summary>
        public IEnemyTarget Exclude { get; }

        /// <summary>자식 스킬을 낳은 이벤트의 진행 방향(부모가 날아온 방향). 없으면 영벡터.
        /// 조준할 다른 적이 없을 때 이 방향 기준으로 부채꼴로 퍼지는 데 쓴다.</summary>
        public Vector3 Direction { get; }

        public AttackEnvironment(Transform caster, IEnemyTargetProvider targets,
            Func<Vector2, float, IReadOnlyList<IEnemyTarget>> select, Vector3? origin = null, IEnemyTarget exclude = null,
            Vector3 direction = default)
        {
            Direction = direction;
            this.caster = caster;
            Targets = targets;
            this.select = select;
            this.origin = origin;
            Exclude = exclude;
        }

        /// <summary>시전 위치. 지정하지 않으면 시전자의 현재 위치</summary>
        public Vector3 Origin => origin ?? caster.position;

        /// <summary>시전 위치 기준으로 사거리 안의 대상을 고른다 (반환 목록은 다음 호출 전까지만 유효)</summary>
        public IReadOnlyList<IEnemyTarget> FindTargets(float range)
        {
            var found = select(Origin, range);
            if (Exclude == null) { return found; }
            filtered ??= new List<IEnemyTarget>();
            filtered.Clear();
            foreach (var target in found)
            {
                if (!ReferenceEquals(target, Exclude)) { filtered.Add(target); }
            }
            return filtered;
        }

        /// <summary>같은 시전자와 대상 선택을 쓰되 다른 위치에서 시전하는 환경 (exclude를 주면 그 적은 조준하지 않는다)</summary>
        public AttackEnvironment At(Vector3 position, IEnemyTarget exclude = null, Vector3 direction = default) =>
            new AttackEnvironment(caster, Targets, select, position, exclude, direction);
    }
}
