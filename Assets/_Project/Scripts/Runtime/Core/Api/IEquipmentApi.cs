using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>장비 강화를 담당하는 API. 장비에는 장착·해제가 없고 강화만 있다</summary>
    public interface IEquipmentApi
    {
        /// <summary>장비를 한 단계 강화하고 반영된 플레이어 스냅샷을 받는다. idempotencyKey로 중복 요청을 막는다.
        /// 거절 코드: LOCKED(해금 전), ALREADY_MAX, INSUFFICIENT_GOLD, INSUFFICIENT_ITEM</summary>
        UniTask<PlayerSnapshot> Upgrade(string equipmentId, string idempotencyKey);
    }
}
