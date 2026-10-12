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
                EnemyType.Boss => "보스",
                EnemyType.Elite => "엘리트",
                _ => "일반",
            };
            string attack = monster.GetAttackType() == AttackType.Ranged ? "원거리 공격" : "근접 공격";
            if (monster.BurstCount > 1)
            {
                attack += $" {monster.BurstCount}연타";
            }

            return $"{grade} · {attack}";
        }

        /// <param name="nameOf">분열·소환되는 몬스터의 이름 (MonsterDisplayTable)</param>
        public static List<string> Lines(MonsterDefinition monster, Func<int, string> nameOf)
        {
            var lines = new List<string>();
            foreach (var pair in monster.Resists ?? new Dictionary<string, float>())
            {
                lines.Add(Resist(ElementName(pair.Key) + " 공격", pair.Value));
            }

            foreach (var pair in monster.CastResists ?? new Dictionary<string, float>())
            {
                lines.Add(Resist(CastName(pair.Key) + " 공격", pair.Value));
            }

            if (monster.BlocksProjectile)
            {
                lines.Add("투사체의 직접 피해를 막고 관통시키지 않아요");
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
            return lines.Count == 0 ? "특별한 능력이 없어요." : "· " + string.Join("\n· ", lines);
        }

        private static string Resist(string what, float value)
        {
            if (value <= -1f) { return $"{what}{IGa(what)} 통하지 않아요"; }
            return value > 0f ? $"{what}에 약해요 (+{Percent(value)}%)" : $"{what}에 강해요 (-{Percent(-value)}%)";
        }

        private static string Passive(PassiveDefinition p, Func<int, string> nameOf)
        {
            switch (p.Kind)
            {
                case PassiveKind.Spawn:
                    string name = nameOf?.Invoke(p.MonsterId) ?? $"#{p.MonsterId}";
                    return p.Trigger == "Interval"
                        ? $"{Number(p.Interval)}초마다 {name} {p.Count}마리를 불러요" + (p.MaxTotal > 0 ? $" (최대 {p.MaxTotal}마리)" : string.Empty)
                        : $"쓰러지면 {name} {p.Count}마리로 나뉘어요";
                case PassiveKind.Immunity:
                    return $"{Statuses(p.Statuses)}에 걸리지 않아요";
                case PassiveKind.Modifier:
                    string target = p.Target == "KnockbackDistance" ? "밀려나는 거리" : p.Target == "BurnDuration" ? "점화 지속시간" : p.Target;
                    float m = p.Multiplier ?? 1f;
                    if (m <= 0f) { return $"{target}{IGa(target)} 없어요"; }
                    return m < 1f ? $"{target}{IGa(target)} {Percent(1f - m)}% 줄어요" : $"{target}{IGa(target)} {Percent(m - 1f)}% 늘어요";
                case PassiveKind.Shield:
                    string shield = p.Trigger == "OnHit"
                        ? $"맞으면 {Percent(p.Chance)}% 확률로 공격 {p.Hits}번을 막는 방어막을 펼쳐요"
                        : $"처음부터 공격 {p.Hits}번을 막는 방어막이 있어요";
                    if (p.Duration > 0f) { shield += $" ({Number(p.Duration)}초)"; }
                    if (p.Statuses != null && p.Statuses.Length > 0) { shield += $"\n  방어막이 있는 동안 {Statuses(p.Statuses)}에 걸리지 않아요"; }
                    return shield;
                case PassiveKind.Evade:
                    return $"{Percent(p.Chance)}% 확률로 공격을 피해요";
                case PassiveKind.SpeedBoost:
                    return $"{Number(p.Interval)}초마다 {Number(p.Duration)}초 동안 {Number(p.SpeedMultiplier)}배 빨라져요";
                case PassiveKind.LowHp:
                    string low = $"체력이 {Percent(p.HpRatio)}% 이하가 되면 {Number(p.Duration)}초 동안";
                    if (p.HealRatio > 0f) { low += $" 최대 체력의 {Percent(p.HealRatio)}%를 회복해요"; }
                    if (p.Invulnerable) { low += " 피해를 받지 않아요"; }
                    return low;
                case PassiveKind.HealOnHit:
                    string by = string.IsNullOrEmpty(p.Element) ? string.Empty : $"{ElementName(p.Element)} 공격에 ";
                    string chance = p.Chance < 1f ? $"{Percent(p.Chance)}% 확률로 " : string.Empty;
                    return $"{by}맞으면 {chance}최대 체력의 {Percent(p.HealRatio)}%를 회복해요";
                default:
                    return null;
            }
        }

        public static string ElementName(string element) => element switch
        {
            "Neutral" => "무속성",
            "Fire" => "화속성",
            "Ice" => "빙속성",
            "Wind" => "풍속성",
            "Lightning" => "뇌속성",
            "Earth" => "토속성",
            _ => element,
        };

        private static string CastName(string cast) => cast switch
        {
            "Projectile" => "투사체",
            "Hitscan" => "즉발",
            "Area" => "범위",
            "Beam" => "광선",
            "Chain" => "연쇄",
            _ => cast,
        };

        private static string Statuses(string[] statuses)
        {
            if (statuses == null || statuses.Length == 0) { return "상태이상"; }
            var names = new List<string>();
            foreach (var status in statuses)
            {
                names.Add(status switch
                {
                    "AllDebuffs" => "모든 상태이상",
                    "Stun" => "기절",
                    "Burn" => "점화",
                    "Paralysis" => "마비",
                    "Freeze" => "빙결",
                    "Slow" => "둔화",
                    "Frostbite" => "동상",
                    "Vulnerability" => "취약",
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
