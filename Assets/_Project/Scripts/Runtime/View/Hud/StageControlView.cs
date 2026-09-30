using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class StageControlView : HudView
    {
        [SerializeField] private Button _speedButton;
        [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private Button _pauseButton;

        private StageManager _stageManager;

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<GameSpeedChanged> speedChangedSubscriber,
            ISubscriber<StageEnded> stageEndedSubscriber)
        {
            _stageManager = stageManager;
            Track(speedChangedSubscriber.Subscribe(OnSpeedChanged));
            Track(stageEndedSubscriber.Subscribe(_ => OnStageEnded()));
        }

        private void Awake()
        {
            _speedButton.onClick.AddListener(() => _stageManager.CycleSpeed());
            _pauseButton.onClick.AddListener(() => _stageManager.Pause());
        }

        private void OnSpeedChanged(GameSpeedChanged message)
        {
            // Speed 0 = StageManager 초기화 전 (Buffered struct는 구독 즉시 기본값이 온다)
            if (message.Speed <= 0f)
            {
                return;
            }

            _speedLabel.text = $"{message.Speed:0.#}x";
        }

        private void OnStageEnded()
        {
            _speedButton.interactable = false;
            _pauseButton.interactable = false;
        }
    }
}
