using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>선택 관문의 등장 요마와 획득 가능한 보상을 보여준다.</summary>
    public sealed class MissionPopupView : HudView, IUiBindable
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [SerializeField] private Button _closeButton, _confirmButton;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private StageMonsterListView _monsters;
        [SerializeField] private ResultRewardListView _rewards;
        [SerializeField] private ItemIconTable _itemIcons;
        [SerializeField] private MonsterDisplayTable _monsterDisplay;
        [SerializeField, Tooltip("랜덤 재료 보상이 있을 때만 보이는 '확률 정보' 버튼")] private Button _oddsButton;

        private RewardOddsButton _odds;

        public void BindUi(IUiRegistry registry) => (_odds ??= new RewardOddsButton(_oddsButton)).Bind(registry);

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _odds ??= new RewardOddsButton(_oddsButton);
            _odds.Init();
            _closeButton.onClick.AddListener(Close);
            _confirmButton.onClick.AddListener(Close);
        }

        public void ShowDetails(GameDataStore data, int stageId)
        {
            _title.text = $"제 {stageId} 관문";
            _description.text = "등장 요마와 클리어 시 획득할 수 있는 보상을 확인하세요.";
            _monsters.Show(MonsterRows.Build(_monsterDisplay, data, stageId));
            var maximum = StageRewardRules.Maximum(data.StageRewards.GetOrThrow(stageId));
            _rewards.Show(RewardRows.Build(_itemIcons, data, maximum.Coin, maximum.Exp, maximum.Items));
            _odds?.Refresh(maximum.Items);
            Show();
        }

        private void Show()
        {
            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 760f, 1120f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);
    }
}
