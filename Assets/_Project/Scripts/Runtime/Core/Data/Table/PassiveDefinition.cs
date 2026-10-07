using System;

namespace Game.Core
{
    /// <summary>패시브 종류 키. Monsters.json의 Passives[].Kind에 쓴다</summary>
    public static class PassiveKind
    {
        public const string Spawn = "Spawn";
        public const string Immunity = "Immunity";
        public const string Modifier = "Modifier";
    }

    /// <summary>몬스터 행에 붙는 패시브 한 개. Kind마다 쓰는 값이 다르고 안 쓰는 값은 비워 둔다</summary>
    [Serializable]
    public class PassiveDefinition
    {
        public string Kind;

        /// <summary>Spawn: 만들어 달라고 요청하는 때 (OnDeath, Interval)</summary>
        public string Trigger;

        /// <summary>Spawn: 만들 몬스터 (Monsters.Id)</summary>
        public int MonsterId;

        /// <summary>Spawn: 한 번에 만드는 수</summary>
        public int Count;

        /// <summary>Spawn(Interval): 주기(초)</summary>
        public float Interval;

        /// <summary>Spawn(Interval): 한 마리가 만들 수 있는 총 수</summary>
        public int MaxTotal;

        /// <summary>면역인 상태이상 이름 (StatusImmunity)</summary>
        public string[] Statuses;

        /// <summary>Modifier: 효과의 크기를 바꿀 대상 (ModifierTarget: KnockbackDistance, BurnDuration)</summary>
        public string Target;

        /// <summary>Modifier: 받는 효과에 곱하는 배수. 0.5면 절반, 0이면 무효, 4면 4배. 같은 대상이 여러 개면 곱한다</summary>
        public float? Multiplier;

        public void Validate(int ownerId)
        {
            switch (Kind)
            {
                case PassiveKind.Spawn:
                    Require(TryParseTrigger(out var trigger), ownerId, $"알 수 없는 Trigger입니다: {Trigger}");
                    Require(MonsterId > 0 && Count >= 1, ownerId, "MonsterId와 Count는 1 이상이어야 합니다");
                    if (trigger == SpawnTrigger.Interval)
                    {
                        Require(Interval > 0f && MaxTotal >= 1, ownerId, "Interval은 0보다 크고 MaxTotal은 1 이상이어야 합니다");
                    }

                    break;
                case PassiveKind.Immunity:
                    Require(Statuses != null && Statuses.Length > 0, ownerId, "Statuses가 비어 있습니다");
                    foreach (var status in Statuses)
                    {
                        Require(TryParseStatus(status, out _), ownerId, $"알 수 없는 상태이상입니다: {status}");
                    }

                    break;
                case PassiveKind.Modifier:
                    Require(TryParseTarget(out _), ownerId, $"알 수 없는 Target입니다: {Target}");
                    Require(Multiplier.HasValue && Multiplier.Value >= 0f && !float.IsNaN(Multiplier.Value) && !float.IsInfinity(Multiplier.Value),
                        ownerId, "Multiplier는 0 이상의 숫자여야 합니다");
                    break;
                default:
                    throw new InvalidOperationException($"Monsters {ownerId}: 알 수 없는 패시브 Kind입니다: {Kind}");
            }
        }

        public ModifierTarget ToModifierTarget()
        {
            TryParseTarget(out var target);
            return target;
        }

        public SpawnTrigger ToSpawnTrigger()
        {
            TryParseTrigger(out var trigger);
            return trigger;
        }

        /// <summary>Statuses 이름을 StatusImmunity 플래그로 합친다</summary>
        public StatusImmunity ToImmunities()
        {
            var flags = StatusImmunity.None;
            foreach (var status in Statuses)
            {
                TryParseStatus(status, out var parsed);
                flags |= parsed;
            }

            return flags;
        }

        private bool TryParseTrigger(out SpawnTrigger trigger) =>
            Enum.TryParse(Trigger, true, out trigger) && Enum.IsDefined(typeof(SpawnTrigger), trigger);

        private bool TryParseTarget(out ModifierTarget target)
        {
            target = default;
            return !string.IsNullOrEmpty(Target) && !char.IsDigit(Target[0])
                && Enum.TryParse(Target, true, out target) && Enum.IsDefined(typeof(ModifierTarget), target);
        }

        // 이름으로만 받는다. 숫자나 여러 개를 합친 값(예: "3")은 거부한다
        private static bool TryParseStatus(string name, out StatusImmunity status)
        {
            status = StatusImmunity.None;
            return !string.IsNullOrEmpty(name) && !char.IsDigit(name[0])
                && Enum.TryParse(name, true, out status) && status != StatusImmunity.None && Enum.IsDefined(typeof(StatusImmunity), status);
        }

        private static void Require(bool condition, int ownerId, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Monsters {ownerId}: {message}");
            }
        }
    }
}
