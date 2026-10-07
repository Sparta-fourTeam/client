using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>업그레이드 구매를 담당하는 API</summary>
    public interface IUpgradeApi
    {
        /// <summary>업그레이드를 구매하고 반영된 플레이어 스냅샷을 받는다. idempotencyKey로 중복 요청을 막는다.
        /// 거절 코드: LOCKED(플레이어 레벨 부족), ALREADY_MAX, INSUFFICIENT_GOLD, INSUFFICIENT_ITEM(재료 부족)</summary>
        UniTask<PlayerSnapshot> Purchase(string upgradeId, string idempotencyKey);
    }
}
