using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>키로 찾는 테이블. 없는 키를 조회하면 UNKNOWN_DATA_ID로 거절한다</summary>
    public sealed class Table<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _map;

        public Table(Dictionary<TKey, TValue> map)
        {
            _map = map;
        }

        public TValue GetOrThrow(TKey key) =>
            _map.TryGetValue(key, out var v) ? v : throw new ApiException(ApiErrorKind.Rejected, "UNKNOWN_DATA_ID");

        public bool Contains(TKey key) => _map.ContainsKey(key);
    }
}
