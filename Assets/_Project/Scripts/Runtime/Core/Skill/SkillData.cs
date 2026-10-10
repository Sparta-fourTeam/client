using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Aimed: 가장 가까운 적을 향해, RollingLane: 대상 줄을 따라 굴러감, Radial: 시전 위치에서 발 수만큼 사방으로 고르게 퍼짐(조준 없음)</summary>
    public enum ProjectilePath { Aimed, RollingLane, Radial }

    /// <summary>Projectile: 날아가 맞춤(투척), Hitscan: 대상 위치에 즉시 타격, Area: 위치에 머무는 범위, Beam: 지속하며 닿는 모든 적을 공격하는 광선, Chain: 여러 적을 연쇄적으로 튕기며 공격</summary>
    public enum CastType { Projectile, Hitscan, Area, Beam, Chain }
    public enum SkillForm { Default, Enbakutsu, JudgementThunder, TriangleIce, LargeLog, FireLog, FlameArrow, ThunderArrow }

    public class SkillData
    {
        public int id;
        public string name;

        /// <summary>이 플레이어 레벨부터 열린다. 랜덤 스킬 재료는 열린 스킬의 재료에서만 뽑는다. 값을 쓰지 않으면 처음부터 열려 있다</summary>
        public int unlockLevel = 1;
        public string desc;

        /// <summary>SkillAssetTable에서 이 스킬의 프리팹과 아이콘을 찾는 키. 스킬마다 유일해야 한다</summary>
        public string assetKey;
        public CastType castType;

        /// <summary>속성(무·화·빙·풍·뇌·토). 숫자: 0 Neutral, 1 Fire, 2 Ice, 3 Wind, 4 Lightning, 5 Earth. 적의 약점·저항이 이 값을 본다. 생략하면 무속성이다.
        /// 자식 스킬의 피해는 가장 바깥 스킬(부모)의 속성을 이어받는다</summary>
        public Element element;
        public ProjectilePath projectilePath;
        public SkillBaseStats baseStats;
        /// <summary>다른 스킬의 효과로만 시전되는 스킬. 새 스킬 카드로 제시하지 않는다</summary>
        public bool childOnly;
        /// <summary>이 스킬이 시전될 때 효과를 가져올 다른 스킬의 카드. 그 카드를 얻은 횟수만큼 적용한다 (집중 화살이 화살의 일제 사격을 쓰는 경우)</summary>
        public List<MirroredCard> mirrorCards;
        public List<SkillUpgradeOption> upgrades;
        // 최초 습득을 제외한 전투 중 성공한 강화 횟수 상한.
        public int maxLevel;
        // PlayerProfile의 영구 성장 ID. 미매핑 스킬은 null이며 영구 레벨 0으로 처리.
        public string progressionId;

        /// <summary>이 스킬이 낸 피해에 붙는 출처(ID, 속성, 시전 형태)</summary>
        public Game.Core.Combat.DamageSource ToDamageSource() => new Game.Core.Combat.DamageSource(id, element, castType);
    }
}
