using Cysharp.Threading.Tasks;
using Game.Core;

namespace Game.Network
{
    /// <summary>GameDataStore가 이미 읽어둔 테이블을 그대로 돌려주는 Mock 구현</summary>
    public sealed class LocalDataApi : IDataApi
    {
        private readonly GameDataStore _data;

        public LocalDataApi(GameDataStore data)
        {
            _data = data;
        }

        public UniTask<DataVersions> GetVersions() =>
            UniTask.FromResult(new DataVersions { revisions = _data.Revisions });

        public UniTask<TableData> GetTable(string name) =>
            UniTask.FromResult(new TableData { json = _data.RawJson(name) });
    }
}
