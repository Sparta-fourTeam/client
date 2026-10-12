using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>아이템·몬스터 등을 눌렀을 때 뜨는 공용 상세 창. 아이콘·제목·부제·설명을 보여주고,
    /// 이동할 곳이 있으면 아래 버튼(예: 강화하러 가기)을 띄운다. 어두운 배경이나 닫기를 누르면 닫힌다.
    /// 인벤토리와 미션 상세가 같이 쓴다</summary>
    public sealed class DetailPopupView : HudView
    {
        public const string DefaultGoLabel = "강화하러 가기";

        [SerializeField] private GameObject _panel;
        [SerializeField] private PointerClickRelay _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _titleText, _subtitleText, _descText;
        [SerializeField] private Button _goButton, _closeButton;
        [SerializeField] private TMP_Text _goLabel;

        private Action _onGo;

        public bool IsShown => _panel.activeSelf;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _background.Clicked += Hide;
            _closeButton.onClick.AddListener(Hide);
            _goButton.onClick.AddListener(() =>
            {
                var go = _onGo;
                Hide();
                go?.Invoke();
            });
        }

        /// <param name="onGo">아래 버튼을 누르면 부른다. null이면 버튼을 숨긴다</param>
        public void Show(Sprite icon, string title, string subtitle, string description, Action onGo = null, string goLabel = DefaultGoLabel)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _titleText.text = title;
            _subtitleText.text = subtitle;
            _descText.text = description;
            _onGo = onGo;
            _goButton.gameObject.SetActive(onGo != null);
            _goLabel.text = goLabel;
            _panel.SetActive(true);
        }

        public void Hide()
        {
            _onGo = null;
            _panel.SetActive(false);
        }
    }
}
