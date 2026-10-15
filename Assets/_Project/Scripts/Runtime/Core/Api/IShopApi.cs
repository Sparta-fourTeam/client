using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>일일상점 다시 뽑기와 구매를 담당하는 API. 물품 구성은 날짜와 다시 뽑은 횟수로 정해지고(ShopRules.Roll), 서버는 횟수·산 칸·재화를 센다</summary>
    public interface IShopApi
    {
        // TODO(server): Ad는 광고 SDK의 완료 확인(adTxId)을 받아야 한다. Local은 확인 없이 횟수만 센다
        /// <summary>오늘 일일상점을 다시 뽑고 반영된 스냅샷을 받는다. Gold는 코인을 낸다. 다시 뽑으면 산 칸 기록이 비워진다. idempotencyKey로 중복 요청을 막는다.
        /// 거절 코드: NO_REROLL_LEFT(오늘 횟수 소진), INSUFFICIENT_GOLD</summary>
        UniTask<PlayerSnapshot> Reroll(ShopRerollKind kind, string idempotencyKey);

        /// <summary>진열된 칸의 물품을 잼으로 사서 보상을 받고 반영된 스냅샷을 받는다. idempotencyKey로 중복 요청을 막는다.
        /// 거절 코드: INVALID_SLOT, SOLD_OUT(이미 산 칸), INSUFFICIENT_GEMS</summary>
        UniTask<PlayerSnapshot> Buy(int slot, string idempotencyKey);

        /// <summary>코인 상점의 코인 묶음(ShopConfig.GoldPacks의 순서 번호)을 잼으로 사서 코인을 받고 반영된 스냅샷을 받는다. idempotencyKey로 중복 요청을 막는다.
        /// 거절 코드: INVALID_PACK, INSUFFICIENT_GEMS</summary>
        UniTask<PlayerSnapshot> BuyGoldPack(int index, string idempotencyKey);
    }
}
