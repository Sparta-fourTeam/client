namespace Game.Core
{
    /// <summary>Stage 씬이 로드된 뒤 어떤 스테이지인지 알아야 할 때 참조하는 공유 상태. Root 스코프에 싱글턴으로 둔다</summary>
    public sealed class StageContext
    {
        public int StageId { get; private set; }

        public void Set(int stageId) => StageId = stageId;
    }
}
