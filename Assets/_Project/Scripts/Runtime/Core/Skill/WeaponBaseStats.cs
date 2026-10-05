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
        }

        public class FieldBase
        {
            public float damageRatio;
            public float radius;
            public float slowRatio;
        }
    }
}
