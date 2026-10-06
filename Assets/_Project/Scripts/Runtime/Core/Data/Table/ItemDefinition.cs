using System;

namespace Game.Core
{
    /// <summary>아이템 콘텐츠 계약. 소유량이나 드랍 확률은 정의에 넣지 않는다.</summary>
    [Serializable]
    public sealed class ItemDefinition
    {
        public string Id;
        public string Name;
        public string IconKey;

        /// <summary>마법북으로 강화할 스킬 또는 장비의 ID.</summary>
        public string TargetId;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name)
                || string.IsNullOrWhiteSpace(IconKey))
            {
                throw new InvalidOperationException("Items: Id, Name, IconKey가 필요합니다.");
            }

            if (string.IsNullOrWhiteSpace(TargetId))
            {
                throw new InvalidOperationException($"Items {Id}: 마법북은 고유 ID와 TargetId가 필요합니다.");
            }
        }
    }
}
