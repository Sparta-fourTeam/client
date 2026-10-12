using System.Collections.Generic;
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
    /// <summary>스테이지가 끝나고 결과 제출까지 끝나면 결과를 보여준다.
    /// StageEnded가 아니라 StageResult를 구독한다: 서버가 확정한 보상이 들어 있고, 판정 없이 끝나는 포기(Forfeit)도 StageResult로 이어지기 때문이다.
    /// 전투 튜토리얼(Tutorials.json)에서 지면 버튼이 [다시 도전]으로 바뀌고 로비 대신 같은 스테이지를 다시 발급받는다</summary>
    public sealed class ResultPopupView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _detail = registry.Get<DetailPopupView>();
        }

        // 보상 칸을 누르면 여는 공용 상세 창
        private DetailPopupView _detail;

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private TMP_Text _killsText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _bubbleText;
        [SerializeField] private Button _lobbyButton;

        [Header("보상 (코인, EXP, 재료, 보석상자 순서)")]
        [SerializeField] private ResultRewardListView _rewardList;
        [SerializeField] private ItemIconTable _itemIcons;

        [SerializeField] private ResultStarsView _starsView;

        [Tooltip("실패하면 결과 위에 겹쳐 띄우는 TIP 패널. 비우면 띄우지 않는다")]
        [SerializeField] private ResultTipView _tip;


        // 스킬별 피해량은 StageResult.SkillDamages로 온다. 보유 스킬(SkillChanged)의 레벨과 SkillAssetTable의 아이콘으로 줄을 만든다.
        // 목록이 비면 영역이 꺼진다
        [Header("인법 피해량")]
        [SerializeField] private ResultDamageListView _damageList;
        [SerializeField] private SkillAssetTable _skillAssets;

        private const string ClearBubble = "어때요? 이 정도쯤이야!";
        private const string FailBubble = "으으... 다음엔 꼭...!";
        private const string TutorialRetryBubble = "괜찮아요, 한 번 더 해 봐요!";
        private const string RetryLabel = "다시 도전";

        private StageContext _stageContext;
        private ISceneNavigator _navigator;
        private GameDataStore _data;
        private IReadOnlyList<ISkillStatus> _ownedSkills;
        private bool _firstStageClear;
        private PlayerProfile _profile;
        private BattleLauncher _launcher;
        private TMP_Text _lobbyLabel;
        private string _lobbyText;
        private bool _retryTutorial;

        [Inject]
        public void Construct(ISubscriber<StageResult> stageResult, StageContext stageContext, ISceneNavigator navigator, GameDataStore data,
            IBufferedSubscriber<SkillChanged> skillChanged, PlayerProfile profile, BattleLauncher launcher)
        {
            _profile = profile;
            _launcher = launcher;
            Track(skillChanged.Subscribe(message => _ownedSkills = message.Skills));
            _stageContext = stageContext;
            _firstStageClear = stageContext.StageId == 1 && !profile.IsStageCleared(1);
            _data = data;
            _navigator = navigator;
            Track(stageResult.Subscribe(OnStageResult));
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _lobbyButton.onClick.AddListener(OnLobbyClicked);
            _lobbyLabel = _lobbyButton.GetComponentInChildren<TMP_Text>(true);
            _lobbyText = _lobbyLabel != null ? _lobbyLabel.text : null;
        }

        private void OnStageResult(StageResult result)
        {
            // 프로필은 전투 전 상태다: 전투 전에 이 스테이지가 진행 중인 전투 튜토리얼이었고 이번에도 졌으면 재도전
            _retryTutorial = !result.Cleared
                && TutorialProgress.BattleStage(_data.Tutorials, TutorialFacts.From(_profile)) == _stageContext.StageId;
            _titleText.text = result.Cleared ? "Clear !" : "Fail !";
            _stageText.text = _stageContext.StageId > 0 ? $"Stage {_stageContext.StageId}" : "-";
            _killsText.text = $"처치\n{result.Kills}";
            _waveText.text = result.ReachedWave > 0 ? $"도달 웨이브\n{result.ReachedWave}" : "도달 웨이브\n-";
            _timeText.text = $"플레이 시간\n{FormatTime(result.PlayTime)}";
            _bubbleText.text = result.Cleared
                ? (_firstStageClear ? "첫 클리어! 로비에서 새 무기를 확인해 봐요!" : ClearBubble)
                : _retryTutorial ? TutorialRetryBubble : FailBubble;
            if (_lobbyLabel != null)
            {
                _lobbyLabel.text = _retryTutorial ? RetryLabel : _lobbyText;
            }
            var rewards = RewardRows.Build(_itemIcons, _data, result.RewardGold, result.RewardExp, result.RewardItems);
            _rewardList.Show(rewards, i => DetailContent.ShowReward(_detail, _data, rewards[i], "획득 {0}개"));
            _starsView.Show(result.Cleared, result.ClearRating);
            if (_damageList != null)
            {
                _damageList.Show(SkillDamageRows.Build(result.SkillDamages, _ownedSkills,
                    status => _skillAssets != null ? _skillAssets.GetHudIcon(status.AssetKey) : null));
            }
            _lobbyButton.interactable = true;
            PopupPanel.Set(_panel, _transition, true);
            ShowTip(result.Cleared);
        }

        // 실패에만 결과 위에 TIP을 띄운다. 누르면 닫히고 아래의 실패 결과가 그대로 보인다
        private void ShowTip(bool cleared)
        {
            if (_tip == null)
            {
                return;
            }

            if (cleared)
            {
                _tip.Hide();
            }
            else
            {
                _tip.Show();
            }
        }

        private void OnLobbyClicked()
        {
            // 연타로 씬 전환이 두 번 일어나지 않게 막는다
            _lobbyButton.interactable = false;
            LeaveAsync().Forget(Debug.LogException);
        }

        private async UniTask LeaveAsync()
        {
            // 재도전 발급이 거절되면 로비로 간다. 로비에서 다시 튜토리얼 전투로 보낸다
            if (_retryTutorial && await _launcher.Launch(_stageContext.StageId))
            {
                return;
            }

            await _navigator.GoToLobby();
        }

        // 초를 "mm:ss"로 바꾼다
        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
