using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>보상 목록 화면의 '확률 정보' 버튼. 보상에 확률형 보상이 있을 때만 보이고, 누르면 공통 확률 팝업을 연다.
    /// 화면 뷰가 InitializeView에서 Init, BindUi에서 Bind, 보상을 보여줄 때 Refresh를 부른다</summary>
    internal sealed class RewardOddsButton
    {
        private readonly Button _button;
        private RewardOddsPopupView _popup;
        private List<string> _randomItems = new();

        public RewardOddsButton(Button button) => _button = button;

        public void Init()
        {
            if (_button == null) { return; }
            _button.gameObject.SetActive(false);
            _button.onClick.AddListener(Open);
        }

        public void Bind(IUiRegistry registry)
        {
            _popup = registry.TryGet<RewardOddsPopupView>();
            if (_button != null && _popup == null)
            { Debug.LogWarning("RewardOddsPopupView가 UI 구성에 없어 '확률 정보' 버튼을 숨깁니다."); }
        }

        public void Refresh(IEnumerable<ItemAmount> rewards)
        {
            _randomItems = RandomRewardOdds.RandomItemsOf(rewards);
            if (_button != null) { _button.gameObject.SetActive(_popup != null && _randomItems.Count > 0); }
        }

        private void Open()
        {
            if (_popup != null) { _popup.Show(_randomItems); }
        }
    }
}
