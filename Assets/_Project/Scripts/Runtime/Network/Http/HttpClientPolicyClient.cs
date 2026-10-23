using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    public sealed class HttpClientPolicyClient : IClientPolicyClient
    {
        private readonly HttpApiConfig _config;
        private readonly IHttpTransport _transport;
        private bool _supported;
        private readonly bool _enabled;
        public ClientBuild Build => GameClientIdentity.Current;
        public HttpClientPolicyClient(HttpApiConfig config, IHttpTransport transport, bool enabled = true)
        { _config = config; _transport = transport; _enabled = enabled; }

        public async UniTask<ClientAccessPolicy> Fetch(CancellationToken cancellationToken)
        {
            if (!_enabled) { return ClientAccessPolicy.Legacy(); }
            var headers = GameClientIdentity.Headers();
            headers["Cache-Control"] = "no-cache";
            var response = await _transport.Send(new HttpRequestData(HttpVerb.Get,
                _config.BaseUrl + "/api/client/status", headers, null), _config.TimeoutSeconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_supported && response.Failure == HttpFailure.None && response.StatusCode == 404)
            { return ClientAccessPolicy.Legacy(); } // 최초 서버 rollout 전의 호환 경로만 허용.
            if (response.Failure != HttpFailure.None || response.StatusCode != 200)
            { throw new ApiException(ApiErrorKind.Transient, "CLIENT_POLICY_UNAVAILABLE"); }
            ClientAccessPolicy policy;
            try
            {
                var json = JObject.Parse(response.Body ?? "");
                if (json["enforcementEnabled"]?.Type != JTokenType.Boolean || json["allowLegacy"]?.Type != JTokenType.Boolean)
                { throw new FormatException(); }
                policy = json.ToObject<ClientAccessPolicy>();
                Validate(policy);
            }
            catch (Exception error) when (error is JsonException or FormatException or ArgumentException)
            { throw new ApiException(ApiErrorKind.Transient, "CLIENT_POLICY_INVALID"); }
#if NOVA_POLICY_LOCAL && NOVA_POLICY_QA && !UNITY_EDITOR
            UnityEngine.Debug.Log($"QA_HTTP_POLICY revision={policy.revision} mode={policy.mode} enforced={policy.enforcementEnabled}");
#endif
            _supported = true;
            return policy;
        }

        public static void Validate(ClientAccessPolicy policy)
        {
            if (policy == null || policy.schemaVersion != 1 || string.IsNullOrWhiteSpace(policy.revision)
                || !System.Text.RegularExpressions.Regex.IsMatch(policy.revision, @"\A[A-Za-z0-9._:-]{1,128}\z")
                || (policy.mode != "NORMAL" && policy.mode != "DRAINING" && policy.mode != "MAINTENANCE")
                || policy.message == null || policy.platforms == null || policy.retryAfterSeconds < 1 || policy.retryAfterSeconds > 3600)
            { throw new FormatException(); }
            foreach (var entry in policy.platforms)
            {
                var rule = entry.Value;
                if (rule == null || !ClientAccessPolicy.ValidBuild(rule.minimumBuild) || !ClientAccessPolicy.ValidBuild(rule.recommendedBuild)
                    || ClientAccessPolicy.CompareBuild(rule.recommendedBuild, rule.minimumBuild) < 0
                    || !ValidStore(entry.Key, rule.storeUrl))
                { throw new FormatException(); }
            }
        }

        private static bool ValidStore(string platform, string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)
            && ((platform == "android" && uri.Host == "play.google.com") || (platform == "ios" && uri.Host == "apps.apple.com")
                || (platform == "editor" && uri.Host == "example.invalid"));
    }
}
