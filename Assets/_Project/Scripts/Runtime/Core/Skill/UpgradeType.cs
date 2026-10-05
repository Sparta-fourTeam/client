namespace Game.Core
{
    /// <summary>카드 효과 종류. 값이 Weapons.json의 type 숫자이므로 바꾸면 JSON도 함께 바꿔야 한다.
    /// 묶음마다 10단위 대역을 갖는다. 새 종류는 해당 대역의 다음 빈 번호를 쓰고 기존 값은 옮기지 않는다.</summary>
    public enum UpgradeType
    {
        // 시전 (0~)
        AttackSpeed = 0,
        Damage = 1,
        ImpactDamage = 2,
        ProjectileCount = 3,
        CastCount = 4,
        ReserveCasts = 5,
        Form = 6,

        // 투사체 (10~)
        PierceCount = 10,
        ProjectileSpeed = 11,
        ProjectileSize = 12,
        Knockback = 13,

        // 폭발 (20~)
        EnableExplosion = 20,
        ExplosionDamage = 21,
        ExplosionRadius = 22,

        // 상태이상 (30~)
        FreezeDuration = 30,
        Frostbite = 31,
        Paralysis = 32,
        ParalysisDuration = 33,
        StunDuration = 34,
        SlowDuration = 35,
        VulnerabilityRatio = 36,
        VulnerabilityDuration = 37,

        // 화상 (40~)
        BurnDuration = 40,
        BurnRatio = 41,
        BurnMaxHp = 42,
        BurnDeathExplosion = 43,

        // 분열 (50~)
        SplitCount = 50,
        SplitDamage = 51,
        SplitFrostbite = 52,
        SplitParalysis = 53,
        SplitLightning = 54,
        SplitExplosion = 55,

        // 번개 (60~)
        LightningStrike = 60,
        KillLightning = 61,

        // 장판 (70~)
        FieldDuration = 70,
        FieldDamageFlat = 71,
        FieldDamageMultiplier = 72,
    }
}
