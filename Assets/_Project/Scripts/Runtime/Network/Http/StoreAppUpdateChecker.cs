using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>Android는 Play 계정의 배포 트랙, iOS는 App Store 공개 버전을 조회한다.</summary>
    public sealed class StoreAppUpdateChecker : IAppUpdateChecker
    {
        private const int TimeoutSeconds = 5;
        private readonly IHttpTransport _transport;
        private bool _checked;

        public StoreAppUpdateChecker(IHttpTransport transport)
        {
            _transport = transport;
        }

        public async UniTask<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 로그아웃 등으로 Loading에 다시 들어와도 같은 실행에서 반복해 묻지 않는다.
            if (_checked)
            {
                return null;
            }

            _checked = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            return await CheckGooglePlayAsync(cancellationToken);
#elif UNITY_IOS && !UNITY_EDITOR
            string country;
            try
            {
                country = new RegionInfo(CultureInfo.CurrentCulture.Name).TwoLetterISORegionName;
            }
            catch (ArgumentException)
            {
                country = "KR";
            }

            var request = new HttpRequestData(HttpVerb.Get,
                $"https://itunes.apple.com/lookup?bundleId={Uri.EscapeDataString(Application.identifier)}&country={country}", null, null);
            var response = await _transport.Send(request, TimeoutSeconds, cancellationToken);
            return AppStoreUpdateParser.Parse(response, Application.identifier, Application.version);
#else
            // Editor·데스크톱은 스토어 업데이트 대상이 아니다.
            await UniTask.CompletedTask;
            return null;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static async UniTask<AppUpdateInfo> CheckGooglePlayAsync(CancellationToken cancellationToken)
        {
            // 공식 Play In-App Update 라이브러리를 JNI로 사용한다. Gradle 의존성은 StoreUpdate.androidlib에 둔다.
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var factory = new AndroidJavaClass("com.google.android.play.core.appupdate.AppUpdateManagerFactory");
            using var manager = factory.CallStatic<AndroidJavaObject>("create", activity);
            using var task = manager.Call<AndroidJavaObject>("getAppUpdateInfo");
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!task.Call<bool>("isComplete"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup >= deadline)
                {
                    return null;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!task.Call<bool>("isSuccessful"))
            {
                return null;
            }

            using var info = task.Call<AndroidJavaObject>("getResult");
            using var availability = new AndroidJavaClass("com.google.android.play.core.install.model.UpdateAvailability");
            return info.Call<int>("updateAvailability") == availability.GetStatic<int>("UPDATE_AVAILABLE")
                ? new AppUpdateInfo($"https://play.google.com/store/apps/details?id={Uri.EscapeDataString(Application.identifier)}")
                : null;
        }
#endif
    }

    public static class AppStoreUpdateParser
    {
        public static AppUpdateInfo Parse(HttpResponseData response, string bundleId, string installedVersion)
        {
            if (response.Failure != HttpFailure.None || response.StatusCode != 200 || string.IsNullOrWhiteSpace(response.Body))
            {
                return null;
            }

            LookupResponse result;
            try
            {
                result = JsonUtility.FromJson<LookupResponse>(response.Body);
            }
            catch (ArgumentException)
            {
                return null;
            }
            if (result?.results == null)
            {
                return null;
            }

            foreach (var app in result.results)
            {
                if (app != null && app.bundleId == bundleId && IsNewer(app.version, installedVersion)
                    && Uri.TryCreate(app.trackViewUrl, UriKind.Absolute, out var url)
                    && url.Scheme == Uri.UriSchemeHttps && url.Host == "apps.apple.com")
                {
                    return new AppUpdateInfo(url.AbsoluteUri);
                }
            }

            return null;
        }

        private static bool IsNewer(string available, string installed)
        {
            // 1.10은 1.9보다 크고, 1.0과 1.0.0은 같다. 잘못된 버전은 업데이트로 판단하지 않는다.
            if (!TryVersion(available, out var latest) || !TryVersion(installed, out var current))
            {
                return false;
            }

            for (int i = 0; i < Math.Max(latest.Length, current.Length); i++)
            {
                int left = i < latest.Length ? latest[i] : 0;
                int right = i < current.Length ? current[i] : 0;
                if (left != right)
                {
                    return left > right;
                }
            }

            return false;
        }

        private static bool TryVersion(string value, out int[] numbers)
        {
            numbers = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var parts = value.Split('.');
            numbers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        [Serializable]
        private sealed class LookupResponse
        {
            public AppRecord[] results;
        }

        [Serializable]
        private sealed class AppRecord
        {
            public string bundleId;
            public string version;
            public string trackViewUrl;
        }
    }
}
