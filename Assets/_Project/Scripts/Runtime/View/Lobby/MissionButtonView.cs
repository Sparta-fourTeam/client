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
    public class MissionButtonView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _missionScreen = registry.Get<MissionScreenView>();
        }

        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _stageText;
        private MissionScreenView _missionScreen;   // 씬의 MissionScreen을 연결

        [Inject]
        public void Construct(IBufferedSubscriber<ProgressChanged> progressChanged, LobbyProfileRefresher refresher)
        {
            Track(progressChanged.Subscribe(OnProgressChanged));
            EnableWhenLoaded(refresher).Forget();
        }

        protected override void InitializeView()
        {
            // 전투에서 돌아온 직후엔 해금 정보가 이전 값이라, 프로필을 다시 받을 때까지 막는다
            _button.interactable = false;
            _button.onClick.AddListener(() => _missionScreen.Open());
        }

        private async UniTaskVoid EnableWhenLoaded(LobbyProfileRefresher refresher)
        {
            await refresher.WhenLoaded;
            if (this != null)
            {
                _button.interactable = true;
            }
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
