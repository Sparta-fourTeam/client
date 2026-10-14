using System;

namespace Game.Core
{
    /// <summary>이번 주기의 퀘스트 진행 한 줄. 진행이 생긴 퀘스트만 줄이 있고, 없는 퀘스트는 진행 0이다</summary>
    [Serializable]
    public class QuestProgressRow
    {
        public string questId;
        public int count;
        public bool claimed;
    }
}
