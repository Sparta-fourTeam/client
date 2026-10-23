#if NOVA_POLICY_QA && !UNITY_EDITOR
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>전용 APK의 오프라인 UI fixture. 실제 서버/스토어 버전 검증을 대신하지 않는다.</summary>
    public sealed class ClientPolicyQaClient : IClientPolicyClient, IAppUpdateChecker
    {
        private int _reads;
        public ClientBuild Build => GameClientIdentity.Current;

        public async UniTask<ClientAccessPolicy> Fetch(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = ++_reads;
            Debug.Log($"QA_POLICY_FETCH_BEGIN read={read} frame={Time.frameCount}");
            // 실제 HTTP처럼 최소 한 player-loop를 거친다. onNewIntent/focus의 동기 fixture 경쟁을 피한다.
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            int.TryParse(IntentValue("qa_policy_delay_ms"), out int delay);
            delay = Mathf.Clamp(delay, 0, 8000);
            if (delay > 0) { await UniTask.Delay(delay, ignoreTimeScale: true, cancellationToken: cancellationToken); }
            string scenario = Scenario();
            Debug.Log($"QA_POLICY_FETCH_END read={read} case={scenario} delayMs={delay} frame={Time.frameCount}");
            if (scenario == "lookup-failure")
            { throw new ApiException(ApiErrorKind.Transient, "QA_SYNTHETIC_POLICY_FAILURE"); }
            string minimum = scenario == "required" ? "9999999999" : "0";
            return new ClientAccessPolicy
            {
                schemaVersion = 1, revision = "offline-qa-" + scenario,
                enforcementEnabled = scenario != "recommended" && scenario != "normal",
                mode = scenario == "maintenance" ? "MAINTENANCE" : scenario == "draining" ? "DRAINING" : "NORMAL",
                message = "[오프라인 QA] 합성 정책 화면입니다. 운영 서버에는 연결하지 않습니다.",
                retryAfterSeconds = 30, allowLegacy = true,
                platforms = new Dictionary<string, ClientPlatformPolicy>
                {
                    ["android"] = new ClientPlatformPolicy
                    {
                        minimumBuild = minimum,
                        recommendedBuild = scenario == "recommended" ? "9999999999" : minimum,
                        storeUrl = "https://play.google.com/store/apps/details?id=com.teamdoodle.doodledefense",
                    },
                },
            };
        }

        // 기존 store 조회의 추가 외부 호출 없이 정책 UI만 확인한다.
        public UniTask<AppUpdateInfo> CheckAsync(CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return UniTask.FromResult<AppUpdateInfo>(null); }

        private static string Scenario()
        {
            string value = IntentValue("qa_policy_case");
            return value is "required" or "recommended" or "normal" or "draining" or "lookup-failure"
                ? value : "maintenance";
        }

        private static string IntentValue(string key)
        {
#if UNITY_ANDROID
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            { return intent.Call<string>("getStringExtra", key); }
#else
            return null;
#endif
        }
    }
}
#endif
