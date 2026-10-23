using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>표시 버전과 설치 build 번호를 구분한다. Editor는 실제 배포 앱 버전을 가장하지 않는다.</summary>
    public static class GameClientIdentity
    {
        private static ClientBuild _current;
        public static ClientBuild Current => _current ??= Read();

        public static Dictionary<string, string> Headers()
        {
            var identity = Current;
            return new Dictionary<string, string>
            {
                ["X-Game-Platform"] = identity.Platform,
                ["X-Game-Build"] = identity.Build,
                ["X-Game-Version"] = identity.Version,
            };
        }

        private static ClientBuild Read()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var manager = activity.Call<AndroidJavaObject>("getPackageManager");
            using var info = manager.Call<AndroidJavaObject>("getPackageInfo", Application.identifier, 0);
            using var sdk = new AndroidJavaClass("android.os.Build$VERSION");
            long build = sdk.GetStatic<int>("SDK_INT") >= 28 ? info.Call<long>("getLongVersionCode") : info.Get<int>("versionCode");
            return new ClientBuild("android", build.ToString(System.Globalization.CultureInfo.InvariantCulture), Application.version);
#elif UNITY_IOS && !UNITY_EDITOR
            return new ClientBuild("ios", System.Runtime.InteropServices.Marshal.PtrToStringAnsi(NovaClientBuildNumber()), Application.version);
#else
            return new ClientBuild("editor", "0", Application.version);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern System.IntPtr NovaClientBuildNumber();
#endif
    }
}
