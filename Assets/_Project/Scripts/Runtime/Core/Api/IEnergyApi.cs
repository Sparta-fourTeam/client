using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>에너지 회복을 담당하는 API</summary>
    public interface IEnergyApi
    {
        /// <summary>에너지를 회복하고 반영된 플레이어 스냅샷을 받는다. 서버 구매는 idempotencyKey로 중복 지급을 막고, 키가 없으면 보류한 동일 거래를 복구하거나 새 키를 만든다</summary>
        UniTask<PlayerSnapshot> Recover(EnergySource source, string idempotencyKey = null);
    }
}
