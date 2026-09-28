using System;

namespace Game.Core
{
    /// <summary>전투 발급 응답</summary>
    [Serializable]
    public class StartBattleResponse
    {
        public string battleId;

        /// <summary>이 전투의 난수 시드. 클라이언트가 서버와 같은 결과를 재현하는 데 쓰인다</summary>
        public int seed;
    }
}
