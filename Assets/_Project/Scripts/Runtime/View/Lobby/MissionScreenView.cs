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
        [Tooltip("가운데 관문 카드. 양옆 카드는 _carousel이 이 카드를 복제해 미리보기로 그린다")]
        [SerializeField] private StageCardView _card;
        [SerializeField] private StageCarouselView _carousel;
        [SerializeField] private Button _prevButton, _nextButton, _backButton, _enterButton;
        [SerializeField] private TMP_Text _costText;
        private EnergyRecoverPopupView _recoverPopup;
        private MissionPopupView _detailsPopup;
        private RewardPopupView _rewardPopup;
        private MissionRewardInfoPopupView _rewardInfoPopup;
        [SerializeField] private ItemIconTable _itemIcons;

        private PlayerProfile _profile;
        private GameDataStore _data;
        private BattleLauncher _launcher;
        private IStageApi _stageApi;
        private IStageRewardRecovery _serverClaim;
        private IUpgradeRecovery _upgradeRecovery;
        private GameSettings _settings;
        private int _stageId = 1;
        private int _energy;
        private bool _launching;
        private bool _claiming;
        private bool _hasSelection;
        private const string SelectedStageKey = "mission_selected_stage";

        [Inject]
        public void Construct(PlayerProfile profile, GameDataStore data, BattleLauncher launcher,
            IBufferedSubscriber<EnergyChanged> energyChanged, ISubscriber<StartFailed> startFailed, IStageApi stageApi,
            GameSettings settings = null)
        {
            _settings = settings;
            _profile = profile;
            _data = data;
            _launcher = launcher;
            _stageApi = stageApi;
            _serverClaim = stageApi as IStageRewardRecovery;
            _upgradeRecovery = stageApi as IUpgradeRecovery;
            _profile.Changed += OnProfileChanged;
            Track(energyChanged.Subscribe(m => _energy = m.Current));
            Track(startFailed.Subscribe(OnStartFailed));
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _prevButton.onClick.AddListener(() => _carousel.Slide(-1));
            _nextButton.onClick.AddListener(() => _carousel.Slide(+1));
            _backButton.onClick.AddListener(Close);
            _enterButton.onClick.AddListener(OnEnterClicked);
            _card.InfoButton.onClick.AddListener(() => _detailsPopup.ShowDetails(_data, _stageId));
            _card.RewardAreaButton.onClick.AddListener(() => ClaimRewardsAsync().Forget());
            for (int i = 0; i < _card.RewardNodes.Length; i++)
            {
                int rating = i + 1;
                _card.RewardNodes[i].Button.onClick.AddListener(() => OnRewardClicked(rating));
            }

            // 카드를 끌거나 화살표를 누르면 옆 관문으로 넘어간다. 입장·수령 중에는 넘기지 않는다
            _carousel.StageExists = IsReachable;
            _carousel.CanSlide = () => !_claiming && !_launching && _serverClaim?.HasPendingStageReward != true;
            _carousel.Moved = Move;
            _carousel.BindPreview = (card, id) => card.Show(id, _profile, busy: false, claiming: false);
        }

        public void Open()
        {
            if (_claiming || _launching) { return; }
            int selected = _hasSelection ? _stageId : PlayerPrefs.GetInt(SelectedStageKey, 0);
            if (_settings != null && _settings.AutoEnterHighestStage)
            {
                selected = _profile.HighestUnlockedStage;
            }
            else if (!IsReachable(selected))
            {
                // 처음 열면 마지막 클리어 관문을 보여 준다. 이후에는 보고 있던 관문을 유지한다.
                selected = 1;
                foreach (var stage in _data.Stages.Values)
                {
                    if (_profile.IsStageCleared(stage.Id)) { selected = System.Math.Max(selected, stage.Id); }
                }
            }
            _stageId = _serverClaim?.HasPendingStageReward == true ? _serverClaim.PendingStageId
                : IsReachable(selected) ? selected : _profile.HighestUnlockedStage;
            RememberSelection();
            Refresh();
            _panel.SetActive(true);
        }

        private void RememberSelection()
        {
            _hasSelection = true;
            PlayerPrefs.SetInt(SelectedStageKey, _stageId);
            PlayerPrefs.Save();
        }

        private void Move(int delta)
        {
            if (_claiming || _launching || _serverClaim?.HasPendingStageReward == true) { return; }
            if (IsReachable(_stageId + delta))
            {
                _stageId += delta;
                RememberSelection();
                Refresh();
            }
        }

        // 지금 도전 중인 관문(열린 관문)까지만 보여 준다. 잠긴 뒤 관문은 넘겨 볼 수 없다
        private bool IsReachable(int stageId) => _data.Stages.Contains(stageId) && _profile.IsStageUnlocked(stageId);

        private void Refresh()
        {
            bool unlocked = _profile.IsStageUnlocked(_stageId);
            bool busy = _claiming || _launching || _upgradeRecovery?.HasPendingPurchase == true;
            bool recovering = _serverClaim?.HasPendingStageReward == true;

            _card.Show(_stageId, _profile, busy, _claiming, recovering);
            _carousel.Show(_stageId);
            _enterButton.interactable = unlocked && !busy;
            // 넘어갈 관문이 없는 쪽(첫 관문, 지금 도전 중인 관문)의 화살표는 숨긴다
            _prevButton.gameObject.SetActive(IsReachable(_stageId - 1));
            _nextButton.gameObject.SetActive(IsReachable(_stageId + 1));
            _prevButton.interactable = !busy && !recovering;
            _nextButton.interactable = !busy && !recovering;
            _backButton.interactable = !busy;
            _costText.text = $"x{_profile.EnergyConfig.Cost}";
            if (_upgradeRecovery?.HasPendingPurchase == true) { _card.Status = "미확정 강화 결과를 먼저 확인해 주세요."; }
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
            if (_claiming || _launching || (_serverClaim?.HasPendingStageReward != true && !_profile.HasUnclaimedReward(_stageId))) { return; }
            if (_upgradeRecovery?.HasPendingPurchase == true) { _card.Status = "미확정 강화 결과를 먼저 확인해 주세요."; return; }
            _claiming = true;
            Refresh();
            int stageId = _stageId;
            var before = _profile.Snapshot();
            try
            {
                var snapshot = _serverClaim?.HasPendingStageReward == true ? await _serverClaim.RecoverStageReward()
                    : await _stageApi.ClaimRatingReward(stageId);
                if (_serverClaim != null)
                {
                    _profile.Apply(snapshot);
                    if (this != null) { _card.Status = $"제 {stageId} 관문 · 서버에서 {_profile.ClaimedRating(stageId)}등급까지 수령된 상태를 확인했어요."; }
                }
                else
                {
                    var rows = RewardRows.Gained(_itemIcons, _data, before, snapshot);
                    _profile.Apply(snapshot);
                    if (this != null) { _rewardPopup.Show(rows, $"제 {stageId} 관문의 미수령 보상을 모두 받았어요."); }
                }
            }
            catch (ApiException error)
            {
                if (this != null)
                {
                    _card.Status = _serverClaim != null ? ServerClaimError(error.Code)
                        : error.Code == "NO_REWARD" ? "이미 수령한 보상이에요"
                        : "보상을 받지 못했어요. 상자 영역을 눌러 다시 시도하세요.";
                }
            }
            catch (System.Exception) when (_serverClaim != null)
            {
                if (this != null)
                {
                    _card.Status = _serverClaim.HasPendingStageReward ? ServerClaimError("STAGE_REWARD_PENDING")
                        : "서버 요청 중입니다. 잠시 뒤 수령 상태를 확인해 주세요.";
                }
            }
            finally
            {
                _claiming = false;
                if (this != null)
                {
                    string status = _card.Status;
                    Refresh();
                    if (_serverClaim != null || _profile.HasUnclaimedReward(stageId)) { _card.Status = status; }
                }
            }
        }

        private string ServerClaimError(string code)
        {
            if (code == "SESSION_EXPIRED" || code == "HTTP_401") { return "다시 로그인한 뒤 서버 수령 상태를 확인해 주세요."; }
            if (code == "UPGRADE_RECOVERY_REQUIRED") { return "미확정 강화 결과를 먼저 확인해 주세요."; }
            if (_serverClaim.HasPendingStageReward)
            { return $"{_serverClaim.PendingTargetRating}등급의 수령 여부가 미확정입니다. 상자를 눌러 조회하세요. 재수령 요청은 보내지 않아요."; }
            return code == "NO_REWARD" ? "현재 조회한 서버 상태에는 미수령 별 보상이 없어요."
                : "요청을 확인하지 못했어요. 서버 상태를 다시 확인해 주세요.";
        }

        private void OnRewardClicked(int rating)
        {
            if (_claiming || _launching || _profile.HasUnclaimedReward(_stageId)) { return; }
            if (_serverClaim != null)
            {
                if (_serverClaim.HasPendingStageReward) { ClaimRewardsAsync().Forget(); return; }
                _rewardInfoPopup.Show("서버 별 보상", $"제 {_stageId} 관문 · 서버에서 {_profile.ClaimedRating(_stageId)}등급까지 수령된 상태입니다.\n"
                    + "별 보상은 골드이며 지급 금액은 서버가 결정합니다.", System.Array.Empty<ResultRewardItem>());
                return;
            }
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
            _panel.SetActive(true);
            Refresh();
            _card.Status = "관문에 입장하지 못했어요. 다시 시도하세요.";
            Debug.LogException(error);
        }

        private void OnStartFailed(StartFailed message)
        {
            _launching = false;
            _panel.SetActive(true);
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
