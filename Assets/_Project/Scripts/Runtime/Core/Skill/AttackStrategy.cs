using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>공격 한 번을 시전하는 방법. 풀링과 자원 정리는 전략이 갖고, 시전 시점(쿨타임·시전 수)은 <see cref="SkillCaster"/>가 관리한다.
    /// 새 공격 종류는 전략 하나를 만들어 <see cref="WeaponFactory"/>에 등록한다.</summary>
    public interface IAttackStrategy : IDisposable
    {
        /// <summary>현재 설정으로 공격 한 번을 낸다</summary>
        void Fire(SkillConfig config, AttackEnvironment environment);

        /// <summary>매 프레임 추가로 처리할 일(예: 예비 시전). 없으면 비워 둔다</summary>
        void Tick(SkillConfig config, AttackEnvironment environment, float deltaTime);
    }

    /// <summary>시전에 필요한 주변 정보. Origin이 시전 위치이며, 자식 스킬은 이벤트 위치를 Origin으로 시전한다.</summary>
    public sealed class AttackEnvironment
    {
        private readonly Transform caster;
        private readonly Func<Vector2, float, IReadOnlyList<IEnemyTarget>> select;
        private readonly Vector3? origin;

        public IEnemyTargetProvider Targets { get; }

        public AttackEnvironment(Transform caster, IEnemyTargetProvider targets,
            Func<Vector2, float, IReadOnlyList<IEnemyTarget>> select, Vector3? origin = null)
        {
            this.caster = caster;
            Targets = targets;
            this.select = select;
            this.origin = origin;
        }

        /// <summary>시전 위치. 지정하지 않으면 시전자의 현재 위치</summary>
        public Vector3 Origin => origin ?? caster.position;

        /// <summary>시전 위치 기준으로 사거리 안의 대상을 고른다 (반환 목록은 다음 호출 전까지만 유효)</summary>
        public IReadOnlyList<IEnemyTarget> FindTargets(float range) => select(Origin, range);

        /// <summary>같은 시전자와 대상 선택을 쓰되 다른 위치에서 시전하는 환경</summary>
        public AttackEnvironment At(Vector3 position) => new AttackEnvironment(caster, Targets, select, position);
    }
}
