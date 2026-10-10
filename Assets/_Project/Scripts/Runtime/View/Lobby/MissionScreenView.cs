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
    public class MissionScreenView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _recoverPopup = registry.Get<EnergyRecoverPopupView>();
            _rewardPopup = registry.Get<RewardPopupView>();
            _detailsPopup = registry.Get<MissionPopupView>();
            _rewardInfoPopup = registry.Get<MissionRewardInfoPopupView>();
        }

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private GameObject _lockOverlay;
        [SerializeField] private Button _prevButton, _nextButton, _backButton, _enterButton;
        [SerializeField] private TMP_Text _costText;
        [SerializeField] private Image _rewardFill;             // RewardTrack/Bar/Fill
        private EnergyRecoverPopupView _recoverPopup;
        [SerializeField] private MissionRewardNodeView[] _rewardNodes;
        [SerializeField] private Button _rewardAreaButton;
        [SerializeField] private Button _infoButton;
        [SerializeField] private GameObject _rewardBadge;
        [SerializeField] private TMP_Text _rewardStatus;
        private MissionPopupView _detailsPopup;
        private RewardPopupView _rewardPopup;
        private MissionRewardInfoPopupView _rewardInfoPopup;
        [SerializeField] private ItemIconTable _itemIcons;

        private PlayerProfile _profile;
        private GameDataStore _data;
        private BattleLauncher _launcher;
        private IStageApi _stageApi;
        private int _stageId = 1;
        private int _energy;
        private bool _launching;
        private bool _claiming;

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data, BattleLauncher launcher,
            IBufferedSubscriber<EnergyChanged> energyChanged, ISubscriber<StartFailed> startFailed, IStageApi stageApi)
        {
            _profile = profile;
            _data = data;
            _launcher = launcher;
            _stageApi = stageApi;
            _profile.Changed += OnProfileChanged;
            Track(energyChanged.Subscribe(m => _energy = m.Current));
            Track(startFailed.Subscribe(OnStartFailed));
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _prevButton.onClick.AddListener(() => Move(-1));
            _nextButton.onClick.AddListener(() => Move(+1));
            _backButton.onClick.AddListener(Close);
            _enterButton.onClick.AddListener(OnEnterClicked);
            _infoButton.onClick.AddListener(() => _detailsPopup.ShowDetails(_data, _stageId));
            _rewardAreaButton.onClick.AddListener(() => ClaimRewardsAsync().Forget());
            for (int i = 0; i < _rewardNodes.Length; i++)
            {
                int rating = i + 1;
                _rewardNodes[i].Button.onClick.AddListener(() => OnRewardClicked(rating));
            }
        }

        public void Open()
        {
            if (_claiming || _launching) { return; }
            _stageId = _profile.HighestUnlockedStage;
            Refresh();
            _panel.SetActive(true);
        }

        private void Move(int delta)
        {
            if (_claiming || _launching) { return; }
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
            int claimed = _profile.ClaimedRating(_stageId);
            bool busy = _claiming || _launching;

            _stageText.text = $"제 {_stageId} 관문";
            _lockOverlay.SetActive(!unlocked);
            _enterButton.interactable = unlocked && !busy;
            _prevButton.interactable = !busy && _data.Stages.Contains(_stageId - 1);
            _nextButton.interactable = !busy && _data.Stages.Contains(_stageId + 1);
            _backButton.interactable = !busy;
            _infoButton.interactable = !busy;
            _costText.text = $"에너지 x{_profile.EnergyConfig.Cost}";

            for (int i = 0; i < _rewardNodes.Length; i++)
            {
                _rewardNodes[i].Show(rating > i, claimed > i, busy);
            }
            _rewardFill.fillAmount = rating <= 1 ? 0f : (rating - 1) / 2f;
            bool available = _profile.HasUnclaimedReward(_stageId);
            _rewardAreaButton.gameObject.SetActive(available);
            _rewardAreaButton.interactable = !busy;
            _rewardBadge.SetActive(available);
            _rewardStatus.text = _claiming ? "보상을 받는 중이에요..."
                : available ? "상자 영역을 눌러 보상을 모두 받으세요"
                : claimed >= 3 ? "모든 보상을 받았어요. 상자를 눌러 정보를 확인하세요"
                : !unlocked ? "이전 관문을 클리어하면 열려요"
                : "상자를 눌러 달성 조건과 보상을 확인하세요";
        }

        private void OnProfileChanged(PlayerProfile profile)
        {
            if (_panel.activeSelf) { Refresh(); }
        }

        private void Close()
        {
            if (_claiming || _launching) { return; }
            _detailsPopup.Close();
            _rewardPopup.Close();
            _rewardInfoPopup.Close();
            _panel.SetActive(false);
        }

        private async UniTask ClaimRewardsAsync()
        {
            if (_claiming || _launching || !_profile.HasUnclaimedReward(_stageId)) { return; }
            _claiming = true;
            Refresh();
            int stageId = _stageId;
            var before = _profile.Snapshot();
            try
            {
                var snapshot = await _stageApi.ClaimRatingReward(stageId);
                var rows = RewardRows.Gained(_itemIcons, _data, before, snapshot);
                _profile.Apply(snapshot);
                if (this != null) { _rewardPopup.Show(rows, $"제 {stageId} 관문의 미수령 보상을 모두 받았어요."); }
            }
            catch (ApiException error)
            {
                if (this != null)
                {
                    _rewardStatus.text = error.Code == "NO_REWARD" ? "이미 수령한 보상이에요"
                        : "보상을 받지 못했어요. 상자 영역을 눌러 다시 시도하세요.";
                }
            }
            finally
            {
                _claiming = false;
                if (this != null)
                {
                    string status = _rewardStatus.text;
                    Refresh();
                    if (_profile.HasUnclaimedReward(stageId)) { _rewardStatus.text = status; }
                }
            }
        }

        private void OnRewardClicked(int rating)
        {
            if (_claiming || _launching || _profile.HasUnclaimedReward(_stageId)) { return; }
            _rewardInfoPopup.Show(_data, _stageId, rating, _profile.ClaimedRating(_stageId) >= rating);
        }

        private void OnEnterClicked()
        {
            if (_launching || _claiming || !_profile.IsStageUnlocked(_stageId)) { return; }
            if (_energy < _profile.EnergyConfig.Cost)
            {
                _recoverPopup.Open();
                return;
            }

            // 연타를 막고, 거절 메시지와 비동기 예외 모두에서 입력을 복구한다.
            _launching = true;
            Refresh();
            _launcher.Launch(_stageId).Forget(OnLaunchException);
        }

        private void OnLaunchException(System.Exception error)
        {
            if (this == null) { return; }
            _launching = false;
            Refresh();
            _rewardStatus.text = "관문에 입장하지 못했어요. 다시 시도하세요.";
            Debug.LogException(error);
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

        protected override void DisposeView()
        {
            if (_profile != null) { _profile.Changed -= OnProfileChanged; }
            base.DisposeView();
        }
    }
}
