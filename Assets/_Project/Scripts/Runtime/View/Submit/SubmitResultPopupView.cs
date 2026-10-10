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
    /// <summary>결과 제출이 거절되거나(SubmitRejected) 네트워크 등으로 실패했을 때(SubmitFailed) 이유를 보여준다.
    /// 거절은 확인을 누르면 로비로 보낸다 (docs/flows.md: 거절 안내 후 Lobby).
    /// 실패 중 다시 보낼 수 있는 것은(Retryable) 재시도 버튼도 보여주고, 누르면 같은 요청을 다시 보낸다.
    /// StageManager는 실패 때 씬을 이동하지 않는다. 이동하면 이 안내가 보이기 전에 Stage 씬이 사라지기 때문이다.
    /// 재시도가 시작되면 StageManager가 Submitting을 다시 발행하므로, 그때 이 안내를 닫는다</summary>
    public sealed class SubmitResultPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _retryButton;

        private ISceneNavigator _navigator;
        private StageManager _stageManager;

        [Inject]
        public void Construct(
            ISubscriber<SubmitRejected> submitRejected,
            ISubscriber<SubmitFailed> submitFailed,
            IBufferedSubscriber<StageStateChanged> stateChanged,
            ISceneNavigator navigator,
            StageManager stageManager)
        {
            _navigator = navigator;
            _stageManager = stageManager;
            Track(submitRejected.Subscribe(OnSubmitRejected));
            Track(submitFailed.Subscribe(OnSubmitFailed));
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _confirmButton.onClick.AddListener(OnConfirmClicked);
            _retryButton.onClick.AddListener(OnRetryClicked);
        }

        private void OnSubmitRejected(SubmitRejected message)
        {
            Show(SubmitResultMessages.ForRejected(message.Code), retryable: false);
        }

        private void OnSubmitFailed(SubmitFailed message)
        {
            Show(SubmitResultMessages.ForFailed(message.Retryable), message.Retryable);
        }

        private void OnStateChanged(StageStateChanged message)
        {
            if (message.State == StageState.Submitting)
            {
                _panel.SetActive(false);
            }
        }

        private void Show(string message, bool retryable)
        {
            _messageText.text = message;
            _confirmButton.interactable = true;
            _retryButton.gameObject.SetActive(retryable);
            _panel.SetActive(true);
        }

        private void OnRetryClicked()
        {
            // 다시 보내기가 시작되면 Submitting이 다시 발행되어 이 안내가 닫힌다. 연타는 StageManager가 막는다
            _stageManager.RetrySubmit();
        }

        private void OnConfirmClicked()
        {
            // 연타로 씬 전환이 두 번 일어나지 않게 막는다
            _confirmButton.interactable = false;
            _navigator.GoToLobby().Forget(Debug.LogException);
        }
    }
}
