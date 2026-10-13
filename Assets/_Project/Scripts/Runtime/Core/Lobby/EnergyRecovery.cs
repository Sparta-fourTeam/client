using System;
using Cysharp.Threading.Tasks;
using Game.Core.Messages;
using MessagePipe;

namespace Game.Core
{
    public class EnergyRecovery
    {
        private readonly PlayerProfile _profile;
        private readonly IEnergyApi _energyApi;
        private readonly IPublisher<LobbyRequestFailed> _requestFailedPublisher;
        public EnergyRecovery(
            PlayerProfile profile,
            IEnergyApi energyApi,
            IPublisher<LobbyRequestFailed> requestFailedPublisher)
        {
            _profile = profile;
            _energyApi = energyApi;
            _requestFailedPublisher = requestFailedPublisher;
        }

        public async UniTask<bool> Recover(EnergySource source)
        {
            try
            {
                // TODO(server): 광고 SDK 콜백의 거래 ID로 바꾼다. 지금은 요청마다 임시 키
                string key = source == EnergySource.Ad ? Guid.NewGuid().ToString() : null;
                PlayerSnapshot snapshot = await _energyApi.Recover(source, key);
                _profile.Apply(snapshot);
                return true;
            }
            catch (ApiException e)
            {
                _requestFailedPublisher.Publish(new LobbyRequestFailed(e.Code));
                return false;
            }
        }

    }
}
