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

        public bool UsesServerPurchase => _energyApi is IEnergyPurchaseApi;
        public EnergyPurchaseState PurchaseState => (_energyApi as IEnergyPurchaseApi)?.PurchaseState;
        public bool HasPendingPurchase => (_energyApi as IEnergyPurchaseApi)?.HasPendingPurchase ?? false;
        private bool _busy;

        public async UniTask<bool> RefreshPurchaseState()
        {
            if (_energyApi is not IEnergyPurchaseApi purchases) { return true; }
            if (_busy) { return false; }
            _busy = true;
            long generation = purchases.SessionGeneration;
            try
            {
                var snapshot = await purchases.RefreshPurchaseState();
                if (generation != purchases.SessionGeneration) { return false; }
                _profile.Apply(snapshot);
                return true;
            }
            catch (ApiException e)
            {
                if (generation == purchases.SessionGeneration) { _requestFailedPublisher.Publish(new LobbyRequestFailed(e.Code)); }
                return false;
            }
            finally { _busy = false; }
        }

        public async UniTask<bool> Recover(EnergySource source)
        {
            if (_busy) { return false; }
            _busy = true;
            var purchases = _energyApi as IEnergyPurchaseApi;
            long generation = purchases?.SessionGeneration ?? 0;
            try
            {
                // TODO(server): 광고 SDK 콜백의 거래 ID로 바꾼다. 지금은 요청마다 임시 키
                string key = source == EnergySource.Ad ? Guid.NewGuid().ToString() : null;
                PlayerSnapshot snapshot = await _energyApi.Recover(source, key);
                if (purchases != null && generation != purchases.SessionGeneration) { return false; }
                _profile.Apply(snapshot);
                return true;
            }
            catch (ApiException e)
            {
                if (purchases != null && generation != purchases.SessionGeneration) { return false; }
                _requestFailedPublisher.Publish(new LobbyRequestFailed(e.Code));
                return false;
            }
            finally { _busy = false; }
        }

    }
}
