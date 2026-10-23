using System;
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
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _adButton;
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private TMP_Text _adTitle;
        [SerializeField] private TMP_Text _purchaseTitle;
        private EnergyRecovery _energyRecovery;
        private PlayerProfile _profile;
        private bool _busy;
        private int _version;
        private EnergyPurchaseState _expiredRefreshAttempt;
        private TMP_Text _purchaseRemaining, _description;
        private GameObject _adRemaining;
        private TMP_Text _purchaseLabel;

        [Inject]
        public void Construct(EnergyRecovery energyRecovery, PlayerProfile profile, ISubscriber<LobbyRequestFailed> requestFailed)
        {
            _energyRecovery = energyRecovery;
            _profile = profile;
            Track(requestFailed.Subscribe(m => Debug.LogWarning($"[EnergyRecover] 회복 실패: {m.Code}")));
        }
        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _purchaseLabel = _purchaseButton.GetComponentInChildren<TMP_Text>(true);
            _purchaseRemaining = _purchaseTitle.transform.parent.Find("RemainingText")?.GetComponent<TMP_Text>();
            _adRemaining = _adTitle.transform.parent.Find("RemainingText")?.gameObject;
            _description = _panel.transform.Find("Window/DescText")?.GetComponent<TMP_Text>();
            _closeButton.onClick.AddListener(() => { _version++; PopupPanel.Set(_panel, _transition, false); });
            _adButton.interactable = false;
            _purchaseButton.onClick.AddListener(() => RecoverAsync().Forget());
        }
        public void Open()
        {
            _version++;
            _expiredRefreshAttempt = null;
            _adTitle.text = L10n.Get("energy.recover.inkPenAmount", _profile.EnergyConfig.AdRecoverAmount);
            _purchaseTitle.text = L10n.Get("energy.recover.inkPenAmount", _profile.EnergyConfig.PurchaseRecoverAmount);
            PopupPanel.Set(_panel, _transition, true);
            if (_energyRecovery.UsesServerPurchase) { RefreshAsync(_version).Forget(); }
            else { Render(); }
        }
        private void OnApplicationFocus(bool focused)
        {
            if (focused && _energyRecovery != null && _energyRecovery.UsesServerPurchase && _panel.activeSelf)
            { RefreshAsync(_version).Forget(); }
        }
        private void Update()
        {
            if (_panel == null || !_panel.activeSelf || _energyRecovery == null || !_energyRecovery.UsesServerPurchase) { return; }
            Render();
            var state = _energyRecovery.PurchaseState;
            if (!_busy && state is { RemainingSeconds: <= 0 } && !ReferenceEquals(_expiredRefreshAttempt, state))
            { _expiredRefreshAttempt = state; RefreshAsync(_version).Forget(); }
        }
        private async UniTask RefreshAsync(int version)
        {
            if (_busy) { return; }
            _busy = true; Render();
            try { await _energyRecovery.RefreshPurchaseState(); }
            finally
            {
                _busy = false;
                if (this != null && version == _version) { Render(); }
            }
        }
        private async UniTask RecoverAsync()
        {
            if (_busy) { return; }
            if (_energyRecovery.UsesServerPurchase && !_energyRecovery.HasPendingPurchase && (_energyRecovery.PurchaseState == null || _energyRecovery.PurchaseState.RemainingSeconds <= 0))
            { await RefreshAsync(_version); return; }
            _busy = true; Render();
            int version = _version;
            bool ok;
            try { ok = await _energyRecovery.Recover(EnergySource.Purchase); }
            finally
            {
                _busy = false;
                if (this != null && version == _version) { Render(); }
            }
            if (ok && this != null && version == _version) { PopupPanel.Set(_panel, _transition, false); }
        }
        private void Render()
        {
            if (!_energyRecovery.UsesServerPurchase)
            {
                _purchaseButton.interactable = !_busy;
                if (_purchaseLabel != null) { _purchaseLabel.text = L10n.Get("energy.recover.gem"); }
                return;
            }
            var state = _energyRecovery.PurchaseState;
            bool pending = _energyRecovery.HasPendingPurchase;
            _purchaseButton.interactable = !_busy && (pending || state == null
                || state.RemainingSeconds <= 0 || state.RemainingPurchases > 0 && _profile.Gems >= state.GemCost);
            _purchaseTitle.text = state != null ? L10n.Get("energy.recover.inkPenAmount", state.EnergyGranted) : L10n.Get("energy.recover.purchaseTitle");
            if (_adRemaining != null) { _adRemaining.SetActive(false); }
            if (_purchaseRemaining != null)
            { _purchaseRemaining.text = state != null ? L10n.Get("common.remainingCount", $"{state.RemainingPurchases}/{state.DailyLimit}") : L10n.Get("energy.recover.checkingCount"); }
            var remaining = TimeSpan.FromSeconds(Math.Ceiling(state?.RemainingSeconds ?? 0));
            string countdown = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            if (_description != null)
            { _description.text = state != null ? L10n.Get("energy.recover.resetIn", countdown) : pending ? L10n.Get("energy.recover.confirmingSame") : L10n.Get("energy.recover.recheck"); }
            string label = _busy ? L10n.Get("common.checking") : pending ? L10n.Get("energy.recover.checkResult") : state == null || state.RemainingSeconds <= 0 ? L10n.Get("energy.recover.checkInfo")
                : L10n.Get("energy.recover.gemCost", state.GemCost);
            if (_purchaseLabel != null) { _purchaseLabel.text = label; }
        }
        protected override void DisposeView() { _version++; }
    }
}
