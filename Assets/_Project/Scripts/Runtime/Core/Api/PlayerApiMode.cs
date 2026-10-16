namespace Game.Core
{
    /// <summary>한 Root 수명 동안 고정한다. 서버 테스트에서는 강화·별 보상 외 진행·튜토리얼·재화 변경을 사용하지 않는다.</summary>
    public sealed class PlayerApiMode
    {
        public bool ServerUpgradeTest { get; }
        public PlayerApiMode(bool serverUpgradeTest = false) => ServerUpgradeTest = serverUpgradeTest;
    }

    /// <summary>서버 테스트 구매의 메모리 내 복구. 다른 구매를 시작하지 않고 보존한 동일 요청을 복구한다.</summary>
    public interface IUpgradeRecovery
    {
        bool HasPendingPurchase { get; }
        string PendingUpgradeId { get; }
        Cysharp.Threading.Tasks.UniTask<PlayerSnapshot> RecoverPurchase();
    }

    /// <summary>별 보상 POST를 다시 보내지 않고 최초 목표의 서버 수령 상태만 확인한다. 메모리 수명이다.</summary>
    public interface IStageRewardRecovery
    {
        bool HasPendingStageReward { get; }
        int PendingStageId { get; }
        int PendingTargetRating { get; }
        Cysharp.Threading.Tasks.UniTask<PlayerSnapshot> RecoverStageReward();
    }
}
