using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>플레이어 상태 조회를 담당하는 API</summary>
    public interface IPlayerApi
    {
        /// <summary>현재 플레이어 상태 전체 스냅샷을 받는다</summary>
        UniTask<PlayerSnapshot> GetMe();
    }
}
