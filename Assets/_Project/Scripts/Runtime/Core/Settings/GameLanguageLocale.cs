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
    }
}
