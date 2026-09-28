using System;
using Game.Core.Combat;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Core.Defense
{
    public sealed class Wall : MonoBehaviour, IDamageable
    {
        private IBufferedPublisher<WallHpChanged> _hpChangedPublisher;
        private IPublisher<WallDestroyed> _destroyedPublisher;

        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public bool IsDestroyed { get; private set; }

        [Inject]
        public void Construct(IBufferedPublisher<WallHpChanged> hpChangedPublisher,
            IPublisher<WallDestroyed> destroyedPublisher)
        {
            _hpChangedPublisher = hpChangedPublisher;
            _destroyedPublisher = destroyedPublisher;
        }

        public void Initialize(int maxHp)
        {
            if (maxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            }

            MaxHp = maxHp;
            CurrentHp = maxHp;
            IsDestroyed = false;

            _hpChangedPublisher.Publish(new WallHpChanged(CurrentHp, MaxHp));
        }

        public void TakeDamage(int amount)
        {
            if (IsDestroyed || amount <= 0 || MaxHp <= 0)
            {
                return;
            }

            CurrentHp = Math.Max(0, CurrentHp - amount);
            _hpChangedPublisher.Publish(new WallHpChanged(CurrentHp, MaxHp));

            if (CurrentHp <= 0)
            {
                IsDestroyed = true;
                _destroyedPublisher.Publish(new WallDestroyed());
            }
        }
    }
}
