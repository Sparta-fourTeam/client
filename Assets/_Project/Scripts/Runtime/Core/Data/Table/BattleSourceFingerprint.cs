using System;
using System.Security.Cryptography;
using System.Text;

namespace Game.Core
{
    /// <summary>원문별 SHA-256을 고정 순서로 묶는다. 숫자 버전이나 클라 보고값으로 원본을 대신하지 않는다.</summary>
    public static class BattleSourceFingerprint
    {
        public static string Compute(string stages, string monsters, string combat) =>
            Hash("battle-policy-v1\n" + Hash(stages) + "\n" + Hash(monsters) + "\n" + Hash(combat) + "\n");

        public static string Hash(string raw)
        {
            if (raw == null) { throw new ArgumentNullException(nameof(raw)); }
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", "").ToLowerInvariant();
        }
    }
}
