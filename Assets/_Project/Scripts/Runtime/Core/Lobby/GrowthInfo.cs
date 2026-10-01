using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>능력치 한 줄. Next가 null이면 변화 없이 Current만 표시한다</summary>
    public readonly struct StatLine
    {
        public string Label { get; }
        public string Current { get; }
        public string Next { get; }

        public StatLine(string label, string current, string next = null)
        {
            Label = label;
            Current = current;
            Next = next;
        }
    }

    /// <summary>장비에 낀 장식품 효과 한 줄. IsActive가 false면 회색 보석으로 표시한다</summary>
    public readonly struct EquipEffect
    {
        public string Text { get; }
        public bool IsActive { get; }

        public EquipEffect(string text, bool isActive = true)
        {
            Text = text;
            IsActive = isActive;
        }
    }

    /// <summary>인술 하나의 표시용 정보 (인술 화면 칸, 인술 강화 팝업)</summary>
    public sealed class NinpoInfo
    {
        public string Id;
        public string Name;
        public string Description;
        public int Level;
        public int MaxLevel;

        /// <summary>해금에 필요한 플레이어 레벨. 잠긴 칸에 "레벨 N에 해금"으로 표시한다</summary>
        public int UnlockLevel;
        public bool IsUnlocked;

        /// <summary>강화 전 → 후 능력치</summary>
        public IReadOnlyList<StatLine> Stats;

        /// <summary>다음 레벨로 강화하는 비용 (엽전, 인법서)</summary>
        public int CoinCost;
        public int BookCost;

        public bool IsMaxLevel => Level >= MaxLevel;
    }

    /// <summary>장비 칸 하나의 표시용 정보 (닌자 화면 장비 칸, 장비 강화 팝업)</summary>
    public sealed class EquipInfo
    {
        public int Slot;
        public string Name;
        public int Level;

        /// <summary>칸 해금에 필요한 캐릭터 레벨</summary>
        public int UnlockLevel;
        public bool IsUnlocked;

        public IReadOnlyList<StatLine> Stats;
        public IReadOnlyList<EquipEffect> Effects;

        /// <summary>다음 레벨로 강화하는 비용 (엽전, 강화서)</summary>
        public int CoinCost;
        public int BookCost;
    }

    /// <summary>닌자 화면 위쪽에 표시하는 캐릭터 정보</summary>
    public sealed class ShinobiInfo
    {
        public string Name;
        public string Rank;
        public int Level;
        public int Power;
    }
}
