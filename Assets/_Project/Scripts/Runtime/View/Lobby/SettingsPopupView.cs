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

        [Header("Unity 진단 ID")]
        [SerializeField] private TMP_Text _diagnosticTitle, _diagnosticInfo, _diagnosticIdText, _diagnosticCopyLabel;
        [SerializeField] private Button _diagnosticCopyButton;

        private string _diagnosticId;
        private bool _diagnosticCopied, _diagnosticCopyFailed;

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
            _diagnosticCopyButton.onClick.AddListener(CopyDiagnosticId);
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
            _diagnosticId = ReadDiagnosticId();
            _diagnosticCopied = _diagnosticCopyFailed = false;
            RefreshLanguage();
            for (int i = 0; i < _sliders.Length && i < Channels.Length; i++)
            {
                float volume = _settings != null ? _settings.Get(Channels[i]) : SoundSettings.DefaultVolume;
                _sliders[i].SetValueWithoutNotify(volume);
                ShowValue(i, volume);
            }
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);

        private void RefreshLanguage()
        {
            _languageLabel.text = LanguagePopupView.NativeName(_game.Language);
            bool english = _game.Language == GameLanguage.English;
            _diagnosticTitle.text = english ? "Unity diagnostic ID" : "Unity 진단 ID";
            _diagnosticInfo.text = english
                ? "Installation ID for privacy inquiries and diagnostic data deletion requests. Separate from account and advertising IDs.\nContact: teamdoodle.dev@gmail.com"
                : "개인정보 문의·진단 데이터 삭제 요청에 사용하는 설치 ID입니다. 계정 ID·광고 ID와 다릅니다.\n문의: teamdoodle.dev@gmail.com";
            bool available = !string.IsNullOrWhiteSpace(_diagnosticId);
            _diagnosticIdText.text = available ? _diagnosticId : english ? "ID unavailable on this installation" : "이 설치에서는 ID를 확인할 수 없습니다";
            _diagnosticCopyButton.interactable = available;
            _diagnosticCopyLabel.text = _diagnosticCopyFailed
                ? english ? "Copy failed — try again" : "복사 실패 · 다시 시도"
                : _diagnosticCopied ? english ? "Copied" : "복사 완료"
                : english ? "Copy ID" : "ID 복사";
        }

        private static string ReadDiagnosticId()
        {
#if UNITY_6000_6_OR_NEWER
            try
            {
                return UnityEngine.Identifiers.Identifiers.installationId;
            }
            catch (System.Exception)
            {
                // ID나 예외 내용을 기록하지 않는다. 미지원 플랫폼도 같은 안내를 쓴다.
                return null;
            }
#else
            return null;
#endif
        }

        private void CopyDiagnosticId()
        {
            if (string.IsNullOrWhiteSpace(_diagnosticId)) { return; }
            try
            {
                GUIUtility.systemCopyBuffer = _diagnosticId;
                _diagnosticCopied = true;
                _diagnosticCopyFailed = false;
            }
            catch (System.Exception)
            {
                _diagnosticCopied = false;
                _diagnosticCopyFailed = true;
            }
            RefreshLanguage();
        }

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
