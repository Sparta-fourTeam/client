using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Stage 씬이 로드된 뒤 어떤 스테이지인지 알아야 할 때 참조하는 공유 상태. Root 스코프에 싱글턴으로 둔다</summary>
    public sealed class StageContext
    {
        public int StageId { get; private set; }
        public BattleCheckpoint Checkpoint { get; set; }
        public string ResumeError { get; set; }

        public string BattleId { get; private set; }

        /// <summary>전투를 발급받은 직후의 레벨·클리어 기록. 서버 전투는 결과를 받을 때 프로필이 먼저 바뀌므로,
        /// 로비가 돌아와서는 전투 전 값을 알 수 없어 여기에 남겨 두고 로비가 한 번 가져간다</summary>
        public PreBattleState PreBattle { get; set; }

        public PreBattleState TakePreBattle()
        {
            var state = PreBattle;
            PreBattle = null;
            return state;
        }

        public void Set(int stageId) => StageId = stageId;

        public void SetBattle(string battleId)
        {
            Checkpoint = null;
            BattleId = battleId;
        }
    }
}

namespace Game.Core
{
    /// <summary>전투 전 플레이어의 레벨과 깬 스테이지. 전투에서 돌아온 로비가 레벨업 팝업과 해금 연출을 전투 전과 비교할 때 쓴다</summary>
    public sealed class PreBattleState
    {
        public int Level;
        public HashSet<int> ClearedStages = new();
    }
}
