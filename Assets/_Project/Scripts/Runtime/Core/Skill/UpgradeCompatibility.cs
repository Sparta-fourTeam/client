using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 강화 효과를 실제로 소비하는 공격 종류 표. 소비하지 않는 공격에 붙은 카드는 조용히 무시되므로 카탈로그 검증이 막는다.
    /// 새 UpgradeType을 추가하면 여기에 등록해야 한다. 등록하지 않으면 어떤 공격에서도 지원하지 않는 것으로 본다.
    /// </summary>
    public static class UpgradeCompatibility
    {
        [Flags]
        private enum Users
        {
            Projectile = 1,
            Hitscan = 2,
            Both = Projectile | Hitscan
        }

        private static readonly Dictionary<UpgradeType, Users> Table = new()
        {
            // 시전: 두 공격 모두 쿨타임, 피해, 시전 수, 형태를 쓴다.
            [UpgradeType.AttackSpeed] = Users.Both,
            [UpgradeType.Damage] = Users.Both,
            [UpgradeType.ImpactDamage] = Users.Both,
            [UpgradeType.ProjectileCount] = Users.Both,
            [UpgradeType.HitCount] = Users.Both,
            [UpgradeType.CastCount] = Users.Both,
            [UpgradeType.Form] = Users.Both,
            [UpgradeType.ReserveCasts] = Users.Projectile,

            // 투사체의 이동과 충돌
            [UpgradeType.PierceCount] = Users.Projectile,
            [UpgradeType.ProjectileSpeed] = Users.Projectile,
            [UpgradeType.ProjectileSize] = Users.Projectile,
            [UpgradeType.Knockback] = Users.Projectile,

            // 폭발은 두 공격 모두 명중 위치에서 터뜨린다.
            [UpgradeType.EnableExplosion] = Users.Both,
            [UpgradeType.ExplosionDamage] = Users.Both,
            [UpgradeType.ExplosionRadius] = Users.Both,

            // 상태 이상: 마비만 Hitscan도 쓴다.
            [UpgradeType.Paralysis] = Users.Both,
            [UpgradeType.ParalysisDuration] = Users.Both,
            [UpgradeType.FreezeDuration] = Users.Projectile,
            [UpgradeType.Frostbite] = Users.Projectile,
            [UpgradeType.StunDuration] = Users.Projectile,
            [UpgradeType.SlowDuration] = Users.Projectile,
            [UpgradeType.VulnerabilityRatio] = Users.Projectile,
            [UpgradeType.VulnerabilityDuration] = Users.Projectile,

            // 점화
            [UpgradeType.BurnDuration] = Users.Projectile,
            [UpgradeType.BurnRatio] = Users.Projectile,
            [UpgradeType.BurnMaxHp] = Users.Projectile,
            [UpgradeType.BurnDeathExplosion] = Users.Projectile,

            // 보조 공격: 분열 수와 피해 배율은 두 공격이 함께 쓴다(투사체 분열, 전기 구체).
            [UpgradeType.SplitCount] = Users.Both,
            [UpgradeType.SplitDamage] = Users.Both,
            [UpgradeType.SplitFrostbite] = Users.Projectile,
            [UpgradeType.LightningStrike] = Users.Projectile,
            [UpgradeType.SplitLightning] = Users.Projectile,
            [UpgradeType.SplitExplosion] = Users.Projectile,
            [UpgradeType.SplitParalysis] = Users.Hitscan,
            [UpgradeType.KillLightning] = Users.Hitscan,

            // 전자기장은 Hitscan만 만든다.
            [UpgradeType.FieldDuration] = Users.Hitscan,
            [UpgradeType.FieldDamageFlat] = Users.Hitscan,
            [UpgradeType.FieldDamageMultiplier] = Users.Hitscan
        };

        public static bool IsRegistered(UpgradeType type) => Table.ContainsKey(type);

        public static bool Supports(CastType castType, UpgradeType type)
        {
            if (!Table.TryGetValue(type, out var users)) { return false; }
            var user = castType == CastType.Hitscan ? Users.Hitscan : Users.Projectile;
            return (users & user) != 0;
        }
    }
}
