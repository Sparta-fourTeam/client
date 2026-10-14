using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    /// <summary>서버로 로그인(인증)하고, 이어서 기존 로컬 로그인을 한다.
    /// 서버에는 아직 인증 외의 플레이어 API가 없어서 게임 데이터(저장 파일)는 로컬이 계속 맡는다.
    /// 서버 로그인에 실패하면 예외를 던지고 로컬 로그인은 하지 않는다 (로그인창의 [다시 시도]가 다시 부른다).
    /// 경로는 서버 AuthController 기준이다: 게스트 가입·로그인, 구글·애플 소셜 로그인</summary>
    public sealed class ServerBackedAuthApi : IAuthApi
    {
        private const string SocialLoginPath = "api/auth/social/login";
        private const string GuestSignUpPath = "api/auth/guest/signup";
        private const string GuestLoginPath = "api/auth/guest/login";

        private readonly HttpApiClient _client;
        private readonly AuthSession _session;
        private readonly LocalAuthApi _local;

        public ServerBackedAuthApi(HttpApiClient client, AuthSession session, LocalAuthApi local)
        {
            _client = client;
            _session = session;
            _local = local;
        }

        public async UniTask<LoginResponse> Login(LoginMethod method, string accountId)
        {
            // 토큰이 아닌 요청 종류와 결과만 남긴다. 실패는 호출한 쪽(LoginFlow)이 예외로 기록한다
            Debug.Log($"[Auth] 서버 로그인 요청: {method}");
            var response = method == LoginMethod.Guest ? await GuestLoginAsync(accountId) : await SocialLoginAsync(method, accountId);
            _session.Set(response.accessToken, response.refreshToken);
            Debug.Log($"[Auth] 서버 로그인 성공: {method}");
            await _local.Login(method, accountId);
            return response;
        }

        // 저장된 게스트 ID가 있으면 그 ID로 로그인하고, 없거나 서버가 모르는 ID(다른 서버에서 만든 ID, 옛 형식)면 새로 가입한다.
        // 가입하면 새 게스트 ID가 생겨 계정이 바뀐다. 서버에는 게스트 데이터가 없어 잃는 것은 서버 쪽 신원뿐이다
        private async UniTask<LoginResponse> GuestLoginAsync(string guestId)
        {
            if (!string.IsNullOrEmpty(guestId))
            {
                try
                {
                    Debug.Log("[Auth] POST guest/login (저장된 게스트 ID)");
                    var tokens = await _client.Post<GuestLoginRequest, AuthTokens>(GuestLoginPath,
                        new GuestLoginRequest { guestId = guestId }, isValid: HasTokens);
                    return ToResponse(tokens, guestId);
                }
                catch (ApiException error) when (IsUnknownGuest(error))
                {
                    Debug.Log($"[Auth] 서버가 저장된 게스트 ID를 모른다({error.Code}) → 새로 가입");
                }
            }

            Debug.Log("[Auth] POST guest/signup");

            var signUp = await _client.Post<object, GuestSignUpResponse>(GuestSignUpPath, new object(),
                isValid: r => !string.IsNullOrEmpty(r.guestId) && HasTokens(r.tokens));
            return ToResponse(signUp.tokens, signUp.guestId);
        }

        // 외부 계정의 ID 토큰을 서버에 보내 검증받는다. 처음 로그인하는 계정이면 서버가 가입시킨다
        private async UniTask<LoginResponse> SocialLoginAsync(LoginMethod method, string idToken)
        {
            Debug.Log($"[Auth] POST social/login ({method})");
            var tokens = await _client.Post<SocialLoginRequest, AuthTokens>(SocialLoginPath,
                new SocialLoginRequest { provider = ProviderOf(method), idToken = idToken }, isValid: HasTokens);
            return ToResponse(tokens, null);
        }

        private static string ProviderOf(LoginMethod method) => method switch
        {
            LoginMethod.Google => "GOOGLE",
            LoginMethod.Apple => "APPLE",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "서버에 소셜 로그인으로 보낼 수 없는 방식입니다"),
        };

        // 서버는 UUID가 아닌 ID에 400, 일치하는 게스트가 없으면 401을 돌려준다.
        // 이 판단은 상태 코드(HTTP_400, HTTP_401)에 기대므로, 오류 코드를 서버 본문에서 읽게 바꾸면 이 비교도 함께 고쳐야 한다
        private static bool IsUnknownGuest(ApiException error) =>
            error.Kind == ApiErrorKind.Rejected && (error.Code == "HTTP_401" || error.Code == "HTTP_400");

        private static bool HasTokens(AuthTokens tokens) =>
            tokens != null && !string.IsNullOrEmpty(tokens.accessToken) && !string.IsNullOrEmpty(tokens.refreshToken);

        private static LoginResponse ToResponse(AuthTokens tokens, string accountId) => new()
        {
            accessToken = tokens.accessToken,
            refreshToken = tokens.refreshToken,
            accountId = accountId,
        };
    }
}
