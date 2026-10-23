using UnityEngine.Localization.Settings;

namespace Game.Core
{
    /// <summary>UIStrings 테이블 조회 헬퍼. 로케일 전환은 LocalizationSettings.SelectedLocale로만 한다.</summary>
    public static class L10n
    {
        /// <summary>화면 문자열을 담은 String Table 이름</summary>
        public const string UITable = "UIStrings";

        /// <summary>현재 로케일의 UIStrings 값을 가져온다</summary>
        public static string Get(string key)
            => LocalizationSettings.StringDatabase.GetLocalizedString(UITable, key);

        /// <summary>현재 로케일의 UIStrings 값을 가져와 args로 서식을 채운다</summary>
        public static string Get(string key, params object[] args)
            => LocalizationSettings.StringDatabase.GetLocalizedString(UITable, key, args);
    }
}
