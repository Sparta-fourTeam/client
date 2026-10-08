using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Core
{
    public class SkillUpgradeOption
    {
        public string id;
        public string name;
        public string desc;
        public List<EffectDef> effects;
        // 같은 카드 ID/횟수를 유지하고 영구 레벨에 따라 표시와 효과 전체를 교체한다.
        public SkillUpgradeVariant[] variants;
        // 양쪽 카탈로그에서 같은 선택 횟수를 참조하는 공유 강화 키. 같은 키를 가진 카드들이 한 강화로 묶인다.
        public string sharedId;
        /// <summary>함께 강화할 스킬 ID. 데이터에 적지 않고 카탈로그를 읽을 때 sharedId가 같은 카드의 스킬을 모아 채운다(SkillUpgradeTransaction.LinkSharedTargets)</summary>
        [JsonIgnore] public int[] affectedWeaponIds;
        /// <summary>최대 등장 횟수. 선택 성공 시에만 사용하며, 미선택 제시는 차감하지 않는다.</summary>
        public int maxPickCount = 1;
        public int[] requiredWeaponIds;

        // 전투 중 레벨만 의미한다. 로비의 영구 스킬 레벨과 구분한다.
        public int minBattleLevel = 1;
        public int minPermanentLevel;
        /// <summary>먼저 골라야 하는 카드와 횟수. 같은 스킬의 카드 한 번이면 {cardId, count:1}만 적는다</summary>
        public CardCountRequirement[] requiredCardCounts;
        public CardExclusion[] exclusions;
        /// <summary>없어진 필드. 조용히 무시하면 선행 조건이 사라지므로 값이 있는 데이터는 읽을 때 거절한다. requiredCardCounts로 옮긴다</summary>
        [JsonProperty("requiredCardIds")]
        private string[] RemovedRequiredCardIds
        {
            set
            {
                if (value != null && value.Length > 0)
                {
                    throw new JsonSerializationException($"카드 '{id}': requiredCardIds는 없어졌습니다. requiredCardCounts({{cardId, count, skillId}})로 옮기세요.");
                }
            }
        }

        public bool enabled = true;
        public string disabledReason;
    }
}
