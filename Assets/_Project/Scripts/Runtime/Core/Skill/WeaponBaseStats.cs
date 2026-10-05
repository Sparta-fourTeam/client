namespace Game.Core
{
    /// <summary>스킬의 기본 수치. 소비하는 쪽(시전·투사체·상태이상 등)별로 묶어 JSON에서 필요한 묶음만 적는다.
    /// 적지 않은 묶음과 값은 각 클래스의 기본값을 쓴다.</summary>
    public class WeaponBaseStats
    {
        public CastBase cast = new CastBase();
        public ProjectileBase projectile = new ProjectileBase();
        public ReserveBase reserve = new ReserveBase();
        public StatusBase status = new StatusBase();
        public ExplosionBase explosion = new ExplosionBase();
        public FieldBase field = new FieldBase();
        public AreaBase area = new AreaBase();

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

        public class FieldBase
        {
            public float damageRatio;
            public float radius;
            public float slowRatio;
        }
    }
}
