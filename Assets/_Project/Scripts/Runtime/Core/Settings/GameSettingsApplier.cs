using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Settings;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>GameSettings를 게임에 적용한다: 목표 프레임레이트, 데미지 숫자와 디버프 이펙트 표시, 언어(Localization 로케일).
    /// 시작할 때 한 번 적용하고 설정이 바뀔 때마다 다시 적용한다. 언어를 바꿔도 씬을 다시 로드하지 않는다.
    /// 코드가 쓴 라벨은 LocalizedLabel이 SelectedLocaleChanged에 맞춰 제자리에서 다시 계산한다</summary>
    public sealed class GameSettingsApplier : IStartable, IDisposable
    {
        private readonly GameSettings _settings;
        private CancellationTokenSource _localeCts;

        public GameSettingsApplier(GameSettings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            Apply();
            _settings.Changed += Apply;
        }

        public void Dispose()
        {
            _settings.Changed -= Apply;
            CancelLocale();
        }

        private void Apply()
        {
            Application.targetFrameRate = _settings.TargetFrameRate;
            DamagePopupPool.Enabled = _settings.DamageIndication;
            EnemyStatusEffects.Enabled = _settings.DebuffEffects;
            ApplyLocale();
        }

        private void ApplyLocale()
        {
            // 이전 요청이 초기화를 기다리는 중이면 최신 언어만 남기도록 취소한다
            CancelLocale();
            _localeCts = new CancellationTokenSource();
            ApplyLocaleAsync(_settings.Language, _localeCts.Token).Forget();
        }

        private void CancelLocale()
        {
            if (_localeCts == null) { return; }
            _localeCts.Cancel();
            _localeCts.Dispose();
            _localeCts = null;
        }

        /// <summary>Localization 초기화(로케일 로드)가 끝난 뒤에 SelectedLocale을 대입한다. 준비 전에 대입하면 로케일을 찾지 못한다</summary>
        private async UniTaskVoid ApplyLocaleAsync(GameLanguage language, CancellationToken ct)
        {
            try
            {
                if (!LocalizationSettings.HasSettings) { return; }
                // 핸들을 들고 있지 않고 매번 다시 읽는다: 로케일이 바뀔 때 Localization이 초기화 핸들을 해제한다
                await UniTask.WaitUntil(() => LocalizationSettings.InitializationOperation.IsDone, cancellationToken: ct);

                string code = GameLanguageLocale.Code(language);
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                if (locale == null)
                {
                    Debug.LogWarning($"Localization 로케일 '{code}'을 찾지 못했다");
                    return;
                }

                if (!ReferenceEquals(LocalizationSettings.SelectedLocale, locale))
                {
                    LocalizationSettings.SelectedLocale = locale;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
