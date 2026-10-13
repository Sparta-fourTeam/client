using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>게임 테이블(마스터 데이터) 동기화를 담당하는 API</summary>
    public interface IDataApi
    {
        /// <summary>테이블별 최신 리비전을 받는다 (DATA_OUTDATED 판정에 사용)</summary>
        UniTask<DataVersions> GetVersions();

        /// <summary>테이블 하나를 원본 JSON으로 받는다. 도메인 모델 변환은 호출부 책임</summary>
        UniTask<TableData> GetTable(string name);
    }
}
