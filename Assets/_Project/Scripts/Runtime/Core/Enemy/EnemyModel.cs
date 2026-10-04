using System;
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
        public float FreezeRemaining { get; private set; }
        public bool IsFrozen => FreezeRemaining > 0;
        public float ParalysisRemaining { get; private set; }
        public bool IsParalyzed => ParalysisRemaining > 0;
        public void ApplyParalysis(float duration)
        {
            if (IsDead || duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return;
            }

            ParalysisRemaining = Math.Max(ParalysisRemaining, duration);
        }

        private readonly System.Collections.Generic.Dictionary<object, float> areaSlows = new();
        public float MovementMultiplier
        {
            get
            {
                float strongest = SlowRemaining > 0 ? slowRatio : 0;
                foreach (var ratio in areaSlows.Values) { strongest = Math.Max(strongest, ratio); }
                return 1 - strongest;
            }
        }
        public void SetAreaSlow(object source, float ratio)
        {
            if (source == null || IsDead || ratio <= 0 || ratio >= 1 || float.IsNaN(ratio) || float.IsInfinity(ratio)) { return; }
            areaSlows[source] = ratio;
        }
        public void RemoveAreaSlow(object source)
        {
            if (source != null) { areaSlows.Remove(source); }
        }

        public float StunRemaining { get; private set; }
        public bool IsStunned => StunRemaining > 0;
        public float SlowRemaining { get; private set; }
        private float slowRatio;
        public float VulnerabilityRemaining { get; private set; }
        public float VulnerabilityRatio { get; private set; }
        private static bool PositiveFinite(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        public void ApplyStun(float duration)
        {
            if (!IsDead && PositiveFinite(duration)) { StunRemaining = Math.Max(StunRemaining, duration); }
        }
        public void ApplySlow(float ratio, float duration)
        {
            if (IsDead || !PositiveFinite(ratio) || ratio >= 1 || !PositiveFinite(duration)) { return; }
            slowRatio = Math.Max(slowRatio, ratio); SlowRemaining = Math.Max(SlowRemaining, duration);
        }
        public void ApplyVulnerability(float ratio, float duration)
        {
            if (IsDead || !PositiveFinite(ratio) || !PositiveFinite(duration)) { return; }
            VulnerabilityRatio = Math.Max(VulnerabilityRatio, ratio);
            VulnerabilityRemaining = Math.Max(VulnerabilityRemaining, duration);
        }

        public float BurnRemaining { get; private set; }
        private float burnDamage;
        private float burnElapsed;
        private float burnMaxHpRatio;
        private Action<Vector2> burnOnDeath;
        public void ApplyBurn(float damagePerSecond, float duration, float maxHpRatio = 0, Action<Vector2> onDeath = null)
        {
            if (IsDead || damagePerSecond <= 0 || duration <= 0
                || float.IsNaN(damagePerSecond) || float.IsInfinity(damagePerSecond)
                || float.IsNaN(duration) || float.IsInfinity(duration)
                || maxHpRatio < 0 || float.IsNaN(maxHpRatio) || float.IsInfinity(maxHpRatio))
            {
                return;
            }

            if (BurnRemaining <= 0)
            {
                ClearBurn();
            }

            BurnRemaining = Math.Max(BurnRemaining, duration);
            burnDamage = Math.Max(burnDamage, damagePerSecond);
            burnMaxHpRatio = Math.Max(burnMaxHpRatio, maxHpRatio);
            if (onDeath != null)
            {
                burnOnDeath = onDeath;
            }
        }

        private void ClearBurn()
        {
            BurnRemaining = 0; burnElapsed = 0; burnDamage = 0;
            burnMaxHpRatio = 0; burnOnDeath = null;
        }

        private void TickBurn(float deltaTime)
        {
            if (IsDead) { ClearBurn(); return; }
            if (BurnRemaining <= 0)
            {
                return;
            }

            burnElapsed += Math.Min(deltaTime, BurnRemaining);
            float remaining = Math.Max(0, BurnRemaining - deltaTime);
            while (burnElapsed >= 1 && !IsDead)
            {
                burnElapsed -= 1;
                TakeDamage(Math.Max(1, (int)(burnDamage + Math.Round(MaxHp * (double)burnMaxHpRatio, 4))));
            }
            BurnRemaining = remaining;
            if (BurnRemaining <= 0 || IsDead)
            {
                ClearBurn();
            }
        }

        private sealed class FrostbiteStack
        {
            public float Damage;
            public float Remaining = 10;
            public float Elapsed;
        }
        private readonly System.Collections.Generic.List<FrostbiteStack> frostbite = new();
        public int FrostbiteStacks => frostbite.Count;

        public void ApplyFrostbite(float damagePerSecond)
        {
            if (IsDead || damagePerSecond <= 0 || float.IsNaN(damagePerSecond) || float.IsInfinity(damagePerSecond))
            {
                return;
            }

            if (frostbite.Count >= 5)
            {
                frostbite.RemoveAt(0);
            }

            frostbite.Add(new FrostbiteStack { Damage = damagePerSecond });
        }

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

        public void ApplyFreeze(float duration)
        {
            if (IsDead || duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration)) { return; }
            FreezeRemaining = Math.Max(FreezeRemaining, duration);
        }

        public void TickStatus(float deltaTime)
        {
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) { return; }
            if (VulnerabilityRemaining > 0 && deltaTime > VulnerabilityRemaining)
            {
                float active = VulnerabilityRemaining;
                TickStatusSlice(active); TickStatusSlice(deltaTime - active);
            }
            else { TickStatusSlice(deltaTime); }
        }

        private void TickStatusSlice(float deltaTime)
        {
            StunRemaining = Math.Max(0, StunRemaining - deltaTime);
            SlowRemaining = Math.Max(0, SlowRemaining - deltaTime);
            if (SlowRemaining <= 0) { slowRatio = 0; }
            FreezeRemaining = Math.Max(0, FreezeRemaining - deltaTime);
            ParalysisRemaining = Math.Max(0, ParalysisRemaining - deltaTime);
            TickBurn(deltaTime);
            if (IsDead) { frostbite.Clear(); return; }
            foreach (var stack in frostbite)
            {
                float activeTime = Math.Min(deltaTime, stack.Remaining);
                stack.Elapsed += activeTime;
                stack.Remaining -= activeTime;
                while (stack.Elapsed >= 1 && !IsDead)
                {
                    stack.Elapsed -= 1;
                    TakeDamage(Math.Max(1, (int)stack.Damage));
                }
            }
            frostbite.RemoveAll(stack => stack.Remaining <= 0);
            VulnerabilityRemaining = Math.Max(0, VulnerabilityRemaining - deltaTime);
            if (VulnerabilityRemaining <= 0) { VulnerabilityRatio = 0; }
            if (IsDead)
            {
                frostbite.Clear();
            }
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
            if (IsDead || IsFrozen || IsParalyzed || IsStunned)
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
            if (IsDead || IsFrozen || IsParalyzed || IsStunned || wall.IsDestroyed)
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

            int appliedDamage = amount;
            if (VulnerabilityRemaining > 0)
            {
                double amplified = Math.Floor(Math.Round(amount * (1d + VulnerabilityRatio), 4));
                appliedDamage = (int)Math.Min(int.MaxValue, amplified);
            }
            Hp = Math.Max(0, Hp - appliedDamage);
            _hpChangedPublisher.Publish(new EnemyHpChanged(Id, Hp, MaxHp));

            if (Hp == 0)
            {
                var deathExplosion = BurnRemaining > 0 ? burnOnDeath : null;
                ClearBurn();
                areaSlows.Clear();
                StunRemaining = SlowRemaining = VulnerabilityRemaining = 0;
                slowRatio = VulnerabilityRatio = 0;
                _diedPublisher.Publish(new EnemyDied(Id));
                deathExplosion?.Invoke(Position);
            }
        }
    }
}
