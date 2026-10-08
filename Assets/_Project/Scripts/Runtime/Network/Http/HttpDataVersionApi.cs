using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>GET /api/data/version 응답. Apidog 명세(DataVersionResponse)의 모양 그대로다</summary>
    [Serializable]
    public class DataVersionResponse
    {
        /// <summary>전체 데이터 리비전. 클라이언트의 DataVersions에는 받을 자리가 없어 지금은 쓰지 않는다</summary>
        public int revision;

        /// <summary>테이블 이름별 리비전</summary>
        public Dictionary<string, int> tables;
    }

    /// <summary>서버에서 테이블별 리비전을 받아 온다. 테이블 본문(GetTable)은 서버 계약이 정해지면 IDataApi 구현으로 합친다.
    /// 서버가 준 테이블 이름을 그대로 담을 뿐 클라이언트의 테이블 이름으로 바꾸지 않는다.
    /// 서버(Cards)와 클라이언트(GeneralCards)의 이름이 다른 테이블은 이름을 맞추는 쪽에서 정해야 한다</summary>
    public sealed class HttpDataVersionApi
    {
        private const string VersionPath = "api/data/version";
        private readonly HttpApiClient _client;

        public HttpDataVersionApi(HttpApiClient client)
        {
            _client = client;
        }

        public async UniTask<DataVersions> GetVersions(CancellationToken cancellationToken = default)
        {
            var response = await _client.Get<DataVersionResponse>(VersionPath, cancellationToken);

            // 응답은 왔지만 tables가 빠졌다면 계약과 다른 응답이다. 빈 목록으로 보면 모든 테이블이 최신으로 보일 수 있다
            if (response.tables == null)
            {
                throw new ApiException(ApiErrorKind.Rejected, HttpErrorMapper.InvalidResponse);
            }

            return new DataVersions { revisions = new Dictionary<string, int>(response.tables) };
        }
    }
}
