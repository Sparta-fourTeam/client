using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum UpgradeEffectCategory { Stat, Cast, Reaction, Transform }

    /// <summary>
    /// 카드 효과 종류의 유일한 선언. 종류 하나는 키, 분류, 소비하는 공격, 값 규칙, 적용 방식을 한 줄에 적는다.
    /// 새 효과는 여기에 한 줄을 더하면 된다. 등록하지 않은 키는 어떤 공격에서도 지원하지 않는 것으로 보고 검증이 막는다.
    /// 소비하지 않는 공격에 붙은 카드는 조용히 무시되므로(<see cref="Supports"/>) 카탈로그 검증이 카드 ID와 함께 거부한다.
    /// </summary>
    public static class EffectRegistry
    {
        [Flags]
        internal enum Users
        {
            Projectile = 1,
            Hitscan = 2,
            Area = 4,
            Beam = 8,
            Both = Projectile | Hitscan,
            All = Projectile | Hitscan | Area | Beam
        }

        internal sealed class Kind
        {
            public readonly UpgradeEffectCategory Category;
            public readonly Users Users;
            public readonly Action<WeaponStatsBuilder, float> Apply;
            public readonly Func<float, bool> Accept;
            /// <summary>숫자 하나로 표현되지 않는 효과(자식 스킬 시전 등)의 컴파일. null이면 value를 스탯에 적용하는 효과다</summary>
            public readonly Func<EffectDef, IUpgradeEffect> Compile;

            public Kind(UpgradeEffectCategory category, Users users, Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept,
                Func<EffectDef, IUpgradeEffect> compile = null)
            {
                Category = category;
                Users = users;
                Apply = apply;
                Accept = accept;
                Compile = compile;
            }
        }

        private static readonly Dictionary<string, Kind> Kinds = Create();

        public static IReadOnlyCollection<string> Keys => Kinds.Keys;

        public static bool IsRegistered(string key) => key != null && Kinds.ContainsKey(key);

        /// <summary>value를 스탯에 적용하는 효과인지(true), 자식 스킬 시전처럼 별도 필드를 쓰는 효과인지(false)</summary>
        public static bool IsStatKind(string key) => key != null && Kinds.TryGetValue(key, out var kind) && kind.Compile == null;

        /// <summary>자식 스킬을 시전하는 효과 종류인지</summary>
        public static bool IsChildCast(string key) => key == "onEvent" || key == "periodic";

        /// <summary>숫자 스탯이 아니라 별도 필드로 표현하는 효과 종류인지 (onEvent, periodic, inherit)</summary>
        public static bool IsHandler(string key) => key != null && Kinds.TryGetValue(key, out var kind) && kind.Compile != null;

        public static bool Supports(CastType castType, string key)
        {
            if (key == null || !Kinds.TryGetValue(key, out var kind)) { return false; }
            var user = castType switch { CastType.Hitscan => Users.Hitscan, CastType.Area => Users.Area, CastType.Beam => Users.Beam, _ => Users.Projectile };
            return (kind.Users & user) != 0;
        }

        internal static bool TryGet(string key, out Kind kind)
        {
            kind = null;
            return key != null && Kinds.TryGetValue(key, out kind);
        }

        private static bool Positive(float value) => value > 0;
        private static bool PositiveInteger(float value) => value > 0 && value == Math.Round(value);
        private static float Factor(float percent) => 1 + percent * .01f;

        private static Dictionary<string, Kind> Create()
        {
            var k = new Dictionary<string, Kind>();
            void Add(string key, UpgradeEffectCategory category, Users users, Action<WeaponStatsBuilder, float> apply, Func<float, bool> accept = null) =>
                k.Add(key, new Kind(category, users, apply, accept ?? (_ => true)));
            const UpgradeEffectCategory Plain = UpgradeEffectCategory.Stat, Cast = UpgradeEffectCategory.Cast,
                Reaction = UpgradeEffectCategory.Reaction, Transform = UpgradeEffectCategory.Transform;
            const Users Both = Users.Both, All = Users.All, Projectile = Users.Projectile, Hitscan = Users.Hitscan,
                Area = Users.Area, Beam = Users.Beam, ProjectileArea = Users.Projectile | Users.Area | Users.Beam;

            // 시전: 두 공격 모두 쿨타임, 피해, 시전 수, 형태를 쓴다.
            Add("attackSpeed", Plain, All, (s, v) => s[Stat.Cooldown] = Math.Max(.1f, s[Stat.Cooldown] * (1 - v * .01f)));
            Add("damage", Plain, All, (s, v) => { s[Stat.Damage] *= Factor(v); s[Stat.ExplosionDamage] *= Factor(v); });
            Add("impactDamage", Plain, Both, (s, v) => s[Stat.Damage] *= Factor(v));
            Add("projectileCount", Cast, All, (s, v) => s[Stat.ProjectileCount] = Math.Max(1, s[Stat.ProjectileCount] + (int)Math.Round(v)));
            Add("castCount", Cast, All, (s, v) => s[Stat.CastCount] = Math.Max(1, s[Stat.CastCount] + (int)v));
            Add("reserveCasts", Cast, Projectile, (s, v) => s[Stat.ReserveCastCount] = (int)v, PositiveInteger);
            Add("form", Transform, Both, (s, v) => s[Stat.Form] = (int)v,
                v => v == (int)WeaponForm.Enbakutsu || v == (int)WeaponForm.JudgementThunder
                    || v == (int)WeaponForm.TriangleIce || v == (int)WeaponForm.LargeLog || v == (int)WeaponForm.FireLog);

            // 투사체의 이동과 충돌
            Add("pierceCount", Plain, Projectile, (s, v) => s[Stat.PierceCount] = Math.Max(0, s[Stat.PierceCount] + (int)v));
            Add("projectileSpeed", Plain, Projectile, (s, v) => s[Stat.ProjectileSpeed] = Math.Max(0, s[Stat.ProjectileSpeed] * Factor(v)));
            Add("projectileSize", Plain, Projectile, (s, v) => s[Stat.ProjectileSizeMultiplier] *= Factor(v), Positive);
            Add("knockback", Plain, Projectile, (s, v) => s[Stat.KnockbackDistance] = Math.Max(0, s[Stat.KnockbackDistance] * Factor(v)));

            // 폭발은 두 공격 모두 명중 위치에서 터뜨린다.
            Add("enableExplosion", Reaction, Both, (s, v) => s[Stat.ExplosionRadius] = v, Positive);
            Add("explosionDamage", Plain, Both, (s, v) => s[Stat.ExplosionDamage] *= Factor(v));
            Add("explosionRadius", Plain, Both, (s, v) => s[Stat.ExplosionRadius] *= Factor(v));

            // 상태 이상: 마비만 Hitscan도 쓴다.
            Add("freezeDuration", Reaction, ProjectileArea, (s, v) => s[Stat.FreezeDuration] = Math.Max(s[Stat.FreezeDuration], v), Positive);
            Add("frostbite", Reaction, ProjectileArea, (s, v) => s[Stat.FrostbiteRatio] = v * .01f, Positive);
            Add("paralysis", Reaction, All, (s, v) => s[Stat.ParalysisDuration] = Math.Max(s[Stat.ParalysisDuration], v), Positive);
            Add("paralysisDuration", Plain, All, (s, v) => s[Stat.ParalysisDuration] += v, Positive);
            Add("stunChance", Plain, ProjectileArea, (s, v) => s[Stat.StunChance] = v * .01f, v => v > 0 && v <= 100);
            Add("stunDuration", Reaction, ProjectileArea, (s, v) => s[Stat.StunDuration] = Math.Max(s[Stat.StunDuration], v), Positive);
            Add("slowDuration", Reaction, ProjectileArea, (s, v) => s[Stat.SlowDuration] += v, Positive);
            Add("vulnerabilityRatio", Reaction, ProjectileArea, (s, v) => s[Stat.VulnerabilityRatio] = Math.Max(s[Stat.VulnerabilityRatio], v * .01f), Positive);
            Add("vulnerabilityDuration", Reaction, ProjectileArea, (s, v) => s[Stat.VulnerabilityDuration] = Math.Max(s[Stat.VulnerabilityDuration], v), Positive);

            // 점화
            Add("burnDuration", Reaction, Projectile, (s, v) => s[Stat.BurnDuration] = v, Positive);
            Add("burnRatio", Reaction, Projectile, (s, v) => s[Stat.BurnRatio] = v * .01f, Positive);
            Add("burnMaxHp", Reaction, Projectile, (s, v) => s[Stat.BurnMaxHpRatio] = v * .01f, Positive);
            Add("burnDeathExplosion", Reaction, Projectile, (s, v) => s[Stat.BurnDeathExplosion] = 1, v => v == 1);

            // 번개
            Add("lightningStrike", Reaction, Projectile, (s, v) => s[Stat.LightningStrikeRatio] = v * .01f, Positive);
            Add("killLightning", Reaction, Hitscan, (s, v) => s[Stat.KillLightningRatio] = v * .01f, Positive);

            // 전자기장은 Hitscan만 만든다.
            Add("fieldDuration", Plain, Hitscan, (s, v) => s[Stat.FieldDuration] += v, Positive);
            Add("fieldDamageFlat", Plain, Hitscan, (s, v) => s[Stat.FieldFlatDamage] += v, Positive);
            Add("fieldDamageMultiplier", Plain, Hitscan, (s, v) => s[Stat.FieldDamageMultiplier] *= Factor(v), Positive);

            // 영역: 서리 감옥처럼 자리에 머무는 공격만 쓴다.
            Add("areaRadius", Plain, Area, (s, v) => s[Stat.AreaRadius] *= Factor(v), Positive);
            Add("areaDuration", Plain, Area, (s, v) => s[Stat.AreaDuration] *= Factor(v), Positive);
            Add("areaMoveSpeed", Plain, Area, (s, v) => s[Stat.AreaMoveSpeed] *= Factor(v), Positive);
            Add("areaPull", Plain, Area, (s, v) => s[Stat.AreaPull] *= Factor(v), Positive);

            // 광선: 지속 시간 동안 닿는 모든 적을 공격하는 공격만 쓴다. 공격 횟수는 지속 시간 안의 펄스 수다.
            Add("beamLength", Plain, Beam, (s, v) => s[Stat.BeamLength] *= Factor(v), Positive);
            Add("beamWidth", Plain, Beam, (s, v) => s[Stat.BeamWidth] *= Factor(v), Positive);
            Add("beamDuration", Plain, Beam, (s, v) => s[Stat.BeamDuration] *= Factor(v), Positive);
            Add("beamPulses", Plain, Beam, (s, v) => s[Stat.BeamPulses] = Math.Max(1, s[Stat.BeamPulses] * Factor(v)), Positive);
            Add("beamPulsesFlat", Plain, Beam, (s, v) => s[Stat.BeamPulses] += v, PositiveInteger);

            // 자식 스킬 시전: 지정한 시점(onEvent) 또는 주기(periodic)에 다른 스킬을 그 위치에서 시전한다.
            k.Add("onEvent", new Kind(Reaction, All, null, null, ChildCastUpgradeEffect.FromEvent));
            k.Add("periodic", new Kind(Reaction, All, null, null, ChildCastUpgradeEffect.FromPeriodic));
            k.Add("inherit", new Kind(Reaction, All, null, null, ChildInheritUpgradeEffect.From));
            return k;
        }
    }
}
