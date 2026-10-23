using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using VContainer;

namespace Game.Boot
{
    /// <summary>복귀 시 상태를 다시 읽는다. 기존 전투는 종료/로그아웃시키지 않고 정상 Pause/Resume만 사용한다.</summary>
    public sealed class ClientPolicyForeground : MonoBehaviour
    {
        private ClientAccessGuard _guard;
        private bool _checking;
        public void Initialize(ClientAccessGuard guard) => _guard = guard;
        private void OnApplicationFocus(bool focused)
        {
#if NOVA_POLICY_QA && !UNITY_EDITOR
            Debug.Log($"QA_FOREGROUND focus={focused} frame={Time.frameCount} checking={_guard?.IsChecking}");
#endif
            if (focused) { Check(); }
        }
        private void OnApplicationPause(bool paused)
        {
#if NOVA_POLICY_QA && !UNITY_EDITOR
            Debug.Log($"QA_FOREGROUND paused={paused} frame={Time.frameCount} checking={_guard?.IsChecking}");
#endif
            if (!paused) { Check(); }
        }

        private void Check()
        {
            if (_guard == null) { return; }
            if (_guard.IsChecking) { _guard.Recheck(); return; }
            if (_checking) { return; }
            // Boot에는 아직 팝업이 없다. Loading 진입 검사가 그 단계를 담당한다.
            if (UnityEngine.Object.FindAnyObjectByType<Game.View.ConfirmPopupView>(FindObjectsInactive.Include) == null) { return; }
            CheckAsync().Forget(Debug.LogException);
        }

        private async UniTask CheckAsync()
        {
            _checking = true;
            StageLifetimeScope scope = null;
            StageManager stage = null;
            bool pausedHere = false;
            bool allowed = false;
            try
            {
                scope = UnityEngine.Object.FindAnyObjectByType<StageLifetimeScope>();
                stage = scope != null && scope.Container != null ? scope.Container.Resolve<StageManager>() : null;
                pausedHere = stage != null && stage.State == StageState.Playing;
                if (pausedHere) { stage.Pause(); }
                await _guard.EnsureAllowed(stage != null ? ClientAccessPurpose.Resume : ClientAccessPurpose.Login,
                    this.GetCancellationTokenOnDestroy());
                allowed = true;
            }
            catch (OperationCanceledException) { /* 종료/씬 수명 취소는 허가로 바꾸지 않는다. */ }
            finally
            {
                if (allowed && pausedHere && scope != null && stage.State == StageState.Paused) { stage.Resume(); }
                _checking = false;
            }
        }
    }
}
