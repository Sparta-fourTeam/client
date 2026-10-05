using System.Collections.Generic;

namespace Game.Core
{
    public class WeaponUpgradeOption
    {
        public string id;
        public string name;
        public string desc;
        public List<EffectDef> effects;
        // 같은 카드 ID/횟수를 유지하고 영구 레벨에 따라 표시와 효과 전체를 교체한다.
        public WeaponUpgradeVariant[] variants;
        // 양쪽 카탈로그에서 같은 선택 횟수를 참조하는 공유 강화 키.
        public string sharedId;
        public int[] affectedWeaponIds;
        /// <summary>최대 등장 횟수. 선택 성공 시에만 사용하며, 미선택 제시는 차감하지 않는다.</summary>
        public int maxPickCount = 1;
        public string[] requiredCardIds;
        public int[] requiredWeaponIds;

        // 전투 중 레벨만 의미한다. 로비의 영구 스킬 레벨과 구분한다.
        public int minBattleLevel = 1;
        public int minPermanentLevel;
        public CardCountRequirement[] requiredCardCounts;
        public CardExclusion[] exclusions;
        public bool enabled = true;
        public string disabledReason;
    }
}
