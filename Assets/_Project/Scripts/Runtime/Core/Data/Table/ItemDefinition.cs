using System;

namespace Game.Core
{
    /// <summary>아이템 콘텐츠 계약. 소유량이나 드랍 확률은 정의에 넣지 않는다.</summary>
    [Serializable]
    public sealed class ItemDefinition
    {
        public string Id;
        public string Name;

        /// <summary>마법북 또는 인챈트 주문서로 강화할 스킬 또는 장비의 ID.</summary>
        public string TargetId;
        private bool _targetFromUpgrade;

        // 장비 대상은 Upgrades.MaterialItemId에서 찾는다. JSON에는 다시 중복해서 싣지 않는다.
        public bool ShouldSerializeTargetId() => !_targetFromUpgrade;

        internal void ResolveEquipmentTarget(string upgradeId)
        {
            TargetId = upgradeId;
            _targetFromUpgrade = true;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("Items: Id, Name이 필요합니다.");
            }

            if (Id != ItemIds.GemChest && string.IsNullOrWhiteSpace(TargetId))
            {
                throw new InvalidOperationException($"Items {Id}: 강화 재료는 고유 ID와 TargetId가 필요합니다.");
            }
        }
    }
}
