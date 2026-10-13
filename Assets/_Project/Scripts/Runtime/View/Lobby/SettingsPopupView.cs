using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>설정 팝업. 음악·효과음 볼륨을 슬라이더(0~100%)로 조절하고, 데미지 표시·디버프 이펙트·프레임레이트·최고 스테이지 자동 입장·언어를 고른다.
    /// 값은 바로 적용·저장된다(SoundSettings, GameSettings)</summary>
    public sealed class SettingsPopupView : HudView, IUiBindable
    {
        public void BindUi(IUiRegistry registry) => _language = registry.Get<LanguagePopupView>();

        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;

        [Header("음악, 효과음 순서")]
        [SerializeField] private Slider[] _sliders;
        [SerializeField] private TMP_Text[] _valueTexts;

        private static readonly SoundChannel[] Channels = { SoundChannel.Music, SoundChannel.Sfx };

        [Header("기기 설정")]
        [SerializeField] private Toggle _damageToggle, _debuffToggle, _autoEnterToggle;
        [SerializeField] private Toggle _frameHighToggle, _frameStandardToggle;
        [SerializeField] private Button _languageButton;
        [SerializeField] private TMP_Text _languageLabel;

        private SoundSettings _settings;
        private GameSettings _game;
        private LanguagePopupView _language;

        public bool IsOpen => _panel.activeSelf;

        [Inject]
        public void Construct(SoundSettings settings, GameSettings game)
        {
            _settings = settings;
            _game = game;
            _game.Changed += RefreshLanguage;
        }

        /// <summary>설정 변경 구독을 해제한다</summary>
        protected override void DisposeView()
        {
            if (_game != null)
            {
                _game.Changed -= RefreshLanguage;
            }

            base.DisposeView();
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
            _damageToggle.onValueChanged.AddListener(on => _game.SetDamageIndication(on));
            _debuffToggle.onValueChanged.AddListener(on => _game.SetDebuffEffects(on));
            _autoEnterToggle.onValueChanged.AddListener(on => _game.SetAutoEnterHighestStage(on));
            _frameHighToggle.onValueChanged.AddListener(on => { if (on) { _game.SetFrameRate(FrameRateMode.High); } });
            _frameStandardToggle.onValueChanged.AddListener(on => { if (on) { _game.SetFrameRate(FrameRateMode.Standard); } });
            _languageButton.onClick.AddListener(() => _language.Open());
            for (int i = 0; i < _sliders.Length && i < Channels.Length; i++)
            {
                int index = i;
                _sliders[i].minValue = 0f;
                _sliders[i].maxValue = 1f;
                _sliders[i].onValueChanged.AddListener(value => OnSlider(index, value));
            }
        }

        public void Open()
        {
            PopupPanel.Set(_panel, _transition, true);
            _damageToggle.SetIsOnWithoutNotify(_game.DamageIndication);
            _debuffToggle.SetIsOnWithoutNotify(_game.DebuffEffects);
            _autoEnterToggle.SetIsOnWithoutNotify(_game.AutoEnterHighestStage);
            _frameHighToggle.SetIsOnWithoutNotify(_game.FrameRate == FrameRateMode.High);
            _frameStandardToggle.SetIsOnWithoutNotify(_game.FrameRate == FrameRateMode.Standard);
            RefreshLanguage();
            for (int i = 0; i < _sliders.Length && i < Channels.Length; i++)
            {
                float volume = _settings != null ? _settings.Get(Channels[i]) : SoundSettings.DefaultVolume;
                _sliders[i].SetValueWithoutNotify(volume);
                ShowValue(i, volume);
            }
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);

        private void RefreshLanguage() => _languageLabel.text = LanguagePopupView.NativeName(_game.Language);

        private void OnSlider(int index, float value)
        {
            _settings?.Set(Channels[index], value);
            ShowValue(index, value);
        }

        private void ShowValue(int index, float value)
        {
            if (index < _valueTexts.Length)
            {
                _valueTexts[index].text = $"{Mathf.RoundToInt(value * 100f)}%";
            }
        }
    }
}
