using UnityEngine;

namespace Game.Core
{
    /// <summary>GameLanguage와 Unity Localization 로케일 코드의 대응. 코드는 Localization Settings의 로케일과 같아야 한다</summary>
    public static class GameLanguageLocale
    {
        /// <summary>언어에 해당하는 로케일 코드("ko", "en", "ja", "zh-Hans")</summary>
        public static string Code(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.English: return "en";
                case GameLanguage.Japanese: return "ja";
                case GameLanguage.ChineseSimplified: return "zh-Hans";
                default: return "ko";
            }
        }

        /// <summary>시스템 언어에 맞는 언어. 사용자가 아직 고르지 않았을 때의 기본값이다. 지원하지 않는 언어(번체 중국어 포함)는 영어</summary>
        public static GameLanguage FromSystemLanguage(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Korean: return GameLanguage.Korean;
                case SystemLanguage.Japanese: return GameLanguage.Japanese;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return GameLanguage.ChineseSimplified;
                default: return GameLanguage.English;
            }
        }
    }
}
