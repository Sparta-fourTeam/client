using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class PauseMenuView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _abandonButton;

        [Header("획득 가능 (지금 포기하거나 져도 받게 되는 확보 보상)")]
        [SerializeField] private ResultRewardListView _obtainableList;
        [SerializeField] private ItemIconTable _itemIcons;

        [Header("등장 요마 (현재 스테이지의 몬스터)")]
        [SerializeField] private StageMonsterListView _monsterList;
        [SerializeField] private MonsterDisplayTable _monsterDisplay;

        private StageManager _stageManager;
        private GameDataStore _data;
        private StageRewardTracker _rewards;
        private StageContext _stageContext;

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<StageStateChanged> stateChanged,
            GameDataStore data,
            StageRewardTracker rewards,
            StageContext stageContext)
        {
            _stageManager = stageManager;
            _data = data;
            _rewards = rewards;
            _stageContext = stageContext;
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _resumeButton.onClick.AddListener(() => _stageManager.Resume());
            // 닫기(X)는 계속하기와 같다. 프리팹에 없는 씬에서도 동작하도록 비어 있으면 건너뛴다
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(() => _stageManager.Resume());
            }
            // 포기하기는 판정 없이 결과 전송으로 간다 (docs/flows.md).
            // Forfeit은 Paused에서만 동작하고 바로 Submitting으로 넘어가므로 연타해도 한 번만 전송된다
            _abandonButton.onClick.AddListener(() => _stageManager.Forfeit());
        }

        private void OnStateChanged(StageStateChanged message)
        {
            bool paused = message.State == StageState.Paused;
            if (paused)
            {
                ShowTitle();
                ShowMonsters();
                ShowObtainable();
            }

            _panel.SetActive(paused);
        }

        /// <summary>"제 N 관문". Stage 씬을 바로 열어 StageId가 없으면 첫 스테이지로 본다</summary>
        private void ShowTitle()
        {
            if (_titleText != null)
            {
                _titleText.text = $"제 {_data.StageOrFirst(_stageContext.StageId).Id} 관문";
            }
        }

        /// <summary>현재 스테이지의 등장 몬스터. 열 때마다 다시 만들어 재시작·스테이지 전환 뒤에도 이전 정보가 남지 않는다</summary>
        private void ShowMonsters()
        {
            if (_monsterList == null)
            {
                return;
            }

            _monsterList.Show(MonsterRows.Build(_monsterDisplay, _data, _stageContext.StageId));
        }

        /// <summary>완료한 웨이브까지 확보한 보상이다. 웨이브가 끝날 때마다 달라지므로 열 때마다 다시 만든다</summary>
        private void ShowObtainable()
        {
            if (_obtainableList == null || _rewards.Rewards == null)
            {
                return;
            }

            var secured = _rewards.Rewards;
            _obtainableList.Show(RewardRows.Build(_itemIcons, _data, secured.Coin, secured.Exp, secured.Items));
        }
    }
}
