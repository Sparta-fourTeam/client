using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public class EnergyRecoverPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _adButton;
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private TMP_Text _adTitle;
        [SerializeField] private TMP_Text _purchaseTitle;

        private EnergyRecovery _energyRecovery;
        private PlayerProfile _profile;
        private bool _busy;

        [Inject]
        public void Construct(EnergyRecovery energyRecovery, PlayerProfile profile, ISubscriber<LobbyRequestFailed> requestFailed)
        {
            _energyRecovery = energyRecovery;
            _profile = profile;
            Track(requestFailed.Subscribe(m => Debug.LogWarning($"[EnergyRecover] 회복 실패: {m.Code}")));
        }
        private void Awake()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(() => _panel.SetActive(false));
            _adButton.onClick.AddListener(() => RecoverAsync(EnergySource.Ad).Forget());
            _purchaseButton.onClick.AddListener(() => RecoverAsync(EnergySource.Purchase).Forget());
        }

        public void Open()
        {
            _adTitle.text = $"에너지 x{_profile.EnergyConfig.AdRecoverAmount}";
            _purchaseTitle.text = $"에너지 x{_profile.EnergyConfig.PurchaseRecoverAmount}";
            _panel.SetActive(true);
        }

        // 연타로 두 번 지급되지 않게 막고, 성공하면 닫는다
        private async UniTask RecoverAsync(EnergySource source)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            SetButtons(false);
            bool ok = await _energyRecovery.Recover(source);
            _busy = false;
            SetButtons(true);

            if (ok)
            {
                _panel.SetActive(false);
            }
        }
        private void SetButtons(bool interactable)
        {
            _adButton.interactable = interactable;
            _purchaseButton.interactable = interactable;
        }
    }
}
