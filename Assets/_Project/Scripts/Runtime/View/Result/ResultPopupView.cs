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

        // 아직 데이터가 없어 채우지 않는다. 무기별 피해량은 _damageList.Show(rows)를 OnStageResult에서 부르면 된다.
        // 호출하기 전에는 영역이 꺼져 있다
        [Header("데이터 연결 전 (StageResult에 아직 없음)")]
        [SerializeField] private ResultDamageListView _damageList;

        private const string ClearBubble = "어때요? 이 정도쯤이야!";
        private const string FailBubble = "으으... 다음엔 꼭...!";

        private StageContext _stageContext;
        private ISceneNavigator _navigator;
        private GameDataStore _data;

        [Inject]
        public void Construct(ISubscriber<StageResult> stageResult, StageContext stageContext, ISceneNavigator navigator, GameDataStore data)
        {
            _stageContext = stageContext;
            _data = data;
            _navigator = navigator;
            Track(stageResult.Subscribe(OnStageResult));
        }

        private void Awake()
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
            _bubbleText.text = result.Cleared ? ClearBubble : FailBubble;
            _rewardList.Show(RewardRows.Build(_itemIcons, _data, result.RewardGold, result.RewardExp, result.RewardItems));
            _starsView.Show(result.Cleared, result.ClearRating);
            _lobbyButton.interactable = true;
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
