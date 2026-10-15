using System;
using System.Collections.Generic;

namespace Game.Network
{
    /// <summary>GET /api/user/me 응답. 서버 UserDataResponse와 필드 이름이 같다 (서버 PR #47).
    /// 서버는 ID를 숫자(Long)로 주므로 클라이언트 DTO(PlayerSnapshot)로 바꾸는 규칙은 ServerUserMapper에 있다</summary>
    [Serializable]
    public class ServerUserData
    {
        public int gold;

        /// <summary>서버 필드 이름은 jem이다</summary>
        public int jem;

        /// <summary>서버가 null을 줄 수 있다</summary>
        public int? exp;

        public int energyStored;

        /// <summary>서버 LocalDateTime(시간대 없는 ISO 문자열). 클라이언트는 UTC로 해석한다 (EnergyRule.ParseUpdatedAt)</summary>
        public string energyUpdatedAt;

        public List<ServerStageProgress> stageProgress;
        public List<ServerUpgrade> upgrades;
        public List<ServerItem> items;
        public List<ServerCodex> codex;
        public List<long> tutorials;
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
        public long itemId;
        public int quantity;
    }

    [Serializable]
    public class ServerCodex
    {
        public long monsterId;
        public bool claimed;
    }
}
