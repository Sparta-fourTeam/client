using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Network
{
    /// <summary>기기에 저장되는 세이브 데이터 전체. LocalSaveStore가 파일로 직렬화한다</summary>
    [Serializable]
    public class LocalSave
    {
        /// <summary>닉네임. 비어 있는 이전 저장 파일은 LocalSaveStore가 불러올 때 만들어 채운다</summary>
        public string nickname;
        public WalletRow wallet = new();
        public int exp;
        public List<StageProgressRow> stageProgress = new() { new StageProgressRow { stageId = 1, clearRating = 0 } };
        public List<UpgradeRow> upgrades = new();
        public List<BattleRow> battles = new();
        public List<LedgerRow> ledger = new();
        /// <summary>마법북 소유량. 기존 저장 파일에서 누락된 items는 빈 목록이다.</summary>
        public List<ItemAmount> items = new();
        /// <summary>도감 등록 몬스터. 기존 저장 파일에서 누락된 codex는 빈 목록이다.</summary>
        public List<CodexRow> codex = new();
        /// <summary>완료한 튜토리얼 단계. 기본값이 null이어야 튜토리얼이 생기기 전 저장 파일을 LocalSaveStore가 알아본다</summary>
        public List<string> tutorials;
        /// <summary>일일·주간 퀘스트 진행과 연 상자. 주기가 바뀌면 LocalSaveStore가 불러올 때 비운다</summary>
        public QuestSave quests = new();
        /// <summary>일일상점을 다시 뽑은 횟수. 날이 바뀌면 LocalSaveStore가 불러올 때 비운다</summary>
        public ShopSave shop = new();
    }
}
