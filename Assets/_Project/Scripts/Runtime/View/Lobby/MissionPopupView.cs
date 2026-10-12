using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>선택 관문의 등장 요마와 획득 가능한 보상을 보여준다. 칸을 누르면 공용 상세 창(DetailPopupView)에
    /// 몬스터의 등급·특징이나 보상의 이름·설명을 띄운다</summary>
    public sealed class MissionPopupView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry)
        {
            _detail = registry.Get<DetailPopupView>();
        }

        private DetailPopupView _detail;
        private GameDataStore _data;

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [SerializeField] private Button _closeButton, _confirmButton;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private StageMonsterListView _monsters;
        [SerializeField] private ResultRewardListView _rewards;
        [SerializeField] private ItemIconTable _itemIcons;
        [SerializeField] private MonsterDisplayTable _monsterDisplay;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _confirmButton.onClick.AddListener(Close);
        }

        public void ShowDetails(GameDataStore data, int stageId)
        {
            _title.text = $"제 {stageId} 관문";
            _description.text = "등장 요마와 클리어 시 획득할 수 있는 보상을 확인하세요.";
            _data = data;
            var monsters = MonsterRows.Build(_monsterDisplay, data, stageId);
            _monsters.Show(monsters, i => DetailContent.ShowMonster(_detail, _data, _monsterDisplay, monsters[i]));
            var maximum = StageRewardRules.Maximum(data.StageRewards.GetOrThrow(stageId));
            var rewards = RewardRows.Build(_itemIcons, data, maximum.Coin, maximum.Exp, maximum.Items);
            _rewards.Show(rewards, i => DetailContent.ShowReward(_detail, _data, rewards[i], "최대 {0}개 획득"));
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
