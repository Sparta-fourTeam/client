using System;

namespace Game.Core
{
    /// <summary>패시브 종류 키. Monsters.json의 Passives[].Kind에 쓴다</summary>
    public static class PassiveKind
    {
        public const string Spawn = "Spawn";
        public const string Immunity = "Immunity";
        public const string Modifier = "Modifier";
        public const string Shield = "Shield";
        public const string Evade = "Evade";
        public const string LowHp = "LowHp";
        public const string HealOnHit = "HealOnHit";
        public const string SpeedBoost = "SpeedBoost";
    }

    /// <summary>몬스터 행에 붙는 패시브 한 개. Kind마다 쓰는 값이 다르고 안 쓰는 값은 비워 둔다</summary>
    [Serializable]
    public class PassiveDefinition
    {
        public string Kind;

        /// <summary>Spawn: 만들어 달라고 요청하는 때 (OnDeath, Interval). Shield: 방어막이 켜지는 때 (Start, OnHit)</summary>
        public string Trigger;

        /// <summary>Spawn: 만들 몬스터 (Monsters.Id)</summary>
        public int MonsterId;

        /// <summary>Spawn: 한 번에 만드는 수</summary>
        public int Count;

        /// <summary>Spawn(Interval), SpeedBoost: 주기(초)</summary>
        public float Interval;

        /// <summary>Spawn(Interval): 한 마리가 만들 수 있는 총 수</summary>
        public int MaxTotal;

        /// <summary>Immunity: 면역인 상태이상 이름 (StatusImmunity). Shield, LowHp: 켜져 있는 동안 걸리지 않는 상태이상(선택)</summary>
        public string[] Statuses;

        /// <summary>Modifier: 효과의 크기를 바꿀 대상 (ModifierTarget: KnockbackDistance, BurnDuration)</summary>
        public string Target;

        /// <summary>Modifier: 받는 효과에 곱하는 배수. 0.5면 절반, 0이면 무효, 4면 4배. 같은 대상이 여러 개면 곱한다</summary>
        public float? Multiplier;

        /// <summary>Shield: 막는 공격 횟수</summary>
        public int Hits;

        /// <summary>Shield: 방어막이 유지되는 시간(초). 0이면 횟수를 다 막을 때까지. LowHp: 효과가 켜져 있는 시간(초). HealOnHit, SpeedBoost: 이속이 바뀌는 시간(초)</summary>
        public float Duration;

        /// <summary>Shield(OnHit), Evade, HealOnHit: 발동 확률(0 초과 1 이하). 생략하면 1</summary>
        public float Chance = 1f;

        /// <summary>LowHp: 이 체력 비율 이하가 되면 발동(0 초과 1 미만)</summary>
        public float HpRatio;

        /// <summary>LowHp: 켜져 있는 동안 최대 체력의 이 비율만큼 나눠서 회복한다. HealOnHit: 피격 한 번에 회복하는 최대 체력의 비율</summary>
        public float HealRatio;

        /// <summary>LowHp: 켜져 있는 동안 상태이상 지속 피해까지 모두 막는다</summary>
        public bool Invulnerable;

        /// <summary>LowHp, HealOnHit, SpeedBoost: 이동 속도에 곱하는 배수. 생략하면 1</summary>
        public float SpeedMultiplier = 1f;

        /// <summary>HealOnHit: 이 속성(Element 이름)의 공격에 맞을 때만 반응한다. 비우면 모든 속성</summary>
        public string Element;

        public void Validate(int ownerId)
        {
            switch (Kind)
            {
                case PassiveKind.Spawn:
                    ValidateSpawn(ownerId);
                    break;
                case PassiveKind.Immunity:
                    Require(Statuses != null && Statuses.Length > 0, ownerId, "Statuses가 비어 있습니다");
                    ValidateStatuses(ownerId);
                    break;
                case PassiveKind.Modifier:
                    Require(TryParseTarget(out _), ownerId, $"알 수 없는 Target입니다: {Target}");
                    Require(Multiplier.HasValue && Multiplier.Value >= 0f && !float.IsNaN(Multiplier.Value) && !float.IsInfinity(Multiplier.Value),
                        ownerId, "Multiplier는 0 이상의 숫자여야 합니다");
                    break;
                case PassiveKind.Shield:
                    ValidateShield(ownerId);
                    break;
                case PassiveKind.Evade:
                    ValidateChance(ownerId);
                    break;
                case PassiveKind.LowHp:
                    ValidateLowHp(ownerId);
                    break;
                case PassiveKind.HealOnHit:
                    ValidateHealOnHit(ownerId);
                    break;
                case PassiveKind.SpeedBoost:
                    Require(IsPositive(Interval) && IsPositive(Duration) && IsPositive(SpeedMultiplier), ownerId,
                        "Interval, Duration, SpeedMultiplier는 0보다 커야 합니다");
                    break;
                default:
                    throw new InvalidOperationException($"Monsters {ownerId}: 알 수 없는 패시브 Kind입니다: {Kind}");
            }
        }

        private void ValidateSpawn(int ownerId)
        {
            Require(TryParseSpawnTrigger(out var trigger), ownerId, $"알 수 없는 Trigger입니다: {Trigger}");
            Require(MonsterId > 0 && Count >= 1, ownerId, "MonsterId와 Count는 1 이상이어야 합니다");
            if (trigger == SpawnTrigger.Interval)
            {
                Require(Interval > 0f && MaxTotal >= 1, ownerId, "Interval은 0보다 크고 MaxTotal은 1 이상이어야 합니다");
            }
        }

