using Game.Core;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    public sealed class PauseMenuView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _abandonButton;

        [Header("획득 가능 (이 스테이지에서 받을 수 있는 최대 보상)")]
        [SerializeField] private ResultRewardListView _obtainableList;
        [SerializeField] private ItemIconTable _itemIcons;

        private StageManager _stageManager;
        private GameDataStore _data;
        private StageRewardBalance _balance;
        private bool _obtainableShown;

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<StageStateChanged> stateChanged,
            GameDataStore data,
            StageRewardBalance balance)
        {
            _stageManager = stageManager;
            _data = data;
            _balance = balance;
            Track(stateChanged.Subscribe(OnStateChanged));
        }

        private void Awake()
        {
            _panel.SetActive(false);
            _resumeButton.onClick.AddListener(() => _stageManager.Resume());
            // 포기하기는 판정 없이 결과 전송으로 간다 (docs/flows.md).
            // Forfeit은 Paused에서만 동작하고 바로 Submitting으로 넘어가므로 연타해도 한 번만 전송된다
            _abandonButton.onClick.AddListener(() => _stageManager.Forfeit());
        }

        private void OnStateChanged(StageStateChanged message)
        {
            bool paused = message.State == StageState.Paused;
            if (paused)
            {
                ShowObtainable();
            }

            _panel.SetActive(paused);
        }

        /// <summary>스테이지가 같으면 목록도 같으므로 처음 열 때 한 번만 만든다</summary>
        private void ShowObtainable()
        {
            if (_obtainableShown || _obtainableList == null)
            {
                return;
            }

            var maximum = StageRewardRules.Maximum(_balance);
            _obtainableList.Show(RewardRows.Build(_itemIcons, _data, maximum.Coin, maximum.Exp, maximum.Items));
            _obtainableShown = true;
        }
    }
}
