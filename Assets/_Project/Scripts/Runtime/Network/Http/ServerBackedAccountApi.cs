using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>계정 연동·삭제를 서버로 보낸다. 삭제는 서버의 204 확인 뒤에만 로컬 저장을 지운다.
    /// 연동은 지금 로그인한 게스트 계정(액세스 토큰)에 외부 계정을 붙이는 것이라, 같은 유저가 외부 계정 유저로 바뀌고 데이터는 그대로 이어진다.
    /// 서버는 성공하면 204, 로그인하지 않았으면 401, 이미 연동된 계정이거나 그 외부 계정이 다른 유저에 연동돼 있으면 409를 준다
    /// (409 두 가지는 본문에 오류 코드가 없어 클라이언트가 구분하지 않는다)</summary>
    public sealed class ServerBackedAccountApi : IAccountApi
    {
        private const string LinkPath = "api/auth/social/link";

        private readonly HttpApiClient _client;
        private readonly LocalAccountApi _local;

        public ServerBackedAccountApi(HttpApiClient client, LocalAccountApi local)
        {
            _client = client;
            _local = local;
        }

        public async UniTask DeleteAccount()
        {
            await _client.Delete("api/auth/me");
            // 서버 삭제는 확정됐다. 로컬 삭제 실패는 저장소의 재시도 기록으로 복구하고,
            // AccountService의 인증·프로필 초기화와 로그인 전환은 계속 마친다.
            try { await _local.DeleteAccount(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public async UniTask LinkAccount(LoginMethod method, string accountId)
        {
            // accountId는 외부 로그인으로 받은 ID 토큰이다. 서버가 검증한다
            await _client.Post(LinkPath, new SocialLoginRequest { provider = ServerBackedAuthApi.ProviderOf(method), idToken = accountId });
            await _local.LinkAccount(method, accountId);
        }
    }
}
