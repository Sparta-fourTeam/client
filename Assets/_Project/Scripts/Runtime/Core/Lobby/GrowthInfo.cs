using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>능력치 한 줄. Next가 null이면 변화 없이 Current만 표시한다</summary>
    public readonly struct StatLine
    {
        public string Label { get; }
        public string Current { get; }
        public string Next { get; }

        /// <summary>다음 레벨에서 오르는 양 (예: "+127.32"). null이면 증가량을 표시하지 않는다</summary>
        public string Increase { get; }

        public StatLine(string label, string current, string next = null, string increase = null)
        {
            Label = label;
            Current = current;
            Next = next;
            Increase = increase;
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

    /// <summary>스킬 하나의 표시용 정보 (스킬 화면 칸, 스킬 강화 팝업). 영구 강화가 정의된 스킬은 레벨·비용이 Upgrades 테이블과 PlayerProfile에서 온다</summary>
    public sealed class SkillInfo
    {
        public string Id;

        /// <summary>강화 API에 넘기는 업그레이드 ID (스킬의 progressionId, 예: shuriken). null이면 아직 강화 데이터가 없는 스킬이다</summary>
        public string UpgradeId;
        public string Name;
        public string Description;
        public int Level;
        public int MaxLevel;

        /// <summary>해금에 필요한 플레이어 레벨. 잠긴 칸에 "레벨 N에 해금"으로 표시한다</summary>
        public int UnlockLevel;
        public bool IsUnlocked;

        /// <summary>현재 능력치와 다음 레벨 증가량</summary>
        public IReadOnlyList<StatLine> Stats;

        /// <summary>다음 레벨로 강화하는 비용: 코인과 재료(스킬 마법북 아이템 ID, 수량). 최대 레벨이면 쓰지 않는다</summary>
        public int CoinCost;
        public string MaterialItemId;
        public int MaterialCost;

        /// <summary>레벨별 보상. 달성 여부는 LevelReward.Level <= Level로 판단한다</summary>
        public IReadOnlyList<LevelReward> LevelRewards;

        public bool IsMaxLevel => Level >= MaxLevel;
    }

    /// <summary>영구 강화 레벨에 도달하면 얻는 것 한 줄 (예: 1 "[폭풍 집결] 선택지 잠금해제")</summary>
    public sealed class LevelReward
    {
        public int Level;
        public string Text;
    }

    /// <summary>장비 칸 하나의 표시용 정보 (캐릭터 화면 장비 칸, 장비 강화 팝업). 레벨·비용·해금은 Upgrades 테이블과 PlayerProfile에서 온다</summary>
    public sealed class EquipInfo
    {
        public int Slot;

        /// <summary>강화 API에 넘기는 업그레이드 ID (예: equipment.hat)</summary>
        public string UpgradeId;
        public string Name;
        public int Level;
        public int MaxLevel;

        /// <summary>칸 해금에 필요한 플레이어 레벨</summary>
        public int UnlockLevel;
        public bool IsUnlocked;

        public IReadOnlyList<StatLine> Stats;
        public IReadOnlyList<EquipEffect> Effects;

        public bool IsMaxLevel => Level >= MaxLevel;

        /// <summary>다음 레벨로 강화하는 비용: 코인과 재료(아이템 ID, 수량). 최대 레벨이면 쓰지 않는다</summary>
        public int CoinCost;
        public string MaterialItemId;
        public int MaterialCost;
    }

    /// <summary>캐릭터 화면 위쪽에 표시하는 캐릭터 정보</summary>
    public sealed class CharacterInfo
    {
        public string Name;
        public string Rank;
        public int Power;
    }
}
