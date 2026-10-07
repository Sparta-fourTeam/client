using System;
using System.Collections.Generic;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    public class EnemyModel : IEnemyTarget, IFreezableTarget, IKnockbackTarget, IFrostbiteTarget, IParalyzableTarget, IBurnableTarget, IAreaSlowTarget, IStunnableTarget, ISlowableTarget, IVulnerableTarget
    {
        // 아래쪽 방향으로 이동.
        private static readonly Vector2 _moveDirection = Vector2.down;
        private readonly float _speed; // 적 이동 속도
        private readonly EnemyType _type; // 적 타입
        private readonly EnemyAttackStats _attack; // 공격 값

        private readonly IPublisher<EnemyHpChanged> _hpChangedPublisher;
        private readonly IPublisher<EnemyDied> _diedPublisher;

        private float _attackTimer; // 공격 시간, 적 별로 공격 시작한 시간이 다르니까

        public Vector2 Position { get; private set; }
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

        public void ApplyKnockback(Vector2 direction, float distance)
        {
            if (IsDead || distance <= 0 || float.IsNaN(distance) || float.IsInfinity(distance)
                || float.IsNaN(direction.x) || float.IsNaN(direction.y)
                || float.IsInfinity(direction.x) || float.IsInfinity(direction.y))
            {
                return;
            }

            Position += direction.normalized * distance;
        }

        public event Action<EnemyProjectileModel> ProjectileFired; // 원거리 투사체 생성 용
        public event Action Attacked; // 공격이 나간 순간 (근접/원거리 공통, 공격 모션 재생 용)
        public event Action<EnemySpawnRequest> SpawnRequested; // 분열·소환 등 새 적 생성 요청 (만드는 일은 EnemySpawner)

        private readonly IReadOnlyList<IPassive> _passives;
        private readonly bool _isSummoned;

        public void RequestSpawn(EnemySpawnRequest request) => SpawnRequested?.Invoke(request);

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

        // 스폰 위치, 이동 속도, 타입 지정해서 몬스터 생성
        // isSummoned: 분열·소환으로 생긴 적. 사망이 웨이브 게이지에 세어지지 않는다
        public EnemyModel(int id, Vector2 spawnPosition, float speed, EnemyType type, int maxHp,
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
            Position = spawnPosition;
            _speed = speed;
            _type = type;

            MaxHp = maxHp;
            Hp = maxHp;
            _hpChangedPublisher = hpChangedPublisher;
            _diedPublisher = diedPublisher;
            _attack = attack;
        }

        // 매 프레임 speed만큼 이동
        public void Move(float deltaTime)
        {
            if (IsDead || _status.IsDisabled)
            {
                return;
            }

            Position += _moveDirection * (_speed * MovementMultiplier * deltaTime);
        }

        public bool IsInAttackRange(Wall wall)
        {
            return Position.y - wall.AttackLineY <= _attack.Range;
        }


        public void Attack(float deltaTime, Wall wall, EnemyProjectileSystem projectiles)
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
                var projectile = projectiles.Fire(Position, _attack.Damage, _attack.ProjectileSpeed);
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
            Hp = Math.Max(0, Hp - appliedDamage);
            _hpChangedPublisher.Publish(new EnemyHpChanged(Id, Hp, MaxHp));

            if (Hp == 0)
            {
                var deathExplosion = _status.ClearOnDeath();
                _diedPublisher.Publish(new EnemyDied(Id, _isSummoned));
                deathExplosion?.Invoke(Position);

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
