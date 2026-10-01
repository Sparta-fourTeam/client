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
        // TODO : 현재는 하드코딩, 추후 StageDefinition 연동시 수정할 수 있음.
        public const int DefaultMaxHp = 100;

        private IBufferedPublisher<WallHpChanged> _hpChangedPublisher;
        private IPublisher<WallDestroyed> _destroyedPublisher;


        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public bool IsDestroyed { get; private set; }

        [SerializeField] private Sprite[] _sprites;
        [SerializeField]
        private float _attackLineOffset;

        public float AttackLineY => transform.position.y + _attackLineOffset;

        private SpriteRenderer sr;

        [Inject]
        public void Construct(IBufferedPublisher<WallHpChanged> hpChangedPublisher,
            IPublisher<WallDestroyed> destroyedPublisher)
        {
            _hpChangedPublisher = hpChangedPublisher;
            _destroyedPublisher = destroyedPublisher;
        }

        // Unity Start는 씬의 모든 Awake(스코프 빌드, 구독) 뒤에 불리므로 첫 HP 발행을 놓치는 구독자가 없다
        private void Start()
        {
            Initialize(DefaultMaxHp);
            sr = GetComponent<SpriteRenderer>();
            sr.sprite = _sprites[0];
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

            if ((float)CurrentHp / MaxHp <= 0.67f && (float)CurrentHp / MaxHp > 0.33f)
            {
                sr.sprite = _sprites[1];
            }
            else if ((float)CurrentHp / MaxHp <= 0.33f)
            {
                sr.sprite = _sprites[2];
            }

            if (CurrentHp <= 0)
            {
                IsDestroyed = true;
                _destroyedPublisher.Publish(new WallDestroyed());
            }
        }
    }
}
