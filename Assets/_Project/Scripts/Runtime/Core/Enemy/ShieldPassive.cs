using System;
using Game.Core.Combat;

namespace Game.Core
{
    /// <summary>방어막이 켜지는 때</summary>
    public enum ShieldTrigger
    {
        /// <summary>몬스터가 만들어질 때부터</summary>
        Start,

        /// <summary>피격으로 체력이 깎인 뒤 chance 확률로</summary>
        OnHit,
    }

    /// <summary>
    /// 방어막: 스킬의 공격을 hits번 막는다. 켜져 있는 동안 스킬에서 나온 피해는 모두 막고(폭발 같은 부가 피해 포함),
    /// 공격이 적중할 때(IsImpact)만 막은 횟수에 센다. 상태이상 지속 피해는 막지 않는다.
    /// duration이 0보다 크면 그 시간이 지나도 사라지고, immunities는 켜져 있는 동안 걸리지 않는 상태이상이다.
    /// 한 번만 켜진다(누리카베의 방어막은 처음부터, 카라카사의 임시 방어막은 피격 때 확률로).
    /// </summary>
    public sealed class ShieldPassive : IPassive
    {
        private readonly ShieldTrigger _trigger;
        private readonly int _hits;
        private readonly float _duration;
        private readonly StatusImmunity _immunities;
        private readonly float _chance;
        private readonly IRandomProvider _random;

        private bool _used;
        private bool _active;
        private int _hitsLeft;
        private float _remaining;

        public bool IsActive => _active;

        public ShieldPassive(ShieldTrigger trigger, int hits, float duration = 0f, StatusImmunity immunities = StatusImmunity.None,
            float chance = 1f, IRandomProvider random = null)
        {
            if (hits < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hits), "방어막이 막는 횟수는 1 이상이어야 합니다.");
            }

            if (duration < 0f || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "방어막 지속시간은 0 이상이어야 합니다.");
            }

            if (!(chance > 0f && chance <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(chance), "확률은 0보다 크고 1 이하여야 합니다.");
            }

            if (trigger == ShieldTrigger.OnHit && chance < 1f && random == null)
            {
                throw new ArgumentNullException(nameof(random), "확률로 켜지는 방어막은 랜덤이 필요합니다.");
            }

            _trigger = trigger;
            _hits = hits;
            _duration = duration;
            _immunities = immunities;
            _chance = chance;
            _random = random;
        }

        public void OnSpawn(EnemyModel self)
        {
            if (_trigger == ShieldTrigger.Start)
            {
                Activate(self);
            }
        }

        public int OnBeforeDamage(EnemyModel self, DamageInfo info, int amount)
        {
            if (!_active || !info.IsFromSkill)
            {
                return amount;
            }

            if (info.IsImpact)
            {
                _hitsLeft--;
                if (_hitsLeft <= 0)
                {
                    Deactivate(self);
                }
            }

            return 0;
        }

        public void OnDamaged(EnemyModel self, DamageInfo info, int appliedDamage)
        {
            if (_trigger == ShieldTrigger.OnHit && !_used && info.IsFromSkill && info.IsImpact
                && StatusProc.Roll(_chance, () => _random.Range(0f, 1f)))
            {
                Activate(self);
            }
        }

        public void OnTick(EnemyModel self, float deltaTime)
        {
            if (_active && _duration > 0f)
            {
                _remaining -= deltaTime;
                if (_remaining <= 0f)
                {
                    Deactivate(self);
                }
            }
        }

        private void Activate(EnemyModel self)
        {
            _used = true;
            _active = true;
            _hitsLeft = _hits;
            _remaining = _duration;
            if (_immunities != StatusImmunity.None)
            {
                self.GrantImmunity(this, _immunities);
            }
        }

        private void Deactivate(EnemyModel self)
        {
            _active = false;
            self.RevokeImmunity(this);
        }
    }
}
