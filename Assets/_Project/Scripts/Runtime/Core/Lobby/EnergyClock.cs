using System;
using Game.Core.Messages;
using MessagePipe;
using VContainer.Unity;

namespace Game.Core
{
    public interface IUtcClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemUtcClock : IUtcClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class EnergyClock : ITickable
    {
        private readonly PlayerProfile _profile;
        private readonly IUtcClock _clock;
        private readonly IEnergyApi _energyApi;
        private readonly IBufferedPublisher<EnergyChanged> _energyChangedPublisher;
        private readonly IPublisher<LobbyRequestFailed> _requestFailedPublisher;

        private int _lastCurrent = -1;
        private int _lastMax = -1;

        public EnergyClock(
            PlayerProfile profile,
            IUtcClock clock,
            IBufferedPublisher<EnergyChanged> energyChangedPublisher)
        {
            _profile = profile;
            _clock = clock;
            _energyChangedPublisher = energyChangedPublisher;
        }

        public void Tick()
        {
            EnergyConfig config = _profile.EnergyConfig;
            (int current, float _) = EnergyRule.At(_profile.EnergyStored, _profile.EnergyUpdatedAt, _clock.UtcNow, config);

            if (current == _lastCurrent && config.Max == _lastMax)
            {
                return;
            }

            _lastCurrent = current;
            _lastMax = config.Max;
            _energyChangedPublisher.Publish(new EnergyChanged(current, config.Max));
        }

    }
}
