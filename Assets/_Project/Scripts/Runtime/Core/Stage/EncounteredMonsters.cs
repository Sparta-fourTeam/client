using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>이번 전투에서 한 번이라도 나온 몬스터. 분열·소환체도 센다. 결과를 제출할 때 서버가 도감에 등록하도록 함께 보낸다</summary>
    public sealed class EncounteredMonsters
    {
        private readonly List<int> _ids = new();

        public IReadOnlyList<int> Ids => _ids;

        public void Record(int monsterId)
        {
            if (!_ids.Contains(monsterId))
            {
                _ids.Add(monsterId);
            }
        }
    }
}
