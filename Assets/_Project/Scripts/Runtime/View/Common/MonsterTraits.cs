using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;

namespace Game.View
{
    /// <summary>몬스터 상세 창의 문구. 등급·공격 방식(부제)과 속성 약점·저항, 시전 형태 저항, 투사체 막기, 패시브를 사람이 읽는 문장으로 만든다.
    /// 문장은 Monsters 표의 값에서 바로 만들므로 수치를 바꾸면 문구도 따라 바뀐다</summary>
    public static class MonsterTraits
    {
        public static string Subtitle(MonsterDefinition monster)
        {
            string grade = monster.GetEnemyType() switch
            {
                EnemyType.Boss => L10n.Get("codex.tab.boss"),
                EnemyType.Elite => L10n.Get("codex.tab.elite"),
                _ => L10n.Get("codex.tab.normal"),
            };
            string attack = L10n.Get(monster.GetAttackType() == AttackType.Ranged ? "monster.attackRanged" : "monster.attackMelee");
            if (monster.BurstCount > 1)
            {
                attack += " " + L10n.Get("monster.burstSuffix", monster.BurstCount.ToString());
            }

            return $"{grade} · {attack}";
        }

        /// <param name="nameOf">분열·소환되는 몬스터의 이름 (MonsterDisplayTable)</param>
        public static List<string> Lines(MonsterDefinition monster, Func<int, string> nameOf)
        {
            var lines = new List<string>();
            foreach (var pair in monster.Resists ?? new Dictionary<string, float>())
            {
                lines.Add(Resist(L10n.Get("monster.attackLabel", ElementName(pair.Key)), pair.Value));
            }

            foreach (var pair in monster.CastResists ?? new Dictionary<string, float>())
            {
                lines.Add(Resist(L10n.Get("monster.attackLabel", CastName(pair.Key)), pair.Value));
            }

            if (monster.BlocksProjectile)
            {
                lines.Add(L10n.Get("monster.blocksProjectile"));
            }

            foreach (var passive in monster.Passives ?? new List<PassiveDefinition>())
            {
                string line = Passive(passive, nameOf);
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        /// <summary>상세 창 설명칸에 넣을 글. 특징이 없으면 안내 한 줄</summary>
        public static string Description(MonsterDefinition monster, Func<int, string> nameOf)
        {
            var lines = Lines(monster, nameOf);
            return lines.Count == 0 ? L10n.Get("monster.noAbility") : "· " + string.Join("\n· ", lines);
        }

        private static string Resist(string what, float value)
        {
            if (value <= -1f) { return L10n.Get("monster.resistImmune", what, IGa(what)); }
            return value > 0f ? L10n.Get("monster.resistWeak", what, Percent(value)) : L10n.Get("monster.resistStrong", what, Percent(-value));
        }

        private static string Passive(PassiveDefinition p, Func<int, string> nameOf)
        {
            switch (p.Kind)
            {
                case PassiveKind.Spawn:
                    string name = nameOf?.Invoke(p.MonsterId) ?? $"#{p.MonsterId}";
                    return p.Trigger == "Interval"
                        ? L10n.Get("monster.spawnInterval", Number(p.Interval), name, p.Count.ToString())
                            + (p.MaxTotal > 0 ? " " + L10n.Get("monster.spawnMax", p.MaxTotal.ToString()) : string.Empty)
                        : L10n.Get("monster.spawnOnDeath", name, p.Count.ToString());
                case PassiveKind.Immunity:
                    return L10n.Get("monster.immune", Statuses(p.Statuses));
                case PassiveKind.Modifier:
                    string target = p.Target == "KnockbackDistance" ? L10n.Get("monster.targetKnockback")
                        : p.Target == "BurnDuration" ? L10n.Get("monster.targetBurnDuration") : p.Target;
                    float m = p.Multiplier ?? 1f;
                    if (m <= 0f) { return L10n.Get("monster.modNone", target, IGa(target)); }
                    return m < 1f ? L10n.Get("monster.modDecrease", target, IGa(target), Percent(1f - m))
                        : L10n.Get("monster.modIncrease", target, IGa(target), Percent(m - 1f));
                case PassiveKind.Shield:
                    string shield = p.Trigger == "OnHit"
                        ? L10n.Get("monster.shieldOnHit", Percent(p.Chance), p.Hits.ToString())
                        : L10n.Get("monster.shieldStart", p.Hits.ToString());
                    if (p.Duration > 0f) { shield += " " + L10n.Get("monster.shieldDuration", Number(p.Duration)); }
                    if (p.Statuses != null && p.Statuses.Length > 0) { shield += "\n  " + L10n.Get("monster.shieldImmune", Statuses(p.Statuses)); }
                    return shield;
                case PassiveKind.Evade:
                    return L10n.Get("monster.evade", Percent(p.Chance));
                case PassiveKind.SpeedBoost:
                    return L10n.Get("monster.speedBoost", Number(p.Interval), Number(p.Duration), Number(p.SpeedMultiplier));
                case PassiveKind.LowHp:
                    bool heals = p.HealRatio > 0f;
                    string lowKey = heals ? (p.Invulnerable ? "monster.lowHpHealInvuln" : "monster.lowHpHeal")
                        : p.Invulnerable ? "monster.lowHpInvuln" : "monster.lowHpOnly";
                    return L10n.Get(lowKey, Percent(p.HpRatio), Number(p.Duration), Percent(p.HealRatio));
                case PassiveKind.HealOnHit:
                    bool hasElement = !string.IsNullOrEmpty(p.Element);
                    bool hasChance = p.Chance < 1f;
                    string healKey = hasElement ? (hasChance ? "monster.healOnHitElementChance" : "monster.healOnHitElement")
                        : hasChance ? "monster.healOnHitChance" : "monster.healOnHit";
                    return L10n.Get(healKey, Percent(p.HealRatio), hasElement ? ElementName(p.Element) : string.Empty, Percent(p.Chance));
                default:
                    return null;
            }
        }

        public static string ElementName(string element) => element switch
        {
            "Neutral" => L10n.Get("element.neutral"),
            "Fire" => L10n.Get("element.fire"),
            "Ice" => L10n.Get("element.ice"),
            "Wind" => L10n.Get("element.wind"),
            "Lightning" => L10n.Get("element.lightning"),
            "Earth" => L10n.Get("element.earth"),
            _ => element,
        };

        private static string CastName(string cast) => cast switch
        {
            "Projectile" => L10n.Get("cast.projectile"),
            "Instant" => L10n.Get("cast.instant"),
            "Area" => L10n.Get("cast.area"),
            "Beam" => L10n.Get("cast.beam"),
            "Chain" => L10n.Get("cast.chain"),
            _ => cast,
        };

        private static string Statuses(string[] statuses)
        {
            if (statuses == null || statuses.Length == 0) { return L10n.Get("status.generic"); }
            var names = new List<string>();
            foreach (var status in statuses)
            {
                names.Add(status switch
                {
                    "AllDebuffs" => L10n.Get("status.all"),
                    "Stun" => L10n.Get("status.stun"),
                    "Burn" => L10n.Get("status.burn"),
                    "Paralysis" => L10n.Get("status.paralysis"),
                    "Freeze" => L10n.Get("status.freeze"),
                    "Slow" => L10n.Get("status.slow"),
                    "Frostbite" => L10n.Get("status.frostbite"),
                    "Vulnerability" => L10n.Get("status.vulnerability"),
                    _ => status,
                });
            }

            return string.Join("·", names);
        }

        // 앞 글자에 받침이 있으면 "이", 없으면 "가"
        private static string IGa(string word)
        {
            if (string.IsNullOrEmpty(word)) { return "이"; }
            char last = word[word.Length - 1];
            return last >= '가' && last <= '힣' && (last - '가') % 28 != 0 ? "이" : "가";
        }

        private static string Percent(float ratio) => Number(ratio * 100f);

        private static string Number(float value) => ((decimal)Math.Round(value, 2)).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
