using System;
using UnityEngine;

namespace Game.Core
{
    public enum FrameRateMode
    {
        High,
        Standard,
    }

    /// <summary>언어 선택지. 실제 문구 번역은 로컬라이징(#193)에서 붙이고, 지금은 선택값만 저장한다. 다른 언어는 나중에 값을 뒤에 추가한다</summary>
    public enum GameLanguage
    {
        Korean,
        English,
    }

    /// <summary>정수 설정을 기기에 남기는 저장소. 테스트에서는 메모리 구현으로 바꾼다</summary>
    public interface ISettingsStore
    {
        bool TryGet(string key, out int value);
        void Set(string key, int value);
    }

    /// <summary>PlayerPrefs에 설정을 저장한다. 계정이 아니라 기기 설정이다</summary>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public bool TryGet(string key, out int value)
        {
            value = PlayerPrefs.GetInt(key, 0);
            return PlayerPrefs.HasKey(key);
        }

        public void Set(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>볼륨을 뺀 기기 설정: 데미지 표시, 디버프 이펙트, 프레임레이트, 최고 스테이지 자동 입장, 언어.
    /// 바꾸면 저장하고 Changed로 알린다. 저장된 값이 없으면 기본값이다</summary>
    public sealed class GameSettings
    {
        public const int HighFrameRate = 60;
        public const int StandardFrameRate = 30;

        private const string DamageKey = "settings.damage_indication";
        private const string DebuffKey = "settings.debuff_effects";
        private const string FrameRateKey = "settings.frame_rate";
        private const string AutoEnterKey = "settings.auto_enter_highest_stage";
        private const string LanguageKey = "settings.language";

        private readonly ISettingsStore _store;

        public event Action Changed;

        public GameSettings(ISettingsStore store)
        {
            _store = store;
            DamageIndication = GetBool(DamageKey, true);
            DebuffEffects = GetBool(DebuffKey, true);
            AutoEnterHighestStage = GetBool(AutoEnterKey, false);
            FrameRate = _store.TryGet(FrameRateKey, out int rate) && Enum.IsDefined(typeof(FrameRateMode), rate)
                ? (FrameRateMode)rate : FrameRateMode.High;
            Language = _store.TryGet(LanguageKey, out int language) && Enum.IsDefined(typeof(GameLanguage), language)
                ? (GameLanguage)language : GameLanguage.Korean;
        }

        public bool DamageIndication { get; private set; }
        public bool DebuffEffects { get; private set; }
        public bool AutoEnterHighestStage { get; private set; }
        public FrameRateMode FrameRate { get; private set; }
        public GameLanguage Language { get; private set; }

        public int TargetFrameRate => FrameRate == FrameRateMode.High ? HighFrameRate : StandardFrameRate;

        public void SetDamageIndication(bool value)
        {
            if (DamageIndication == value) { return; }
            DamageIndication = value;
            SaveBool(DamageKey, value);
        }

        public void SetDebuffEffects(bool value)
        {
            if (DebuffEffects == value) { return; }
            DebuffEffects = value;
            SaveBool(DebuffKey, value);
        }

        public void SetAutoEnterHighestStage(bool value)
        {
            if (AutoEnterHighestStage == value) { return; }
            AutoEnterHighestStage = value;
            SaveBool(AutoEnterKey, value);
        }

        public void SetFrameRate(FrameRateMode mode)
        {
            if (FrameRate == mode) { return; }
            FrameRate = mode;
            _store.Set(FrameRateKey, (int)mode);
            Changed?.Invoke();
        }

        public void SetLanguage(GameLanguage language)
        {
            if (Language == language) { return; }
            Language = language;
            _store.Set(LanguageKey, (int)language);
            Changed?.Invoke();
        }

        private void SaveBool(string key, bool value)
        {
            _store.Set(key, value ? 1 : 0);
            Changed?.Invoke();
        }

        private bool GetBool(string key, bool fallback) => _store.TryGet(key, out int value) ? value != 0 : fallback;
    }
}
