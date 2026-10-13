using System;
using Game.Core.Combat;

namespace Game.Core
{
    /// <summary>
    /// 피격 시 회복·가속: 스킬 공격에 맞아 체력이 깎일 때마다(적중 피해, IsImpact) chance 확률로
    /// 최대 체력의 healRatio만큼 회복하고 이속이 speedMultiplier배가 된다(speedDuration초, 다시 맞으면 갱신).
    /// element를 정하면 그 속성의 공격에 맞을 때만 반응한다(아카시타의 "피격 시 체력 회복&이동속도 증가").
    /// </summary>
    public sealed class HealOnHitPassive : IPassive
    {
        private readonly float _healRatio;
        private readonly float _speedMultiplier;
        private readonly float _speedDuration;
        private readonly Element? _element;
        private readonly float _chance;
        private readonly IRandomProvider _random;

        public HealOnHitPassive(float healRatio, float speedMultiplier = 1f, float speedDuration = 0f, Element? element = null,
            float chance = 1f, IRandomProvider random = null)
        {
            if (healRatio < 0f || float.IsNaN(healRatio) || float.IsInfinity(healRatio))
            {
                throw new ArgumentOutOfRangeException(nameof(healRatio), "회복 비율은 0 이상이어야 합니다.");
            }

            if (!(speedMultiplier > 0f) || float.IsInfinity(speedMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "이속 배율은 0보다 커야 합니다.");
            }

            if (speedMultiplier != 1f && !(speedDuration > 0f && !float.IsInfinity(speedDuration)))
            {
                throw new ArgumentOutOfRangeException(nameof(speedDuration), "이속이 바뀌면 지속시간은 0보다 커야 합니다.");
            }

            if (!(chance > 0f && chance <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(chance), "확률은 0보다 크고 1 이하여야 합니다.");
            }

            if (chance < 1f && random == null)
            {
                throw new ArgumentNullException(nameof(random), "확률로 반응하려면 랜덤이 필요합니다.");
            }

            if (healRatio == 0f && speedMultiplier == 1f)
            {
                throw new ArgumentException("반응할 효과가 하나도 없습니다.");
            }

            _healRatio = healRatio;
            _speedMultiplier = speedMultiplier;
            _speedDuration = speedDuration;
            _element = element;
            _chance = chance;
            _random = random;
        }

        public void OnDamaged(EnemyModel self, DamageInfo info, int appliedDamage)
        {
            if (!info.IsFromSkill || !info.IsImpact || (_element.HasValue && info.Element != _element.Value)
                || !StatusProc.Roll(_chance, () => _random.Range(0f, 1f)))
            {
                return;
            }

            if (_healRatio > 0f)
            {
                self.Heal(Math.Max(1, (int)Math.Round(self.MaxHp * _healRatio)));
            }

            if (_speedMultiplier != 1f)
            {
                self.AddSpeedBoost(this, _speedMultiplier, _speedDuration);
            }
        }
    }
}
