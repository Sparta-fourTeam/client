using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>서버 계약을 아직 연결하지 않은 작업은 Local 잔액을 변경하지 않고 거절한다.</summary>
    public sealed class ServerUnavailableApi : IUpgradeApi, IEnergyApi, IStageApi, ICodexApi, IQuestApi, IShopApi, ITutorialApi
    {
        private static ApiException Unavailable() => new(ApiErrorKind.Rejected, "SERVER_PROGRESS_UNAVAILABLE");
        public UniTask<PlayerSnapshot> Purchase(string upgradeId, string idempotencyKey) => throw Unavailable();
        public UniTask<PlayerSnapshot> Recover(EnergySource source, string idempotencyKey = null) => throw Unavailable();
        public UniTask<PlayerSnapshot> ClaimRatingReward(int stageId) => throw Unavailable();
        public UniTask<PlayerSnapshot> ClaimMonsterReward(int monsterId) => throw Unavailable();
        public UniTask<PlayerSnapshot> ClaimQuest(string questId) => throw Unavailable();
        public UniTask<PlayerSnapshot> ClaimQuestChest(string chestId) => throw Unavailable();
        public UniTask<PlayerSnapshot> Reroll(ShopRerollKind kind, string idempotencyKey) => throw Unavailable();
        public UniTask<PlayerSnapshot> Buy(int slot, string idempotencyKey) => throw Unavailable();
        public UniTask<PlayerSnapshot> BuyGoldPack(int index, string idempotencyKey) => throw Unavailable();
        public UniTask<PlayerSnapshot> BuyGemPack(int index, string idempotencyKey) => throw Unavailable();
        public UniTask<TutorialCompleteResponse> Complete(string tutorialId) => throw Unavailable();
    }
}
