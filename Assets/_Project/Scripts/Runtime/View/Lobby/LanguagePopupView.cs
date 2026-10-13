using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>언어 선택 팝업. 하나를 고르면 바로 저장하고 닫는다. 선택값만 저장하며, 실제 문구 번역은 로컬라이징(#193)에서 붙인다.
    /// 칸 순서는 GameLanguage 순서와 같다</summary>
    public sealed class LanguagePopupView : HudView
    {
        private static readonly string[] Names = { "한국어", "English" };

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Toggle[] _options;
        [SerializeField] private TMP_Text[] _labels;

        private GameSettings _settings;

        /// <summary>언어를 그 언어 자신의 글자로 쓴 이름</summary>
        public static string NativeName(GameLanguage language) => Names[(int)language];

        [Inject]
        public void Construct(GameSettings settings) => _settings = settings;

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            for (int i = 0; i < _options.Length; i++)
            {
                var language = (GameLanguage)i;
                _labels[i].text = Names[i];
                _options[i].onValueChanged.AddListener(on =>
                {
                    if (!on) { return; }
                    _settings.SetLanguage(language);
                    Close();
                });
            }
        }

        public void Open()
        {
            for (int i = 0; i < _options.Length; i++)
            {
                _options[i].SetIsOnWithoutNotify(i == (int)_settings.Language);
            }

            PopupPanel.Set(_panel, _transition, true);
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);
    }
}
