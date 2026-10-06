using System;

namespace Game.Core
{
    /// <summary>패시브 종류 키. Monsters.json의 Passives[].Kind에 쓴다</summary>
    public static class PassiveKind
    {
        public const string SplitOnDeath = "SplitOnDeath";
        public const string SummonPeriodic = "SummonPeriodic";
        public const string Immunity = "Immunity";
    }

    /// <summary>몬스터 행에 붙는 패시브 한 개. Kind마다 쓰는 값이 다르고 안 쓰는 값은 비워 둔다</summary>
    [Serializable]
    public class PassiveDefinition
    {
        public string Kind;

        /// <summary>분열·소환으로 나오는 몬스터 (Monsters.Id)</summary>
        public int MonsterId;

        /// <summary>분열·소환 수</summary>
        public int Count;

        /// <summary>소환 주기(초)</summary>
        public float Interval;

        /// <summary>한 마리가 소환할 수 있는 총 수</summary>
        public int MaxTotal;

        /// <summary>면역인 상태이상 이름 (StatusImmunity)</summary>
        public string[] Statuses;

        public void Validate(int ownerId)
        {
            switch (Kind)
            {
                case PassiveKind.SplitOnDeath:
                    Require(MonsterId > 0 && Count >= 1, ownerId, "MonsterId는 1 이상, Count는 1 이상이어야 합니다");
                    break;
                case PassiveKind.SummonPeriodic:
                    Require(MonsterId > 0 && Count >= 1 && Interval > 0f && MaxTotal >= 1, ownerId,
                        "MonsterId, Count, MaxTotal은 1 이상, Interval은 0보다 커야 합니다");
                    break;
                case PassiveKind.Immunity:
                    Require(Statuses != null && Statuses.Length > 0, ownerId, "Statuses가 비어 있습니다");
                    foreach (var status in Statuses)
                    {
                        Require(TryParseStatus(status, out _), ownerId, $"알 수 없는 상태이상입니다: {status}");
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Monsters {ownerId}: 알 수 없는 패시브 Kind입니다: {Kind}");
            }
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

        private static bool TryParseStatus(string name, out StatusImmunity status) =>
            Enum.TryParse(name, true, out status) && status != StatusImmunity.None;

        private static void Require(bool condition, int ownerId, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Monsters {ownerId}: {message}");
            }
        }
    }
}
