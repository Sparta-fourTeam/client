using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>스킬 카드 후보를 내고 적용하는 쪽. 카드를 뽑는 CardDeck이 WeaponController 없이도 테스트되도록 나눈 경계다</summary>
    public interface IUpgradeChoiceSource
    {
        /// <summary>지금 고를 수 있는 스킬 카드 전부 (섞지 않은 채)</summary>
        List<UpgradeChoice> GetUpgradeCandidates();

        bool ApplyUpgradeChoice(UpgradeChoice choice);
    }
}
