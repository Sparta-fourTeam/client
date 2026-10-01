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

        // 남은 HP 비율이 이 값 이하면 해당 단계의 스프라이트를 쓴다
        private const float DamagedRatio = 0.67f;
        private const float BrokenRatio = 0.33f;

        private SpriteRenderer _renderer;

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
            ApplySprite();

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

            ApplySprite();

            if (CurrentHp <= 0)
            {
                IsDestroyed = true;
                _destroyedPublisher.Publish(new WallDestroyed());
            }
        }

        // 스프라이트가 없는 벽(테스트 등)도 동작하도록 렌더러와 배열을 확인한다
        private void ApplySprite()
        {
            if (_sprites == null || _sprites.Length == 0)
            {
                return;
            }

            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
                if (_renderer == null)
                {
                    return;
                }
            }

            float ratio = MaxHp > 0 ? (float)CurrentHp / MaxHp : 1f;
            int stage = ratio <= BrokenRatio ? 2 : ratio <= DamagedRatio ? 1 : 0;
            _renderer.sprite = _sprites[Mathf.Min(stage, _sprites.Length - 1)];
        }
    }
}
