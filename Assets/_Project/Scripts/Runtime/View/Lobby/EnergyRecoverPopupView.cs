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
        private TMP_Text _purchaseLabel;
        private string _localLabel;

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
            _localLabel = _purchaseLabel != null ? _purchaseLabel.text : null;
            _closeButton.onClick.AddListener(() => { _version++; PopupPanel.Set(_panel, _transition, false); });
            _adButton.interactable = false;
            _purchaseButton.onClick.AddListener(() => RecoverAsync().Forget());
        }
        public void Open()
        {
            _version++;
            _expiredRefreshAttempt = null;
            _adTitle.text = $"잉크펜 x{_profile.EnergyConfig.AdRecoverAmount}";
            _purchaseTitle.text = $"잉크펜 x{_profile.EnergyConfig.PurchaseRecoverAmount}";
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
                if (_purchaseLabel != null) { _purchaseLabel.text = _localLabel; }
                return;
            }
            var state = _energyRecovery.PurchaseState;
            bool pending = _energyRecovery.HasPendingPurchase;
            _purchaseButton.interactable = !_busy && (pending || state == null
                || state.RemainingSeconds <= 0 || state.RemainingPurchases > 0 && _profile.Gems >= state.GemCost);
            if (state != null) { _purchaseTitle.text = $"잉크펜 x{state.EnergyGranted}"; }
            var remaining = TimeSpan.FromSeconds(Math.Ceiling(state?.RemainingSeconds ?? 0));
            string countdown = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            string label = _busy ? "확인 중" : pending ? "구매 결과 다시 확인" : state == null || state.RemainingSeconds <= 0 ? "구매 정보 다시 확인"
                : $"젬 {state.GemCost} · 남은 {state.RemainingPurchases}/{state.DailyLimit}\n초기화 {countdown}";
            if (_purchaseLabel != null) { _purchaseLabel.text = label; }
        }
        protected override void DisposeView() { _version++; }
    }
}
