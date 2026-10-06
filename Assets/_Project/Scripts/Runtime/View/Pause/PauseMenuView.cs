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

        [Header("획득 가능 (지금 포기하거나 져도 받게 되는 확보 보상)")]
        [SerializeField] private ResultRewardListView _obtainableList;
        [SerializeField] private ItemIconTable _itemIcons;

        private StageManager _stageManager;
        private GameDataStore _data;
        private StageRewardTracker _rewards;

        [Inject]
        public void Construct(
            StageManager stageManager,
            IBufferedSubscriber<StageStateChanged> stateChanged,
            GameDataStore data,
            StageRewardTracker rewards)
        {
            _stageManager = stageManager;
            _data = data;
            _rewards = rewards;
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
