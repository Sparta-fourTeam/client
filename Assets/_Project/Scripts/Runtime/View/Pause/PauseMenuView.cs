using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class PauseMenuView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _abandonButton;

        private StageManager _stageManager;
        private ISceneNavigator _navigator;

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<StageStateChanged> stateChanged,
            ISceneNavigator navigator)
        {
            _stageManager = stageManager;
            _navigator = navigator;
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _resumeButton.onClick.AddListener(() => _stageManager.Resume());
            _abandonButton.onClick.AddListener(OnAbandonClicked);
        }

        private void OnStateChanged(StageStateChanged message)
        {
            _panel.SetActive(message.State == StageState.Paused);
        }

        // TODO: 결과 전송(#32)이 생기면 "판정 없이 결과 전송"으로 바꾼다 (docs/flows.md)
        // timeScale은 씬을 나갈 때 StageManager.Dispose에서 1로 돌아온다
        private void OnAbandonClicked()
        {
            // 연타로 씬 전환이 두 번 일어나지 않게 막는다
            _abandonButton.interactable = false;
            _resumeButton.interactable = false;
            _navigator.GoToLobby().Forget(Debug.LogException);
        }
    }
}
