using UnityEngine;

namespace Game.Boot
{
    /// <summary>모바일은 기본 30fps로 도는 경우가 많아서 시작할 때 목표 프레임레이트를 정한다</summary>
    internal static class FrameRateSetup
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply() => Application.targetFrameRate = TargetFrameRate;
    }
}
