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
    public sealed class ResultPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private TMP_Text _killsText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private Button _lobbyButton;

        private BattleStats _stats;
        private StageContext _stageContext;
        private ISceneNavigator _navigator;

        [Inject]
        public void Construct(ISubscriber<StageEnded> stageEnded, BattleStats stats, StageContext stageContext, ISceneNavigator navigator)
        {
            _stats = stats;
            _stageContext = stageContext;
            _navigator = navigator;
            Track(stageEnded.Subscribe(OnStageEnded));
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _lobbyButton.onClick.AddListener(OnLobbyClicked);
        }

        private void OnStageEnded(StageEnded message)
        {
            _titleText.text = message.Outcome == StageOutcome.Clear ? "클리어" : "실패";
            _stageText.text = _stageContext.StageId > 0 ? $"Stage {_stageContext.StageId}" : "-";
            _killsText.text = $"처치 {_stats.Kills}";
            _waveText.text = _stats.ReachedWave > 0 ? $"WAVE {_stats.ReachedWave}" : "-";
            _timeText.text = FormatTime(_stats.PlayTime);
            _panel.SetActive(true);
        }

        private void OnLobbyClicked()
        {
            // 연타로 씬 전환이 두 번 일어나지 않게 막는다
            _lobbyButton.interactable = false;
            _navigator.GoToLobby().Forget(Debug.LogException);
        }

        // 초를 "mm:ss"로 바꾼다
        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
