using System;
using Game.Core.Combat;

namespace Game.Core
{
    /// <summary>
    /// 회피: 스킬 공격이 적중할 때(IsImpact) chance 확률로 피한다. 피하면 그 공격의 피해를 받지 않고,
    /// 같은 공격에서 따라오는 폭발 같은 부가 피해와 상태이상도 잠깐(0.1초) 막는다(카라카사의 "피해·디버프 면역", 카와사부로의 회피).
    /// 상태이상 지속 피해는 피하지 않는다.
    /// </summary>
    public sealed class EvadePassive : IPassive
    {
        // 피한 공격에 딸린 부가 피해와 상태이상이 같은 프레임에 이어서 오므로 이 시간 동안 같이 막는다
        public const float AfterEvadeWindow = 0.1f;

        private readonly float _chance;
        private readonly IRandomProvider _random;
        private float _window;

        public EvadePassive(float chance, IRandomProvider random = null)
        {
            if (!(chance > 0f && chance <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(chance), "확률은 0보다 크고 1 이하여야 합니다.");
            }

            if (chance < 1f && random == null)
            {
                throw new ArgumentNullException(nameof(random), "확률로 피하려면 랜덤이 필요합니다.");
            }

            _chance = chance;
            _random = random;
        }

        public int OnBeforeDamage(EnemyModel self, DamageInfo info, int amount)
        {
            if (!info.IsFromSkill)
            {
                return amount;
            }

            if (_window > 0f)
            {
                return 0; // 방금 피한 공격의 나머지 피해
            }

            if (!info.IsImpact || !StatusProc.Roll(_chance, () => _random.Range(0f, 1f)))
            {
                return amount;
            }

            _window = AfterEvadeWindow;
            self.GrantImmunity(this, StatusImmunity.AllDebuffs, AfterEvadeWindow);
            return 0;
        }

        public void OnTick(EnemyModel self, float deltaTime)
        {
            if (_window > 0f)
            {
                _window = Math.Max(0f, _window - deltaTime);
            }
        }
    }
}
