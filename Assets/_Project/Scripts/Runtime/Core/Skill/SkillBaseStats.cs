namespace Game.Core
{
    /// <summary>스킬의 기본 수치. 소비하는 쪽(시전·투사체·상태이상 등)별로 묶어 JSON에서 필요한 묶음만 적는다.
    /// 적지 않은 묶음과 값은 각 클래스의 기본값을 쓴다.</summary>
    public class SkillBaseStats
    {
        public CastBase cast = new CastBase();
        public ProjectileBase projectile = new ProjectileBase();
        public ReserveBase reserve = new ReserveBase();
        public StatusBase status = new StatusBase();
        public ExplosionBase explosion = new ExplosionBase();
        public FieldBase field = new FieldBase();
        public AreaBase area = new AreaBase();
        public BeamBase beam = new BeamBase();
        public ChainBase chain = new ChainBase();

        public class CastBase
        {
            public float cooldown;
            public float baseDamage;
            public float range;
            public int projectileCount;
            public int castCount = 1;
            public float castInterval = 0.1f;
        }

        public class ProjectileBase
        {
            public float speed;
            public int pierceCount;
            public float knockbackDistance;
        }

        public class ReserveBase
        {
            public float distance;
            public float cooldown;
            public float interval;
        }

        public class StatusBase
        {
            public float freezeDuration;
            public float freezeChance = 1;
            public float frostbiteChance = 1;
            public float paralysisDuration;
            public float paralysisChance = 1;
            public float stunDuration;
            public float stunChance = 1;
            public float slowDuration;
            public float slowRatio;
            public float burnChance = 1;
        }

        public class ExplosionBase
        {
            public float radius;
            public float damageRatio;
        }

        public class AreaBase
        {
            public float radius;
            public float duration;
            public float pulseInterval = 1;
            /// <summary>초당 이동 거리. 가장 가까운 적을 향해 천천히 움직인다 (0이면 제자리)</summary>
            public float moveSpeed;
            /// <summary>펄스마다 범위 안의 적을 중심으로 끌어당기는 거리 (0이면 끌어당기지 않는다)</summary>
            public float pull;
        }

        public class ChainBase
        {
            /// <summary>첫 대상 뒤에 튕기는 횟수(반사)</summary>
            public float bounces;
            /// <summary>다음 대상을 찾는 거리 (현재 대상 기준)</summary>
            public float jumpRange = 4;
            /// <summary>대상 사이를 건너는 시간(초)</summary>
            public float hopInterval = 0.08f;
            /// <summary>0보다 크면 튕기는 경로 위(이 폭 이내)의 적도 함께 공격한다</summary>
            public float pathWidth;
        }

        public class BeamBase
        {
            /// <summary>광선 길이</summary>
            public float length;
            public float width;
            public float duration;
            /// <summary>지속 시간 동안의 공격 횟수(펄스 수). 간격은 지속 시간 / 공격 횟수다</summary>
            public float pulses = 1;
            /// <summary>겨눈 적(메인 대상)에게 공격마다 더 주는 피해 비율. 0.5면 메인 대상은 공격력의 150%를 받는다</summary>
            public float focusBonus;
        }

        public class FieldBase
        {
            public float damageRatio;
            public float radius;
            public float slowRatio;
        }
    }
}
