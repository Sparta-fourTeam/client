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
    /// <summary>결과 제출이 거절되거나(SubmitRejected) 네트워크 등으로 실패했을 때(SubmitFailed) 이유를 보여주고,
    /// 확인을 누르면 로비로 보낸다 (docs/flows.md: 거절 안내 후 Lobby).
    /// StageManager는 실패 때 씬을 이동하지 않는다. 이동하면 이 안내가 보이기 전에 Stage 씬이 사라지기 때문이다.
    /// 이름은 거절 전용처럼 보이지만 실패도 같이 다룬다. 재시도 버튼이 생기면(#89) 실패 쪽을 분리하며 이름도 정리한다</summary>
    public sealed class SubmitRejectedView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private Button _confirmButton;

        private ISceneNavigator _navigator;

        [Inject]
        public void Construct(
            ISubscriber<SubmitRejected> submitRejected,
            ISubscriber<SubmitFailed> submitFailed,
            ISceneNavigator navigator)
        {
            _navigator = navigator;
            Track(submitRejected.Subscribe(OnSubmitRejected));
            Track(submitFailed.Subscribe(OnSubmitFailed));
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnSubmitRejected(SubmitRejected message)
        {
            Show(SubmitRejectedMessages.For(message.Code));
        }

        private void OnSubmitFailed(SubmitFailed message)
        {
            Show(SubmitRejectedMessages.ForFailure(message.Code));
        }

        private void Show(string message)
        {
            _messageText.text = message;
            _confirmButton.interactable = true;
            _panel.SetActive(true);
        }

        private void OnConfirmClicked()
        {
            // 연타로 씬 전환이 두 번 일어나지 않게 막는다
            _confirmButton.interactable = false;
            _navigator.GoToLobby().Forget(Debug.LogException);
        }
    }
}
