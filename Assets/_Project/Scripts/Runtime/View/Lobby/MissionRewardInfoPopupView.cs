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
            _title.SetL10n(() => rating == 1 ? L10n.Get("mission.stageClear") : L10n.Get("missionReward.barrierHp", (rating == 2 ? 50 : 100).ToString()));
            _description.SetL10n(() =>
            {
                string condition = rating == 1 ? L10n.Get("missionReward.conditionClear")
                    : L10n.Get("missionReward.conditionBarrier", (rating == 2 ? 50 : 100).ToString());
                return claimed ? L10n.Get("missionReward.claimedDesc", stageId.ToString()) : L10n.Get("missionReward.conditionDesc", stageId.ToString(), condition);
            });
            var items = stage.RatingItemRewards != null && stage.RatingItemRewards.Count >= rating
                ? stage.RatingItemRewards[rating - 1] : null;
            Open(RewardRows.Build(_itemIcons, data, stage.RatingRewards[rating - 1], 0, items ?? new List<ItemAmount>()));
        }

        /// <summary>관문이 아닌 상자(퀘스트 보물상자 등)의 조건과 보상을 보여준다</summary>
        public void Show(string title, string description, IReadOnlyList<ResultRewardItem> rewards)
        {
            _title.text = title;
            _description.text = description;
            Open(rewards);
        }

        private void Open(IReadOnlyList<ResultRewardItem> rewards)
        {
            _rewards.Show(rewards);
            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1040f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);
    }
}
