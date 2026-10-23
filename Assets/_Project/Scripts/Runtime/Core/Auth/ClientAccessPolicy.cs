using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    public enum ClientAccessPurpose { Login, NewBattle, Resume }
    public enum ClientAccessState { Allowed, Advisory, UpdateRequired, Draining, Maintenance, Unavailable }

    public sealed class ClientBuild
    {
        public readonly string Platform, Build, Version;
        public ClientBuild(string platform, string build, string version = "")
        { Platform = platform; Build = build; Version = version; }
    }

    public sealed class ClientPlatformPolicy
    {
        public string minimumBuild, recommendedBuild, storeUrl;
    }

    /// <summary>서버와 nginx가 함께 제공하는 공개 정책. 데이터 테이블 버전과 구분한다.</summary>
    public sealed class ClientAccessPolicy
    {
        public int schemaVersion;
        public string revision, mode, message, estimatedEnd;
        public bool enforcementEnabled, allowLegacy;
        public int retryAfterSeconds;
        public Dictionary<string, ClientPlatformPolicy> platforms;

        public static bool ValidBuild(string value) => value != null && Regex.IsMatch(value, @"\A[0-9]{1,10}(\.[0-9]{1,10}){0,3}\z");
        public static int CompareBuild(string left, string right)
        {
            if (!ValidBuild(left) || !ValidBuild(right)) { throw new ArgumentException("Invalid build number."); }
            var a = left.Split('.'); var b = right.Split('.');
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int comparison = (i < a.Length ? long.Parse(a[i]) : 0).CompareTo(i < b.Length ? long.Parse(b[i]) : 0);
                if (comparison != 0) { return comparison; }
            }
            return 0;
        }

        public ClientAccessDecision Evaluate(ClientBuild build, ClientAccessPurpose purpose)
        {
            if (enforcementEnabled && mode == "MAINTENANCE")
            { return new ClientAccessDecision(ClientAccessState.Maintenance, message, estimatedEnd); }
            if (enforcementEnabled && mode == "DRAINING" && purpose != ClientAccessPurpose.Resume)
            { return new ClientAccessDecision(ClientAccessState.Draining, message, estimatedEnd); }
            if (enforcementEnabled && mode == "DRAINING" && purpose == ClientAccessPurpose.Resume)
            { return new ClientAccessDecision(ClientAccessState.Allowed); }
            platforms.TryGetValue(build.Platform ?? "", out var rule);
            if (enforcementEnabled && (rule == null || !ValidBuild(build.Build)
                || CompareBuild(build.Build, rule.minimumBuild) < 0))
            { return new ClientAccessDecision(ClientAccessState.UpdateRequired, message, null, rule?.storeUrl); }
            if (purpose == ClientAccessPurpose.Login && rule != null && ValidBuild(build.Build)
                && CompareBuild(build.Build, rule.recommendedBuild) < 0)
            { return new ClientAccessDecision(ClientAccessState.Advisory, message, null, rule.storeUrl); }
            return new ClientAccessDecision(ClientAccessState.Allowed);
        }

        public static ClientAccessPolicy Legacy() => new ClientAccessPolicy
        {
            schemaVersion = 1,
            revision = "legacy-endpoint",
            mode = "NORMAL",
            message = "",
            retryAfterSeconds = 30,
            allowLegacy = true,
            platforms = new Dictionary<string, ClientPlatformPolicy>(),
        };
    }

    public sealed class ClientAccessDecision
    {
        public readonly ClientAccessState State;
        public readonly string Message, EstimatedEnd, StoreUrl;
        public bool Blocking => State != ClientAccessState.Allowed && State != ClientAccessState.Advisory;
        public ClientAccessDecision(ClientAccessState state, string message = "", string estimatedEnd = null, string storeUrl = null)
        { State = state; Message = message; EstimatedEnd = estimatedEnd; StoreUrl = storeUrl; }
    }

    public interface IClientPolicyClient
    {
        ClientBuild Build { get; }
        UniTask<ClientAccessPolicy> Fetch(CancellationToken cancellationToken);
    }

    public interface IClientAccessPrompt
    {
        void SetChecking(bool checking);
        UniTask<bool> Show(ClientAccessDecision decision, CancellationToken cancellationToken);
        void OpenStore(string url);
        void Recheck();
    }

    public interface IClientAccessGuard
    {
        UniTask EnsureAllowed(ClientAccessPurpose purpose, CancellationToken cancellationToken = default);
    }

    /// <summary>버튼·닫기·스토어 이동은 허가가 아니다. 새 정책이 허용할 때만 호출자를 통과시킨다.</summary>
    public sealed class ClientAccessGuard : IClientAccessGuard
    {
        private readonly IClientPolicyClient _client;
        private readonly IClientAccessPrompt _prompt;
        private readonly IAppUpdateChecker _advisory;
        private readonly SemaphoreSlim _checks = new(1, 1);
        private bool _advisoryShown;
        private int _recheckGeneration;
        public bool IsChecking { get; private set; }

        public ClientAccessGuard(IClientPolicyClient client, IClientAccessPrompt prompt, IAppUpdateChecker advisory)
        { _client = client; _prompt = prompt; _advisory = advisory; }

        public void Recheck() { _recheckGeneration++; _prompt.Recheck(); }

        public async UniTask EnsureAllowed(ClientAccessPurpose purpose, CancellationToken cancellationToken = default)
        {
            await _checks.WaitAsync(cancellationToken);
            IsChecking = true;
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int generation = _recheckGeneration;
                    ClientAccessDecision decision;
                    _prompt.SetChecking(true);
                    try
                    { decision = (await _client.Fetch(cancellationToken).AttachExternalCancellation(cancellationToken)).Evaluate(_client.Build, purpose); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception)
                    { decision = new ClientAccessDecision(ClientAccessState.Unavailable, "서버 상태를 확인하지 못했습니다. 다시 시도해 주세요."); }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (generation != _recheckGeneration) { continue; }
                    _prompt.SetChecking(false);
                    if (!decision.Blocking)
                    {
                        if (purpose == ClientAccessPurpose.Login && !_advisoryShown)
                        {
                            _advisoryShown = true;
                            if (decision.State != ClientAccessState.Advisory)
                            {
                                // 스토어 조회 실패는 권장 안내에만 영향을 준다. 정책 조회 실패는 이 분기에 오지 않는다.
                                try
                                {
                                    var update = await _advisory.CheckAsync(cancellationToken);
                                    if (update != null)
                                    { decision = new ClientAccessDecision(ClientAccessState.Advisory, storeUrl: update.StoreUrl); }
                                }
                                catch (OperationCanceledException) { throw; }
                                catch (Exception) { /* 권장 업데이트는 기존 동작대로 건너뛸 수 있다. */ }
                            }
                            if (decision.State == ClientAccessState.Advisory
                                && await _prompt.Show(decision, cancellationToken))
                            { cancellationToken.ThrowIfCancellationRequested(); _prompt.OpenStore(decision.StoreUrl); }
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (generation != _recheckGeneration) { continue; }
#if NOVA_POLICY_QA && !UNITY_EDITOR
                        UnityEngine.Debug.Log($"QA_GATE_GRANTED purpose={purpose} generation={generation} frame={UnityEngine.Time.frameCount}");
#endif
                        return;
                    }

                    bool confirmed = await _prompt.Show(decision, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (confirmed && decision.State == ClientAccessState.UpdateRequired && !string.IsNullOrEmpty(decision.StoreUrl))
                    { _prompt.OpenStore(decision.StoreUrl); }
                    // 닫힘/foreground 재검사/업데이트 버튼 모두 정책을 다시 읽는다. 로그인이나 pending result는 변경하지 않는다.
                }
            }
            finally { _prompt.SetChecking(false); IsChecking = false; _checks.Release(); }
        }
    }
}
