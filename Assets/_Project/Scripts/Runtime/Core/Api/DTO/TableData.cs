using System;

namespace Game.Core
{
    /// <summary>테이블 하나의 원본 JSON. 도메인 모델 변환은 호출부 책임</summary>
    [Serializable]
    public class TableData
    {
        public string json;
    }
}
