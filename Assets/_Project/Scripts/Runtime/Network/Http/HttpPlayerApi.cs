using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Game.Core;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>서버의 GET /api/user/me로 플레이어 상태를 받는다 (서버 PR #47).
    /// 진행 조회에 프로필 API의 최신 닉네임·초상을 합친다.
    /// #78/#79 문자열 튜토리얼과 서버 퀘스트 보드를 복원한다. 부분 응답은 적용하지 않는다.</summary>
    public sealed class HttpPlayerApi : IPlayerApi
    {
        private const string MePath = "api/user/me";

        private readonly HttpApiClient _client;
        private readonly LocalPlayerApi _local;
        private readonly IAuthTokenStore _tokens;
        private readonly GameDataStore _data;
        private readonly HttpBattleSourceApi _source;
        private readonly HttpDataVersionApi _versions;
        private readonly IProfileApi _profiles;
        private sealed class PlayerLevelsResponse
        { public int? baseExp, stepExp, maxLevel, version; }

        public HttpPlayerApi(HttpApiClient client, LocalPlayerApi local, IAuthTokenStore tokens)
        {
            _client = client ?? throw new System.ArgumentNullException(nameof(client));
            _local = local ?? throw new System.ArgumentNullException(nameof(local));
            _tokens = tokens ?? throw new System.ArgumentNullException(nameof(tokens));
        }

        /// <summary>실제 앱 경로: 서버 전투 원본과 버전을 먼저 검증하고 서버 계정만 복원한다.</summary>
        public HttpPlayerApi(HttpApiClient client, IAuthTokenStore tokens, GameDataStore data,
            HttpBattleSourceApi source, HttpDataVersionApi versions, IProfileApi profiles = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _versions = versions ?? throw new ArgumentNullException(nameof(versions));
            _profiles = profiles;
        }

        public async UniTask<PlayerSnapshot> GetMe()
        {
            long generation = _tokens.Generation;
            EnsureSession(generation);
            if (_source != null)
            {
                _data.InvalidateServerPlayerLevels();
                // 미게시(503)나 원문 불일치는 로그인 재시도 화면에 남긴다. Local JSON으로 진행하지 않는다.
                var downloaded = await _source.LoadGameData(_data.CopyTables());
                EnsureSession(generation);
                var versions = await _versions.GetVersions();
                EnsureSession(generation);
                var levels = await _client.Get<PlayerLevelsResponse>("api/player-levels", isValid: value =>
                    value.baseExp > 0 && value.stepExp >= 0 && value.maxLevel >= 2
                    && value.maxLevel <= PlayerLevelConfig.MaxSupportedLevel && value.version > 0);
                EnsureSession(generation);
                try
                {
                    downloaded.ApplyServerPlayerLevels(new PlayerLevelConfig
                    { BaseExp = levels.baseExp.Value, StepExp = levels.stepExp.Value, MaxLevel = levels.maxLevel.Value }, levels.version.Value);
                }
                catch (Exception error) when (error is InvalidOperationException or OverflowException or ArgumentException)
                { throw ServerProgressContract.Invalid("GET api/player-levels apply"); }
                _data.ApplyBattleSource(downloaded, versions);
                _data.ApplyServerPlayerLevels(new PlayerLevelConfig
                { BaseExp = levels.baseExp.Value, StepExp = levels.stepExp.Value, MaxLevel = levels.maxLevel.Value }, levels.version.Value);
            }
            var json = await _client.GetJson(MePath);
            EnsureSession(generation);
            var data = WithMePath(() => ServerProgressContract.Parse(() => ServerProgressContract.ReadMe(json)));
            PlayerSnapshot snapshot;
            if (_source == null)
            {
                var local = await _local.GetMe();
                snapshot = WithMePath(() => ServerUserMapper.ToSnapshot(data, local.nickname, local.playerId));
                snapshot.avatarId = local.avatarId;
            }
            else
            {
                string subject = ServerAccountIdentity.Subject(_tokens.AccessToken);
                snapshot = WithMePath(() => ServerUserMapper.ToSnapshot(data, "플레이어", subject));
            }
            EnsureSession(generation);
            if (_profiles != null)
            {
                var identity = await _profiles.GetProfile();
                EnsureSession(generation);
                snapshot.nickname = identity.Nickname;
                snapshot.avatarId = identity.AvatarId;
            }
            return snapshot;
        }

        // INVALID_RESPONSE에 요청 경로를 붙여 진단 로그에서 어느 응답이 문제인지 보이게 한다 (값은 제외)
        private static T WithMePath<T>(Func<T> read)
        {
            try { return read(); }
            catch (ApiException error) when (error.Code == HttpErrorMapper.InvalidResponse)
            { throw new ApiException(error.Kind, error.Code, $"GET {MePath} {error.Detail}"); }
        }

        private void EnsureSession(long generation)
        {
            if (_tokens.Generation != generation || string.IsNullOrEmpty(_tokens.AccessToken))
            { throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.SessionExpired); }
        }
    }

    /// <summary>서버 JWT의 subject는 DB 계정 ID다. 로컬 저장 ID나 닉네임을 계정 경계로 쓰지 않는다.</summary>
    internal static class ServerAccountIdentity
    {
        public static string Subject(string accessToken)
        {
            try
            {
                var parts = accessToken.Split('.');
                if (parts.Length != 3) { throw new FormatException(); }
                string encoded = parts[1].Replace('-', '+').Replace('_', '/');
                encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
                var subject = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))).Value<string>("sub");
                if (!long.TryParse(subject, out long id) || id <= 0) { throw new FormatException(); }
                return subject;
            }
            catch (Exception error) when (error is FormatException or ArgumentException or Newtonsoft.Json.JsonException)
            { throw ServerProgressContract.Invalid("access token subject"); }
        }
    }
}
