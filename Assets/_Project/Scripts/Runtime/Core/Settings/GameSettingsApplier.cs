using System;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core
{
    /// <summary>GameSettings를 게임에 적용한다: 목표 프레임레이트, 데미지 숫자와 디버프 이펙트 표시.
    /// 시작할 때 한 번 적용하고 설정이 바뀔 때마다 다시 적용한다</summary>
    public sealed class GameSettingsApplier : IStartable, IDisposable
    {
        private readonly GameSettings _settings;

        public GameSettingsApplier(GameSettings settings)
        {
            _settings = settings;
        }

        public void Start()
        {
            Apply();
            _settings.Changed += Apply;
        }

        public void Dispose() => _settings.Changed -= Apply;

        private void Apply()
        {
            Application.targetFrameRate = _settings.TargetFrameRate;
            DamagePopupPool.Enabled = _settings.DamageIndication;
            EnemyStatusEffects.Enabled = _settings.DebuffEffects;
        }
    }
}
