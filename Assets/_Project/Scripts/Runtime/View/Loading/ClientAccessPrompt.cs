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
                Title = "서버 상태 확인 중",
                Message = "서버 상태를 확인하고 있습니다. 잠시만 기다려 주세요.",
                ConfirmLabel = "확인 중",
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
                    ClientAccessState.Advisory => "새 버전 안내",
                    ClientAccessState.UpdateRequired => "업데이트가 필요합니다",
                    ClientAccessState.Unavailable => "상태 확인 실패",
                    _ => "서버 점검 안내",
                },
                Message = !string.IsNullOrWhiteSpace(decision.Message) ? decision.Message
                    : update ? "새로운 버전으로 업데이트해 주세요." : "서버 점검 중입니다. 잠시 후 다시 확인해 주세요.",
                ConfirmLabel = update && !string.IsNullOrEmpty(decision.StoreUrl) ? "업데이트" : "다시 확인",
                CancelLabel = decision.Blocking ? null : "나중에",
                CannotDismiss = decision.Blocking,
            };
            if (!string.IsNullOrEmpty(decision.EstimatedEnd)) { request.Message += "\n예상 종료: " + decision.EstimatedEnd; }
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
