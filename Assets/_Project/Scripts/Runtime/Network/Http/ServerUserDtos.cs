using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Game.Network
{
    /// <summary>GET /api/user/me 응답. 서버 UserDataResponse와 필드 이름이 같다 (서버 PR #47).
    /// 튜토리얼은 #78 문자열 ID, 퀘스트는 #79 서버 기간별 보드다. 변환 규칙은 ServerUserMapper에 있다.</summary>
    [Serializable]
    public class ServerUserData
    {
        public int gold;

        /// <summary>PR #58 이후 서버 필드. 0도 유효하므로 누락과 구분한다.</summary>
        public int? gem;

        /// <summary>이전 배포의 오타 필드. gem이 없을 때만 호환용으로 읽는다.</summary>
        public int jem;

        /// <summary>서버가 null을 줄 수 있다</summary>
        public int? exp;

        public int energyStored;

        /// <summary>서버 UTC Instant(ISO8601 Z). EnergyRule.ParseUpdatedAt으로 UTC 시각을 읽는다</summary>
        public string energyUpdatedAt;

        public List<ServerStageProgress> stageProgress;
        public List<ServerUpgrade> upgrades;
        public List<ServerItem> items;
        public List<ServerCodex> codex;
        public JToken shop;
        public List<string> tutorials;
        public List<ServerTutorialState> tutorialStates;
        public List<ServerQuestBoard> quests;
    }

    [Serializable]
    public class ServerStageProgress
    {
        public long stageId;
        public int clearRating;
        public int claimedRating;
    }

    [Serializable]
    public class ServerUpgrade
    {
        public string upgradeId;
        public int? level;
    }

    [Serializable]
    public class ServerItem
    {
        /// <summary>PR #58 이후 문자열 ID는 그대로 옮긴다. 이전 숫자 ID는 추측 변환하지 않도록 토큰 종류를 보존한다.</summary>
        public JToken itemId;
        public int quantity;
    }

    [Serializable]
    public class ServerCodex
    {
        public long monsterId;
        public bool claimed;
    }
}
