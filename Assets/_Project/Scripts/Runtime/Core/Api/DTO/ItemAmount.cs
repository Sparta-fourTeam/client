using System;

namespace Game.Core
{
    /// <summary>강화 비용, 지급 수량, 소유량에서 공통으로 사용하는 절대 수량. 차감은 음수 대신 소비 동작으로 표현한다.</summary>
    [Serializable]
    public sealed class ItemAmount
    {
        public string itemId;
        public int quantity;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity < 0)
            {
                throw new InvalidOperationException("아이템 ID는 비어 있을 수 없고 수량은 0 이상이어야 합니다.");
            }
        }
    }
}
