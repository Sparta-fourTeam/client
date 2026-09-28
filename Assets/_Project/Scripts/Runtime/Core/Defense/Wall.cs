using System;
using Game.Core.Messages;
using MessagePipe;

namespace Game.Core.Defense
{
    public sealed class Wall : IWall
    {
        private readonly IBufferedPublisher<WallHpChanged> _hpChangedPublisher;
        private readonly IPublisher<WallDestroyed> _destroyedPublisher;

        public int CurrentHp { get; private set; }
        public int MaxHp { get; }
        public bool IsDestroyed { get; private set; }

        public Wall(
            int maxHp,
            IBufferedPublisher<WallHpChanged> hpChangedPublisher,
            IPublisher<WallDestroyed> destroyedPublisher)
        {
            if (maxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp));
            }

            MaxHp = maxHp;
            CurrentHp = maxHp;
            _hpChangedPublisher = hpChangedPublisher;
            _destroyedPublisher = destroyedPublisher;

            // 늦게 구독한 HUD도 초기 HP를 받도록 한 번 발행
            _hpChangedPublisher.Publish(new WallHpChanged(CurrentHp, MaxHp));
        }

        public void TakeDamage(int amount)
        {
            if (IsDestroyed || amount <= 0)
            {
                return;
            }

            CurrentHp = Math.Max(0, CurrentHp - amount);
            _hpChangedPublisher.Publish(new WallHpChanged(CurrentHp, MaxHp));

            if (CurrentHp == 0)
            {
                IsDestroyed = true;
                _destroyedPublisher.Publish(new WallDestroyed());
            }
        }
    }
}
