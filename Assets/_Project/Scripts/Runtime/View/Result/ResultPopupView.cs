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
    /// StageEnded가 아니라 StageResult를 구독한다: 서버가 확정한 보상이 들어 있고, 판정 없이 끝나는 포기(Forfeit)도 StageResult로 이어지기 때문이다</summary>
    public sealed class ResultPopupView : HudView
    {
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

        private StageContext _stageContext;
        private ISceneNavigator _navigator;
        private GameDataStore _data;
        private IReadOnlyList<ISkillStatus> _ownedSkills;
        private bool _firstStageClear;

        [Inject]
        public void Construct(ISubscriber<StageResult> stageResult, StageContext stageContext, ISceneNavigator navigator, GameDataStore data,
            IBufferedSubscriber<SkillChanged> skillChanged, PlayerProfile profile)
        {
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
        }

        private void OnStageResult(StageResult result)
        {
            _titleText.text = result.Cleared ? "Clear !" : "Fail !";
            _stageText.text = _stageContext.StageId > 0 ? $"Stage {_stageContext.StageId}" : "-";
            _killsText.text = $"처치\n{result.Kills}";
            _waveText.text = result.ReachedWave > 0 ? $"도달 웨이브\n{result.ReachedWave}" : "도달 웨이브\n-";
            _timeText.text = $"플레이 시간\n{FormatTime(result.PlayTime)}";
            _bubbleText.text = result.Cleared
                ? (_firstStageClear ? "첫 클리어! 로비에서 화살을 강화해 봐요!" : ClearBubble)
                : FailBubble;
            _rewardList.Show(RewardRows.Build(_itemIcons, _data, result.RewardGold, result.RewardExp, result.RewardItems));
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
