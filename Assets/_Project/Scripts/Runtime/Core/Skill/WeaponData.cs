using System.Collections.Generic;

namespace Game.Core
{
    public enum ProjectilePath { Aimed, RollingLane }

    public enum CastType { Projectile, Hitscan }
    public enum WeaponForm { Default, Enbakutsu, JudgementThunder, TriangleIce, LargeLog, FireLog }

    public class WeaponData
    {
        public int id;
        public string name;
        public string desc;

        /// <summary>HUD가 아이콘을 찾는 키 (SkillIconTable). 컨벤션상 아이콘은 테이블의 IconKey로 찾는다</summary>
        public string iconKey;
        public CastType castType;
        public ProjectilePath projectilePath;
        public WeaponBaseStats baseStats;
        public List<WeaponUpgradeOption> upgrades;
        // 최초 습득을 제외한 전투 중 성공한 강화 횟수 상한.
        public int maxLevel;
        // PlayerProfile의 영구 성장 ID. 미매핑 인술은 null이며 영구 레벨 0으로 처리.
        public string progressionId;
        // 새 인술의 기본 수치 자료가 없을 때 사용한 기존 프로토타입 ID.
        public int prototypeBalanceSourceId;

    }
}
