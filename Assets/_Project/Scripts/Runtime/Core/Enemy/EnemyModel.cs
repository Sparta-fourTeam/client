using System;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;

namespace Game.Core
{
    public class EnemyModel : IEnemyTarget
    {
        // 아래쪽 방향으로 이동.
        private static readonly Vector2 _moveDirection = Vector2.down;
        private readonly float _speed; // 적 이동 속도
        private readonly EnemyType _type; // 적 타입

        private readonly IPublisher<EnemyHpChanged> _hpChangedPublisher;
        private readonly IPublisher<EnemyDied> _diedPublisher;

        public Vector2 Position { get; private set; }
        public EnemyType Type => _type;

        public int Id { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public bool IsDead => Hp <= 0;

        // 스폰 위치, 이동 속도, 타입 지정해서 몬스터 생성
        public EnemyModel(int id, Vector2 spawnPosition, float speed, EnemyType type, int maxHp,
            IPublisher<EnemyHpChanged> hpChangedPublisher,
            IPublisher<EnemyDied> diedPublisher)
        {
            if (maxHp <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp), "적 체력은 1 이상이어야 합니다.");
            Id = id;
            Position = spawnPosition;
            _speed = speed;
            _type = type;

            MaxHp = maxHp;
            Hp = maxHp;
            _hpChangedPublisher = hpChangedPublisher;
            _diedPublisher = diedPublisher;
        }

        // 매 프레임 speed만큼 이동
        public void Move(float deltaTime)
        {
            if (IsDead) return;

            Position += _moveDirection * (_speed * deltaTime);
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0) return;

            Hp = Math.Max(0, Hp - amount);
            _hpChangedPublisher.Publish(new EnemyHpChanged(Id, Hp, MaxHp));

            if(Hp == 0)
            {
                _diedPublisher.Publish(new EnemyDied(Id));
            }
        }
    }
}
