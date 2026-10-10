using System.Collections.Generic;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>미수령 보상이 없을 때 선택한 상자의 조건과 1회성 보상을 보여준다.</summary>
    public sealed class MissionRewardInfoPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [SerializeField] private Button _closeButton, _confirmButton;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private ResultRewardListView _rewards;
        [SerializeField] private ItemIconTable _itemIcons;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _confirmButton.onClick.AddListener(Close);
        }

        public void Show(GameDataStore data, int stageId, int rating, bool claimed)
        {
            var stage = data.Stages.GetOrThrow(stageId);
            _title.text = rating == 1 ? "관문 클리어" : $"방벽 체력 {(rating == 2 ? 50 : 100)}%";
            string condition = rating == 1 ? "관문을 클리어하면 받을 수 있어요."
                : $"방벽 체력을 {(rating == 2 ? 50 : 100)}% 이상 남기고 클리어하세요.";
            _description.text = claimed ? $"제 {stageId} 관문 · 이미 수령한 보상이에요." : $"제 {stageId} 관문 · {condition}";
            var items = stage.RatingItemRewards != null && stage.RatingItemRewards.Count >= rating
                ? stage.RatingItemRewards[rating - 1] : null;
            _rewards.Show(RewardRows.Build(_itemIcons, data, stage.RatingRewards[rating - 1], 0, items ?? new List<ItemAmount>()));
            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1040f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);
    }
}
