using System;
using System.Collections.Generic;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    // 적 한 마리의 규칙(HP, 속도, 공격, 상태이상, 패시브). 위치를 모른다: 위치와 이동은 Enemy(Transform)가 맡고,
    // 위치가 필요한 계산은 호출하는 쪽이 position을 넘긴다
    public class EnemyModel
    {
        private readonly float _speed; // 적 이동 속도
        private readonly EnemyType _type; // 적 타입
        private readonly EnemyAttackStats _attack; // 공격 값

        private readonly IPublisher<EnemyHpChanged> _hpChangedPublisher;
        private readonly IPublisher<EnemyDied> _diedPublisher;

        private float _attackTimer; // 공격 시간, 적 별로 공격 시작한 시간이 다르니까

        public EnemyType Type => _type;

        public AttackType AttackType => _attack.Type;

        public int Id { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        // 상태이상은 EnemyStatus가 맡고, 아래 멤버는 기존 접점(I*Target, 뷰, 테스트)을 유지하려고 위임한다
        private readonly EnemyStatus _status;

        public float FreezeRemaining => _status.FreezeRemaining;
        public bool IsFrozen => _status.IsFrozen;
        public float ParalysisRemaining => _status.ParalysisRemaining;
        public bool IsParalyzed => _status.IsParalyzed;
        public float StunRemaining => _status.StunRemaining;
        public bool IsStunned => _status.IsStunned;
        public float SlowRemaining => _status.SlowRemaining;
        public float VulnerabilityRemaining => _status.VulnerabilityRemaining;
        public float VulnerabilityRatio => _status.VulnerabilityRatio;
        public float BurnRemaining => _status.BurnRemaining;
        public int FrostbiteStacks => _status.FrostbiteStacks;
        public float MovementMultiplier => _status.MovementMultiplier;
        public bool IsImmuneTo(StatusImmunity status) => _status.IsImmuneTo(status);

        public void ApplyParalysis(float duration) => _status.ApplyParalysis(duration);
        public void SetAreaSlow(object source, float ratio) => _status.SetAreaSlow(source, ratio);
        public void RemoveAreaSlow(object source) => _status.RemoveAreaSlow(source);
        public void ApplyStun(float duration) => _status.ApplyStun(duration);
        public void ApplySlow(float ratio, float duration) => _status.ApplySlow(ratio, duration);
        public void ApplyVulnerability(float ratio, float duration) => _status.ApplyVulnerability(ratio, duration);
        public void ApplyBurn(float damagePerSecond, float duration, float maxHpRatio = 0, Action<Vector2> onDeath = null)
            => _status.ApplyBurn(damagePerSecond, duration, maxHpRatio, onDeath);
        public void ApplyFrostbite(float damagePerSecond) => _status.ApplyFrostbite(damagePerSecond);
        public void ApplyFreeze(float duration) => _status.ApplyFreeze(duration);
        public void TickStatus(float deltaTime) => _status.Tick(deltaTime);

        // 밀치기로 실제 움직일 거리. 적용(위치 이동)은 Enemy가 한다
        public Vector2 ResolveKnockback(Vector2 direction, float distance)
        {
            if (IsDead || distance <= 0 || float.IsNaN(distance) || float.IsInfinity(distance)
                || float.IsNaN(direction.x) || float.IsNaN(direction.y)
                || float.IsInfinity(direction.x) || float.IsInfinity(direction.y))
            {
                return Vector2.zero;
            }

            return direction.normalized * distance;
        }

        public event Action<EnemyProjectileModel> ProjectileFired; // 원거리 투사체 생성 용
        public event Action Attacked; // 공격이 나간 순간 (근접/원거리 공통, 공격 모션 재생 용)
        public event Action<int, Vector2> SpawnRequested; // 분열·소환 요청 (몬스터 Id, 이 적 기준 오프셋). 위치를 더하는 건 Enemy, 만드는 일은 EnemySpawner
        public event Action<Action<Vector2>> DeathExplosionRequested; // 점화 중 사망 폭발 (Enemy가 자기 위치로 호출한다)

        private readonly IReadOnlyList<IPassive> _passives;
        private readonly bool _isSummoned;

        public void RequestSpawn(int monsterId, Vector2 offset) => SpawnRequested?.Invoke(monsterId, offset);

        // 패시브의 시간 흐름. 빙결·마비·기절 중에는 멈춘다
        public void TickPassives(float deltaTime)
        {
            if (_passives == null || IsDead || _status.IsDisabled || deltaTime <= 0f)
            {
                return;
            }

            foreach (var passive in _passives)
            {
                passive.OnTick(this, deltaTime);
            }
        }

        // 이동 속도, 타입 지정해서 몬스터 생성
        // isSummoned: 분열·소환으로 생긴 적. 사망이 웨이브 게이지에 세어지지 않는다
        public EnemyModel(int id, float speed, EnemyType type, int maxHp,
            EnemyAttackStats attack,
            IPublisher<EnemyHpChanged> hpChangedPublisher,
            IPublisher<EnemyDied> diedPublisher,
            IReadOnlyList<IPassive> passives = null,
            bool isSummoned = false,
            StatusImmunity immunities = StatusImmunity.None)
        {
            if (maxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp), "적 체력은 1 이상이어야 합니다.");
            }

            _passives = passives;
            _isSummoned = isSummoned;
            _status = new EnemyStatus(maxHp, immunities, () => IsDead, TakeDamage);

            Id = id;
            _speed = speed;
            _type = type;

            MaxHp = maxHp;
            Hp = maxHp;
            _hpChangedPublisher = hpChangedPublisher;
            _diedPublisher = diedPublisher;
            _attack = attack;
        }

        // 지금 이동해야 하는 속도(감속 반영). 죽었거나 빙결·마비·기절 중이면 0. 실제 이동은 Enemy가 한다
        public float MoveSpeed => IsDead || _status.IsDisabled ? 0f : _speed * MovementMultiplier;

        public bool IsInAttackRange(Vector2 position, Wall wall)
        {
            return position.y - wall.AttackLineY <= _attack.Range;
        }

        // position: 이 적의 현재 위치 (근접은 벽을 때리고, 원거리는 여기서 투사체를 쏜다)
        public void Attack(float deltaTime, Vector2 position, Wall wall, EnemyProjectileSystem projectiles)
        {
            if (IsDead || _status.IsDisabled || wall.IsDestroyed)
            {
                return;
            }

            _attackTimer += deltaTime;
            if (_attackTimer < _attack.Interval)
            {
                return;
            }

            _attackTimer -= _attack.Interval;
            Attacked?.Invoke();

            if (_attack.Type == AttackType.Melee)
            {
                wall.TakeDamage(_attack.Damage);
            }
            else
            {
                var projectile = projectiles.Fire(position, _attack.Damage, _attack.ProjectileSpeed);
                ProjectileFired?.Invoke(projectile);
            }
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            int appliedDamage = _status.AmplifyDamage(amount);
            int hpBefore = Hp;
            Hp = Math.Max(0, Hp - appliedDamage);
            // 실제로 깎인 체력만 스킬 몫으로 알린다 (남은 체력보다 큰 과잉 피해는 뺀다)
            Game.Core.Combat.DamageAttribution.Report(hpBefore - Hp);
            _hpChangedPublisher.Publish(new EnemyHpChanged(Id, Hp, MaxHp));

            if (Hp == 0)
            {
                var deathExplosion = _status.ClearOnDeath();
                _diedPublisher.Publish(new EnemyDied(Id, _isSummoned));
                if (deathExplosion != null)
                {
                    DeathExplosionRequested?.Invoke(deathExplosion);
                }

                if (_passives != null)
                {
                    foreach (var passive in _passives)
                    {
                        passive.OnDied(this);
                    }
                }
            }
        }
    }
}
