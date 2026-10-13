using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>누적 경험치로 플레이어 레벨을 계산한다. 레벨은 저장하지 않고 exp에서 항상 파생한다 (전투 중 스킬 레벨과 별개)</summary>
    public sealed class PlayerLevelTable
    {
        private readonly int[] _required; // _required[i] = 레벨 i+1에서 i+2로 오르는 데 필요한 경험치

        /// <summary>행은 Level 1부터 빠짐없이 이어져야 하고 RequiredExp는 양수여야 한다. 만렙은 마지막 행의 Level + 1</summary>
        public PlayerLevelTable(IReadOnlyList<PlayerLevelRow> rows)
        {
            if (rows == null || rows.Count == 0) { throw new InvalidOperationException("PlayerLevels: 행이 없습니다."); }

            _required = new int[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Level != i + 1) { throw new InvalidOperationException($"PlayerLevels: Level {i + 1}이(가) 순서대로 있어야 합니다."); }
                if (rows[i].RequiredExp <= 0) { throw new InvalidOperationException($"PlayerLevels {rows[i].Level}: RequiredExp는 양수여야 합니다."); }

                _required[i] = rows[i].RequiredExp;
            }
        }

        public int MaxLevel => _required.Length + 1;

        /// <summary>level에서 level+1로 오르는 데 필요한 경험치. 만렙이면 0</summary>
        public int RequiredToNext(int level)
        {
            if (level < 1) { throw new ArgumentOutOfRangeException(nameof(level)); }

            return level >= MaxLevel ? 0 : _required[level - 1];
        }

        /// <summary>누적 exp로 도달한 레벨과 그 레벨 안에서의 진행 경험치. 한 번에 여러 레벨이 올라도 반복 차감으로 처리한다</summary>
        public (int level, int expIntoLevel) At(int totalExp)
        {
            long remaining = Math.Max(0, totalExp);
            int level = 1;
            while (level < MaxLevel && remaining >= _required[level - 1])
            {
                remaining -= _required[level - 1];
                level++;
            }

            // 만렙은 남는 경험치를 그대로 보관한다
            return (level, (int)remaining);
        }
    }
}
