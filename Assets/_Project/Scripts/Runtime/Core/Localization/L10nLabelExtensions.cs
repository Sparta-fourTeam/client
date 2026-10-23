using System;
using TMPro;

namespace Game.Core
{
    /// <summary>TMP_Text에 로케일을 따라가는 문구를 넣는 확장 메서드. 코드가 쓴 문구가 언어를 바꿔도 제자리에서 다시 계산된다</summary>
    public static class L10nLabelExtensions
    {
        /// <summary>UIStrings 키로 문구를 넣는다. 로케일이 바뀌면 같은 키와 인자로 다시 조회한다. 인자는 호출 시점의 값을 그대로 보관한다</summary>
        public static void SetL10n(this TMP_Text label, string key, params object[] args)
        {
            if (label == null) { return; }
            if (args == null || args.Length == 0)
            {
                SetL10n(label, () => L10n.Get(key));
                return;
            }
            SetL10n(label, () => L10n.Get(key, args));
        }

        /// <summary>생산자가 만든 문구를 넣는다. 로케일이 바뀌면 생산자를 다시 호출한다. 생산자가 읽는 값도 호출 시점의 캡처를 따른다</summary>
        public static void SetL10n(this TMP_Text label, Func<string> producer)
        {
            if (label == null) { return; }
            var localized = label.GetComponent<LocalizedLabel>();
            if (localized == null) { localized = label.gameObject.AddComponent<LocalizedLabel>(); }
            localized.Bind(label, producer);
        }
    }
}
