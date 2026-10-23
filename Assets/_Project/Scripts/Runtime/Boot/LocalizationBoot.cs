using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Boot
{
    /// <summary>프리팹에 박힌 한국어가 테이블 로드 전에 그려지는 틈이 없도록, 첫 씬이 켜지기 전에 Localization 초기화와 Preload 테이블 로드를 끝낸다</summary>
    internal static class LocalizationBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if !UNITY_WEBGL // WebGL은 동기 대기를 지원하지 않는다. SceneLoader가 커튼 뒤에서 대신 기다린다
            if (!LocalizationSettings.HasSettings) { return; }

            try
            {
                LocalizationSettings.InitializationOperation.WaitForCompletion();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
#endif
        }
    }
}
