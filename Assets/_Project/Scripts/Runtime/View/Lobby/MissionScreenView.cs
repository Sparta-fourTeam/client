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
    public class MissionScreenView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private Button _prevButton, _nextButton, _backButton, _enterButton;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private Image _rewardFill;             // RewardTrack/Bar/Fill
        [SerializeField] private GameObject[] _claimedMarks;    // RewardNode 3개의 Chest/Claimed
        [SerializeField] private EnergyRecoverPopupView _recoverPopup;

        private PlayerProfile _profile;
        private GameDataStore _data;
        private BattleLauncher _launcher;
        private int _stageId = 1;
        private int _energy;
        private bool _launching;

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data, BattleLauncher launcher,
            IBufferedSubscriber<EnergyChanged> energyChanged, ISubscriber<StartFailed> startFailed)
        {
            _profile = profile;
            _data = data;
            _launcher = launcher;
            Track(energyChanged.Subscribe(m => _energy = m.Current));
            Track(startFailed.Subscribe(OnStartFailed));
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _prevButton.onClick.AddListener(() => Move(-1));
            _nextButton.onClick.AddListener(() => Move(+1));
            _backButton.onClick.AddListener(() => _panel.SetActive(false));
            _enterButton.onClick.AddListener(OnEnterClicked);
        }

        public void Open()
        {
            _stageId = _profile.HighestUnlockedStage;
            Refresh();
            _panel.SetActive(true);
        }

        private void Move(int delta)
        {
            if (_data.Stages.Contains(_stageId + delta))
            {
                _stageId += delta;
                Refresh();
            }
        }

        private void Refresh()
        {
            bool unlocked = _profile.IsStageUnlocked(_stageId);
            int rating = _profile.ClearRating(_stageId);

            _stageText.text = $"Stage {_stageId}";
            _lockOverlay.SetActive(!unlocked);
            _enterButton.interactable = unlocked && !_launching;
            _prevButton.interactable = _data.Stages.Contains(_stageId - 1);
            _nextButton.interactable = _data.Stages.Contains(_stageId + 1);
            _costText.text = $"에너지 x{_profile.EnergyConfig.Cost}";

            // 보상 3단계는 표시만: 달성한 단계까지 "완료", 바는 1단계=0, 2단계=0.5, 3단계=1
            for (int i = 0; i < _claimedMarks.Length; i++)
            {
                _claimedMarks[i].SetActive(rating > i);
            }
            _rewardFill.fillAmount = rating <= 1 ? 0f : (rating - 1) / 2f;
        }

        private void OnEnterClicked()
        {
            if (_energy < _profile.EnergyConfig.Cost)
            {
                _recoverPopup.Open();
                return;
            }

            // 연타로 두 번 발급되지 않게 막는다. 성공하면 씬이 바뀌고, 실패하면 StartFailed에서 푼다
            _launching = true;
            _enterButton.interactable = false;
            _launcher.Launch(_stageId).Forget(Debug.LogException);
        }

        private void OnStartFailed(StartFailed message)
        {
            _launching = false;
            Refresh();
            if (message.Code == "INSUFFICIENT_ENERGY")
            {
                _recoverPopup.Open();
                return;
            }

            Debug.LogWarning($"[Mission] 입장 거절: {message.Code}");
        }
    }
}
