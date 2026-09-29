using System;

namespace Game.Core
{
    public enum CardType
    {
        /// <summary>무기 신규 획득 또는 강화. WeaponId + (강화 카드면) UpgradeOption을 쓴다</summary>
        Skill,

        /// <summary>후보가 3장 미만일 때 채우는 폴백 카드. 항상 후보가 될 수 있다(MaxPicks를 크게 잡는다)</summary>
        WallRepair
    }

    /// <summary>카드 3지선다 테이블 한 행. WeaponId로 실제 무기 시스템(WeaponController)과 연결된다</summary>
    [Serializable]
    public class CardDefinition
    {
        public string Id;
        public CardType Type = CardType.Skill;

        public int WeaponId;

        /// <summary>Type이 Skill이고 이미 보유 중인 무기를 강화하는 카드일 때만 쓰인다</summary>
        public UpgradeType UpgradeOption;

        /// <summary>Type이 WallRepair일 때만 쓰인다</summary>
        public int WallRepairAmount;

        /// <summary>이 카드를 뽑기 전에 전부 보유하고 있어야 하는 카드들. 없으면 null 또는 빈 배열</summary>
        public string[] Requires;

        /// <summary>이미 보유 중이면 이 카드를 제시하지 않는 카드 목록</summary>
        public string[] Excludes;

        public int MaxPicks;
    }
}
