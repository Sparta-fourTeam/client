using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>서버 강화의 저장된 같은 createId를 복구한다.</summary>
    public interface IUpgradeRecovery
    {
        bool HasPendingPurchase { get; }
        string PendingUpgradeId { get; }
        UniTask<PlayerSnapshot> RecoverPurchase();
    }
}
