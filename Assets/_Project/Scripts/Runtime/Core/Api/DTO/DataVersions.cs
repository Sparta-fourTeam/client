using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>테이블 이름별 최신 리비전. 로컬 캐시와 비교해 DATA_OUTDATED를 판정하는 데 쓰인다</summary>
    [Serializable]
    public class DataVersions
    {
        public Dictionary<string, int> revisions;
    }
}
