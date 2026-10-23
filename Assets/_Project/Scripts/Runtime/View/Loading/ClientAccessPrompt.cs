using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.View
{
    /// <summary>현재 씬의 기존 확인 팝업을 사용한다. 재검사나 취소가 다른 요청의 팝업을 닫지 않는다.</summary>
    public sealed class ClientAccessPrompt : IClientAccessPrompt
    {
        private ConfirmPopupView _popup;
        private ConfirmRequest _request;
        private ConfirmPopupView _checkingPopup;
        private ConfirmRequest _checkingRequest;

        public void SetChecking(bool checking)
        {
            if (!checking)
            {
                if (_checkingPopup != null && _checkingRequest != null) { _checkingPopup.Cancel(_checkingRequest); }
                _checkingRequest = null; _checkingPopup = null;
                return;
            }
            if (_checkingPopup != null && _checkingRequest != null) { return; }
            _checkingPopup = UnityEngine.Object.FindAnyObjectByType<ConfirmPopupView>(FindObjectsInactive.Include);
            if (_checkingPopup == null) { throw new InvalidOperationException("Client policy popup is unavailable."); }
            _checkingRequest = new ConfirmRequest
            {
                Title = L10n.Get("access.checkingTitle"),
                Message = L10n.Get("access.checkingMessage"),
                ConfirmLabel = L10n.Get("access.checkingConfirm"),
                CannotDismiss = true,
                ConfirmEnabled = false,
            };
            _checkingPopup.ShowAsync(_checkingRequest).Forget();
        }

        public async UniTask<bool> Show(ClientAccessDecision decision, CancellationToken cancellationToken)
        {
            SetChecking(false);
            _popup = UnityEngine.Object.FindAnyObjectByType<ConfirmPopupView>(FindObjectsInactive.Include);
            if (_popup == null) { throw new InvalidOperationException("Client policy popup is unavailable."); }
            bool update = decision.State is ClientAccessState.Advisory or ClientAccessState.UpdateRequired;
            var request = new ConfirmRequest
            {
                Title = decision.State switch
                {
                    ClientAccessState.Advisory => L10n.Get("access.advisoryTitle"),
                    ClientAccessState.UpdateRequired => L10n.Get("access.requiredTitle"),
                    ClientAccessState.Unavailable => L10n.Get("access.unavailableTitle"),
                    _ => L10n.Get("access.maintenanceTitle"),
                },
                Message = !string.IsNullOrWhiteSpace(decision.Message) ? decision.Message
                    : update ? L10n.Get("access.updateMessage") : L10n.Get("access.maintenanceMessage"),
                ConfirmLabel = update && !string.IsNullOrEmpty(decision.StoreUrl) ? L10n.Get("update.confirm") : L10n.Get("access.retry"),
                CancelLabel = decision.Blocking ? null : L10n.Get("update.later"),
                CannotDismiss = decision.Blocking,
            };
            if (!string.IsNullOrEmpty(decision.EstimatedEnd)) { request.Message += "\n" + L10n.Get("access.estimatedEnd", decision.EstimatedEnd); }
            _request = request;
            var popup = _popup;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, popup.GetCancellationTokenOnDestroy());
            try { return await popup.ShowAsync(request).AttachExternalCancellation(lifetime.Token); }
            finally
            {
                if (popup != null) { popup.Cancel(request); }
                if (ReferenceEquals(_request, request)) { _request = null; _popup = null; }
            }
        }

        public void OpenStore(string url) => Application.OpenURL(url);
        public void Recheck() { if (_popup != null && _request != null) { _popup.Cancel(_request); } }
    }
}
