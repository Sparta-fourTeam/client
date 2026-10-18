using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>플레이어 상태 전체 스냅샷. 증분이 아닌 최신 상태 전체로 덮어쓴다</summary>
    [Serializable]
    public class PlayerSnapshot
    {
        /// <summary>플레이어 닉네임. 프로필에서 선택적으로 변경하며 중복을 허용한다</summary>
        public string nickname;
        /// <summary>공개 표시 ID. 현재는 로컬 세이브 단위이며 로그인 자격 정보로 사용하지 않는다</summary>
        public string playerId;
        public int gold;
        /// <summary>잼. 일일상점·코인 상점에서 물품을 살 때 쓰고, 상단 재화 표시도 이 값을 쓴다</summary>
        public int gems;
        public int exp;
        public int energyStored;
        public string energyUpdatedAt;
        public List<StageProgressRow> stageProgress;
        public List<UpgradeRow> upgrades;
        /// <summary>스킬별·장비별 마법북 소유량. itemId당 한 행이며, 없는 아이템의 소유량은 0이다.</summary>
        public List<ItemAmount> items;
        /// <summary>도감에 등록된 몬스터. 전투에서 마주친 몬스터마다 한 행이며, 없는 몬스터는 아직 등록되지 않은 것이다.</summary>
        public List<CodexRow> codex;
        /// <summary>완료한 튜토리얼 ID (Tutorials.json)</summary>
        public List<string> tutorials;
        /// <summary>서버에 저장된 시청·완료·수령 구분. Local 스냅샷에는 없을 수 있다.</summary>
        public List<TutorialAccountState> tutorialStates;
        /// <summary>서버가 정한 기간키·다음 초기화·수령 점수. 기기 시각으로 갱신하지 않는다.</summary>
        public List<QuestAccountPeriod> questPeriods;
        /// <summary>이번 일일·주간 주기의 퀘스트 진행. 진행이 생긴 퀘스트만 줄이 있다</summary>
        public List<QuestProgressRow> quests;
        /// <summary>이번 주기에 연 퀘스트 보물상자 ID</summary>
        public List<string> questChests;
        /// <summary>오늘 일일상점 상태(다시 뽑은 횟수, 산 칸)</summary>
        public ShopState shop;
    }

    [Serializable]
    public class TutorialAccountState
    {
        public string tutorialId;
        public bool dialogueSeen;
        public bool completed;
        public bool rewardClaimed;
        public string dialogueSeenAt;
        public string completedAt;
    }

    [Serializable]
    public class QuestAccountPeriod
    {
        public QuestPeriod period;
        public string periodKey;
        public string nextResetAt;
        public string lastLoginDay;
        public int loginCount;
        public int spentEnergy;
        public long points;
    }
}
