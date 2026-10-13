using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>관문, 우편 등에서 수령한 보상 목록을 표시하는 공통 모달.</summary>
    public sealed class RewardPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private TMP_Text _title, _description;
        [SerializeField] private Button _closeButton, _confirmButton;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private ResultRewardListView _rewards;

        private void Awake()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _confirmButton.onClick.AddListener(Close);
        }

        public void Show(IReadOnlyList<ResultRewardItem> rewards, string description = "보상을 받았어요.", string title = "보상 획득")
        {
            _title.text = title;
            _description.text = description;
            _rewards.Show(rewards);
            PopupPanel.Set(_panel, _transition, true);
            PopupPanel.FitContent(_scroll, 560f, 1040f);
            _scroll.verticalNormalizedPosition = 1f;
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);
    }
}
