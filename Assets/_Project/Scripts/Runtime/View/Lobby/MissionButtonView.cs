using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public class MissionButtonView : HudView
    {
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private MissionScreenView _missionScreen;   // 씬의 MissionScreen을 연결

        [Inject]
        public void Construct(IBufferedSubscriber<ProgressChanged> progressChanged)
        {
            Track(progressChanged.Subscribe(OnProgressChanged));
        }

        private void Awake()
        {
            _button.onClick.AddListener(() => _missionScreen.Open());
        }

        private void OnProgressChanged(ProgressChanged message)
        {
            // 0 = 발행 전 기본값
            if (message.HighestUnlockedStage <= 0)
            {
                return;
            }

            _stageText.text = $"Stage {message.HighestUnlockedStage}";
        }
    }
}
