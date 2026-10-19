using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>로그인한 계정의 미확정 서버 거래를 같은 요청으로 먼저 복구한다.</summary>
    public interface IAccountPendingRecovery
    {
        UniTask Recover();
    }

    public sealed class NoAccountPendingRecovery : IAccountPendingRecovery
    {
        public UniTask Recover() => UniTask.CompletedTask;
    }
}
