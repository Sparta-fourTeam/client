using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
    /// <summary>Localization 시작 로케일 선택기. 저장된 언어(GameSettings)가 있으면 그 언어, 없으면 시스템 언어를 따른다.
    /// GameSettingsApplier가 SelectedLocale을 대입하기 전에도 첫 프레임부터 맞는 언어로 시작하게 한다.
    /// LocalizationSettings 에셋의 Startup Locale Selectors에 넣어 쓴다(SerializeReference)</summary>
    [Serializable]
    public sealed class GameLanguageLocaleSelector : IStartupLocaleSelector
    {
        /// <summary>저장된 언어 또는 시스템 언어에 해당하는 로케일. 찾지 못하면 null을 돌려 다음 선택기로 넘긴다</summary>
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            return availableLocales.GetLocale(new LocaleIdentifier(GameLanguageLocale.StartupCode()));
        }
    }
}
