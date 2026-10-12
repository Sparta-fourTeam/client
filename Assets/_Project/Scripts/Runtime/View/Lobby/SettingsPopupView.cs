using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.View
{
    /// <summary>설정 팝업. 음악·효과음·음성 볼륨을 슬라이더(0~100%)로 조절한다. 값은 바로 적용·저장된다(SoundSettings)</summary>
    public sealed class SettingsPopupView : HudView
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private PopupTransition _transition;
        [SerializeField] private Button _closeButton;

        [Header("음악, 효과음, 음성 순서")]
        [SerializeField] private Slider[] _sliders;
        [SerializeField] private TMP_Text[] _valueTexts;

        private static readonly SoundChannel[] Channels = { SoundChannel.Music, SoundChannel.Sfx, SoundChannel.Voice };

        private SoundSettings _settings;

        public bool IsOpen => _panel.activeSelf;

        [Inject]
        public void Construct(SoundSettings settings)
        {
            _settings = settings;
        }

        protected override void InitializeView()
        {
            _panel.SetActive(false);
            _closeButton.onClick.AddListener(Close);
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
            for (int i = 0; i < _sliders.Length && i < Channels.Length; i++)
            {
                float volume = _settings != null ? _settings.Get(Channels[i]) : SoundSettings.DefaultVolume;
                _sliders[i].SetValueWithoutNotify(volume);
                ShowValue(i, volume);
            }
        }

        public void Close() => PopupPanel.Set(_panel, _transition, false);

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
