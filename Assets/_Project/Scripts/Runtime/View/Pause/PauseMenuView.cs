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

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<StageStateChanged> stateChanged)
        {
            _stageManager = stageManager;
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _resumeButton.onClick.AddListener(() => _stageManager.Resume());
            // 포기하기는 판정 없이 결과 전송으로 간다 (docs/flows.md).
            // Forfeit은 Paused에서만 동작하고 바로 Submitting으로 넘어가므로 연타해도 한 번만 전송된다
            _abandonButton.onClick.AddListener(() => _stageManager.Forfeit());
        }

        private void OnStateChanged(StageStateChanged message)
        {
            _panel.SetActive(message.State == StageState.Paused);
        }
    }
}
