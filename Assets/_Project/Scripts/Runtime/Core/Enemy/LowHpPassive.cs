using System;
using Game.Core.Combat;

namespace Game.Core
{
    /// <summary>
    /// 체력 조건 발동: 피격으로 체력 비율이 hpRatio 이하가 되면 한 번만 duration초 동안 효과가 켜진다.
    /// 효과는 회복(healRatio: 그 시간 동안 최대 체력의 비율만큼 나눠서 회복), 무적(상태이상 지속 피해까지 모두 막음),
    /// 이속 배율, 걸리지 않는 상태이상이다(나무 정령의 "30% 이하에서 5초간 50% 회복", 카와타로의 "10% 미만에서 6초간 무적").
    /// 기절·빙결·마비 중에는 시간이 흐르지 않는다.
    /// </summary>
    public sealed class LowHpPassive : IPassive
    {
        private readonly float _hpRatio;
        private readonly float _duration;
        private readonly float _healRatio;
        private readonly bool _invulnerable;
        private readonly float _speedMultiplier;
        private readonly StatusImmunity _immunities;

        private bool _used;
        private bool _active;
        private float _remaining;
        private float _healCarry;

        public bool IsActive => _active;

        public LowHpPassive(float hpRatio, float duration, float healRatio = 0f, bool invulnerable = false,
            float speedMultiplier = 1f, StatusImmunity immunities = StatusImmunity.None)
        {
            if (!(hpRatio > 0f && hpRatio < 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(hpRatio), "체력 비율은 0보다 크고 1보다 작아야 합니다.");
            }

            if (!(duration > 0f) || float.IsInfinity(duration))
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "지속시간은 0보다 커야 합니다.");
            }

            if (healRatio < 0f || float.IsNaN(healRatio) || float.IsInfinity(healRatio))
            {
                throw new ArgumentOutOfRangeException(nameof(healRatio), "회복 비율은 0 이상이어야 합니다.");
            }

            if (!(speedMultiplier > 0f) || float.IsInfinity(speedMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "이속 배율은 0보다 커야 합니다.");
            }

            if (healRatio == 0f && !invulnerable && speedMultiplier == 1f && immunities == StatusImmunity.None)
            {
                throw new ArgumentException("켜질 효과가 하나도 없습니다.");
            }

            _hpRatio = hpRatio;
            _duration = duration;
            _healRatio = healRatio;
            _invulnerable = invulnerable;
            _speedMultiplier = speedMultiplier;
            _immunities = immunities;
        }

        public int OnBeforeDamage(EnemyModel self, DamageInfo info, int amount) => _active && _invulnerable ? 0 : amount;

        public void OnDamaged(EnemyModel self, DamageInfo info, int appliedDamage)
        {
            if (_used || self.HpRatio > _hpRatio)
            {
                return;
            }

            _used = true;
            _active = true;
            _remaining = _duration;
            if (_immunities != StatusImmunity.None)
            {
                self.GrantImmunity(this, _immunities);
            }

            if (_speedMultiplier != 1f)
            {
                self.AddSpeedBoost(this, _speedMultiplier);
            }
        }

        public void OnTick(EnemyModel self, float deltaTime)
        {
            if (!_active)
            {
                return;
            }

            float step = Math.Min(deltaTime, _remaining);
            if (_healRatio > 0f)
            {
                _healCarry += _healRatio * self.MaxHp * step / _duration;
                int whole = (int)_healCarry;
                if (whole >= 1)
                {
                    self.Heal(whole);
                    _healCarry -= whole;
                }
            }

            _remaining -= step;
            if (_remaining <= 0f)
            {
                _active = false;
                self.RevokeImmunity(this);
                self.RemoveSpeedBoost(this);
            }
        }
    }
}
