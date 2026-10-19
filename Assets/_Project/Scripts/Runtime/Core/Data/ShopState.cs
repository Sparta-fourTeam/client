using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>오늘 일일상점의 상태. 다시 뽑은 횟수의 합이 오늘 물품 구성을 정하는 값이라 같은 날 다시 켜도 같은 구성이 나오고,
    /// 다시 뽑으면 산 칸 기록이 비워진다</summary>
    [Serializable]
    public class ShopState
    {
        public int adUsed;
        public int goldUsed;
        /// <summary>지금 구성에서 이미 산 칸의 번호(0부터)</summary>
        public List<int> purchased = new();
        /// <summary>서버가 확정한 진열. null이면 Local 규칙으로 구성한다.</summary>
        public List<ShopOfferPrice> offers;
        public string nextResetAt;
    }

    [Serializable]
    public class ShopOfferPrice
    {
        public int slot;
        public int price;
    }
}
