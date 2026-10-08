using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>스테이지가 20웨이브를 모두 끝냈을 때 주는 아이템 하나의 총수량</summary>
    [Serializable]
    public sealed class StageRewardEntry
    {
        public string itemId;
        public int finalAmount;
    }

    /// <summary>스테이지별 보상 밸런스. 보상마다 "전체 웨이브를 끝냈을 때의 총수량"만 적는다.
    /// 시작 수량·웨이브당 수량·중간 보너스 시점은 모든 스테이지가 같아 StageRewardSchedule이 정하고,
    /// 중간 보너스 수량은 총수량에서 계산한다 (총수량 − 시작 − 웨이브수 × 웨이브당)</summary>
    [Serializable]
    public sealed class StageRewardBalance
    {
        public int StageId;
        public int BaseExp, ThreeStarBonusExp;
        public int FinalCoin;

        /// <summary>코인을 뺀 아이템 보상. 랜덤 재료도 같은 ID 필드로 표현한다. 새 아이템 보상은 항목만 추가한다</summary>
        public List<StageRewardEntry> Items = new();

        public bool UsesRandomSkillMaterial => HasPositive(ItemIds.RandomSkillMaterial);
        public bool UsesRandomEquipmentMaterial => HasPositive(ItemIds.RandomEquipmentMaterial);

        private bool HasPositive(string itemId) => Items.Exists(item => item.itemId == itemId && item.finalAmount > 0);

        public void Validate()
        {
            if (StageId <= 0 || BaseExp < 0 || ThreeStarBonusExp < 0)
            { throw new InvalidOperationException($"StageRewards {StageId}: 보상 데이터가 잘못되었습니다."); }
            RequireReachable("코인", StageRewardSchedule.Coin, FinalCoin);
            if (Items == null) { throw new InvalidOperationException($"StageRewards {StageId}: 아이템 보상 목록이 필요합니다."); }
            var ids = new HashSet<string>();
            foreach (var item in Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.itemId) || !ids.Add(item.itemId))
                { throw new InvalidOperationException($"StageRewards {StageId}: 아이템 보상은 중복 없는 ID로 적습니다."); }
                if (!StageRewardSchedule.TryGetItem(item.itemId, out var schedule))
                { throw new InvalidOperationException($"StageRewards {StageId}: {item.itemId}의 지급 규칙이 없습니다. StageRewardSchedule에 추가하세요."); }
                RequireReachable(item.itemId, schedule, item.finalAmount);
            }
        }

        /// <summary>총수량이 기본 누적(시작 + 웨이브당 × 웨이브수)보다 작으면 중간 보너스가 음수가 된다</summary>
        private void RequireReachable(string what, RewardSchedule schedule, int finalAmount)
        {
            if (finalAmount < schedule.MinimumFinal)
            { throw new InvalidOperationException($"StageRewards {StageId}: {what} 총수량 {finalAmount}이 기본 누적 {schedule.MinimumFinal}보다 작습니다."); }
        }
    }
}
