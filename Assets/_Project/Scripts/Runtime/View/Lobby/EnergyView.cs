using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.View
{
    public sealed class EnergyView : HudView, IUiBindable
    {
        [SerializeField] private TMP_Text _value;
        [SerializeField] private UnityEngine.UI.Button _button;

        private EnergyRecoverPopupView _recoverPopup;

        public void BindUi(IUiRegistry registry)
        {
            _recoverPopup = registry.Get<EnergyRecoverPopupView>();
        }

        protected override void InitializeView()
        {
            _button.onClick.AddListener(OpenRecovery);
        }

        private void OpenRecovery() => _recoverPopup.Open();

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

        protected override void DisposeView()
        {
            _button.onClick.RemoveListener(OpenRecovery);
            base.DisposeView();
        }
    }
}
