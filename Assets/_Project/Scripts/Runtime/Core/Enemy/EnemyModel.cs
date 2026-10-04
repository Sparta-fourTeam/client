using System;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    public class EnemyModel : IEnemyTarget, IFreezableTarget
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
        public float FreezeRemaining { get; private set; }
        public bool IsFrozen => FreezeRemaining > 0;

        public void ApplyFreeze(float duration)
        {
            if (IsDead || duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration)) { return; }
            FreezeRemaining = Math.Max(FreezeRemaining, duration);
        }

        public void TickStatus(float deltaTime)
        {
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { return; }
            FreezeRemaining = Math.Max(0, FreezeRemaining - deltaTime);
        }

        public event Action<EnemyProjectileModel> ProjectileFired; // 원거리 투사체 생성 용
        public event Action Attacked; // 공격이 나간 순간 (근접/원거리 공통, 공격 모션 재생 용)

        // 스폰 위치, 이동 속도, 타입 지정해서 몬스터 생성
        public EnemyModel(int id, Vector2 spawnPosition, float speed, EnemyType type, int maxHp,
            EnemyAttackStats attack,
            IPublisher<EnemyHpChanged> hpChangedPublisher,
            IPublisher<EnemyDied> diedPublisher)
        {
            if (maxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp), "적 체력은 1 이상이어야 합니다.");
            }

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
            if (IsDead || IsFrozen)
            {
                return;
            }

            Position += _moveDirection * (_speed * deltaTime);
        }

        public bool IsInAttackRange(Wall wall)
        {
            return Position.y - wall.AttackLineY <= _attack.Range;
        }


        public void Attack(float deltaTime, Wall wall, EnemyProjectileSystem projectiles)
        {
            if (IsDead || IsFrozen || wall.IsDestroyed)
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
                var projectile = projectiles.Fire(Position, _attack.Damage);
                ProjectileFired?.Invoke(projectile);
            }
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            Hp = Math.Max(0, Hp - amount);
            _hpChangedPublisher.Publish(new EnemyHpChanged(Id, Hp, MaxHp));

            if (Hp == 0)
            {
                _diedPublisher.Publish(new EnemyDied(Id));
            }
        }
    }
}
