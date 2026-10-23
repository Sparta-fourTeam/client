using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>로케일이 바뀌면 GameDataStore의 표시 문구(스킬·카드·아이템 이름·설명, 튜토리얼 대사)를 새 언어로 다시 적용한다.
    /// 순서 가정: Root 스코프의 엔트리포인트라 다른 어떤 LocalizedLabel보다 먼저 SelectedLocaleChanged를 구독한다.
    /// 그래서 라벨이 갱신되는 시점에는 데이터가 이미 새 언어로 바뀌어 있다. RootLifetimeScope에서 GameSettingsApplier보다 앞에 등록한다</summary>
    public sealed class DisplayTextLocalizer : IStartable, IDisposable
    {
        private readonly GameDataStore _data;

        public DisplayTextLocalizer(GameDataStore data) => _data = data;

        public void Start() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        public void Dispose() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        private void OnLocaleChanged(Locale locale)
        {
            if (locale != null) { _data.ApplyDisplayLocale(locale.Identifier.Code); }
        }
    }
}
