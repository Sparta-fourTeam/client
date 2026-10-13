using System;

namespace Game.Core
{
    /// <summary>로그인 응답</summary>
    [Serializable]
    public class LoginResponse
    {
        public string accessToken;

        /// <summary>로그인 시점의 테이블 리비전. 클라이언트 테이블과 비교해 동기화 여부를 판단한다</summary>
        public int dataRevision;
    }
}
