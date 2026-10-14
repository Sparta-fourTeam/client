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
        [Tooltip("스테이지 몬스터 강화(체력·공격력 등) 안내 줄(바탕 포함). 강화가 없으면 통째로 숨긴다. 비어 있으면 건너뛴다")]
        [SerializeField] private GameObject _bonus;
        [SerializeField] private TMP_Text _bonusText;
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
            if (_description != null) { _description.text = "등장 요마와 클리어 시 획득할 수 있는 보상을 확인하세요."; }
            _data = data;
            ShowBonuses(data.Stages.GetOrThrow(stageId));
            var monsters = MonsterRows.Build(_monsterDisplay, data, stageId);
            _monsters.Show(monsters, i => DetailContent.ShowMonster(_detail, _data, _monsterDisplay, monsters[i]));
            var maximum = StageRewardRules.Maximum(data.StageRewards.GetOrThrow(stageId));
            var rewards = RewardRows.Build(_itemIcons, data, maximum.Coin, maximum.Exp, maximum.Items);
            _rewards.Show(rewards, i => DetailContent.ShowReward(_detail, _data, rewards[i], "최대 {0}개 획득"));
            Show();
        }

        private const float BonusLineHeight = 40f, BonusPadding = 16f;

        /// <summary>능력치마다 한 줄씩 보여주고, 줄 수만큼 바탕의 높이를 늘린다</summary>
        private void ShowBonuses(StageDefinition stage)
        {
            if (_bonus == null || _bonusText == null)
            {
                return;
            }

            var lines = new System.Collections.Generic.List<string>();
            foreach (var bonus in stage.MonsterBonuses ?? new System.Collections.Generic.List<MonsterBonus>())
            {
                lines.Add(MonsterBonusText.Line(bonus.GetStat(), bonus.Percent));
            }

            _bonus.SetActive(lines.Count > 0);
            _bonusText.text = string.Join("\n", lines);
            if (_bonus.TryGetComponent<LayoutElement>(out var layout))
            {
                layout.preferredHeight = layout.minHeight = lines.Count * BonusLineHeight + BonusPadding;
            }
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