        private void ValidateShield(int ownerId)
        {
            Require(TryParseShieldTrigger(out var trigger), ownerId, $"알 수 없는 Trigger입니다: {Trigger}");
            Require(Hits >= 1, ownerId, "Hits는 1 이상이어야 합니다");
            Require(Duration >= 0f && !float.IsNaN(Duration) && !float.IsInfinity(Duration), ownerId, "Duration은 0 이상이어야 합니다");
            if (trigger == ShieldTrigger.OnHit)
            {
                ValidateChance(ownerId);
            }

            ValidateStatuses(ownerId);
        }

        private void ValidateLowHp(int ownerId)
        {
            Require(HpRatio > 0f && HpRatio < 1f, ownerId, "HpRatio는 0보다 크고 1보다 작아야 합니다");
            Require(IsPositive(Duration), ownerId, "Duration은 0보다 커야 합니다");
            Require(HealRatio >= 0f && !float.IsNaN(HealRatio) && !float.IsInfinity(HealRatio), ownerId, "HealRatio는 0 이상이어야 합니다");
            Require(IsPositive(SpeedMultiplier), ownerId, "SpeedMultiplier는 0보다 커야 합니다");
            ValidateStatuses(ownerId);
            Require(HealRatio > 0f || Invulnerable || SpeedMultiplier != 1f || (Statuses != null && Statuses.Length > 0), ownerId,
                "켜질 효과가 하나도 없습니다 (HealRatio, Invulnerable, SpeedMultiplier, Statuses 중 하나가 필요합니다)");
        }

        private void ValidateHealOnHit(int ownerId)
        {
            Require(HealRatio >= 0f && !float.IsNaN(HealRatio) && !float.IsInfinity(HealRatio), ownerId, "HealRatio는 0 이상이어야 합니다");
            Require(IsPositive(SpeedMultiplier), ownerId, "SpeedMultiplier는 0보다 커야 합니다");
            Require(SpeedMultiplier == 1f || IsPositive(Duration), ownerId, "SpeedMultiplier가 1이 아니면 Duration은 0보다 커야 합니다");
            Require(HealRatio > 0f || SpeedMultiplier != 1f, ownerId, "반응할 효과가 하나도 없습니다 (HealRatio나 SpeedMultiplier가 필요합니다)");
            ValidateChance(ownerId);
            if (!string.IsNullOrEmpty(Element))
            {
                Require(TryParseElement(out _), ownerId, $"알 수 없는 Element입니다: {Element}");
            }
        }

        private void ValidateChance(int ownerId) =>
            Require(Chance > 0f && Chance <= 1f, ownerId, "Chance는 0보다 크고 1 이하여야 합니다");

        private void ValidateStatuses(int ownerId)
        {
            foreach (var status in Statuses ?? Array.Empty<string>())
            {
                Require(TryParseStatus(status, out _), ownerId, $"알 수 없는 상태이상입니다: {status}");
            }
        }

        public SpawnTrigger ToSpawnTrigger()
        {
            TryParseSpawnTrigger(out var trigger);
            return trigger;
        }

        public ShieldTrigger ToShieldTrigger()
        {
            TryParseShieldTrigger(out var trigger);
            return trigger;
        }

        public ModifierTarget ToModifierTarget()
        {
            TryParseTarget(out var target);
            return target;
        }

        /// <summary>HealOnHit의 Element. 비어 있으면 null(모든 속성)</summary>
        public Element? ToElement() => string.IsNullOrEmpty(Element) ? null : TryParseElement(out var element) ? element : null;

        /// <summary>Statuses 이름을 StatusImmunity 플래그로 합친다. Statuses가 없으면 None</summary>
        public StatusImmunity ToImmunities()
        {
            var flags = StatusImmunity.None;
            foreach (var status in Statuses ?? Array.Empty<string>())
            {
                TryParseStatus(status, out var parsed);
                flags |= parsed;
            }

            return flags;
        }

        private bool TryParseSpawnTrigger(out SpawnTrigger trigger) =>
            Enum.TryParse(Trigger, true, out trigger) && Enum.IsDefined(typeof(SpawnTrigger), trigger);

        private bool TryParseShieldTrigger(out ShieldTrigger trigger)
        {
            trigger = default;
            return !string.IsNullOrEmpty(Trigger) && !char.IsDigit(Trigger[0])
                && Enum.TryParse(Trigger, true, out trigger) && Enum.IsDefined(typeof(ShieldTrigger), trigger);
        }

        private bool TryParseTarget(out ModifierTarget target)
        {
            target = default;
            return !string.IsNullOrEmpty(Target) && !char.IsDigit(Target[0])
                && Enum.TryParse(Target, true, out target) && Enum.IsDefined(typeof(ModifierTarget), target);
        }

        // 속성 이름은 DamageProfile의 Resists 키와 같이 대소문자를 구분한다
        private bool TryParseElement(out Element element)
        {
            element = default;
            return !string.IsNullOrEmpty(Element) && !char.IsDigit(Element[0])
                && Enum.TryParse(Element, false, out element) && Enum.IsDefined(typeof(Element), element);
        }

        // 이름으로만 받는다. 숫자나 여러 개를 합친 값(예: "3")은 거부한다
        private static bool TryParseStatus(string name, out StatusImmunity status)
        {
            status = StatusImmunity.None;
            return !string.IsNullOrEmpty(name) && !char.IsDigit(name[0])
                && Enum.TryParse(name, true, out status) && status != StatusImmunity.None && Enum.IsDefined(typeof(StatusImmunity), status);
        }

        private static bool IsPositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Require(bool condition, int ownerId, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Monsters {ownerId}: {message}");
            }
        }
    }
}
