using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public sealed class EnergyView : HudView
    {
        [SerializeField] private TMP_Text _value;

        [Inject]
        public void Construct(IBufferedSubscriber<EnergyChanged> energyChangedSubscriber)
        {
            Track(energyChangedSubscriber.Subscribe(OnEnergyChanged));
        }

        private void OnEnergyChanged(EnergyChanged message)
        {
            if (message.Max <= 0)
            {
                return;
            }

            _value.text = $"{message.Current}/{message.Max}";
        }
    }
}
