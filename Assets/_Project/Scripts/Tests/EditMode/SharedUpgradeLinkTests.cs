using System.Collections.Generic;
using System.Linq;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>공유 강화의 대상 스킬 목록(affectedWeaponIds)은 데이터에 적지 않고 sharedId가 같은 카드에서 모은다</summary>
    public class SharedUpgradeLinkTests
    {
        private static SkillData Weapon(int id, params SkillUpgradeOption[] options) => new()
        {
            id = id,
            assetKey = "w" + id,
            maxLevel = 15,
            baseStats = new SkillBaseStats(),
            upgrades = options.ToList()
        };

        private static SkillUpgradeOption Shared(string id, string sharedId) => new()
        {
            id = id,
            sharedId = sharedId,
            maxPickCount = 1,
            effects = new List<EffectDef> { new() { kind = "damage", value = 50 } }
        };

        [Test(Description = "sharedId가 같은 카드를 가진 스킬이 서로의 대상이 된다")]
        public void Link_CollectsWeaponsBySharedId()
        {
            var a = Shared("a_dmg", "pair_dmg");
            var b = Shared("b_dmg", "pair_dmg");
            var other = Shared("a_speed", "pair_speed");
            var c = Shared("c_speed", "pair_speed");
            var weapons = new List<SkillData> { Weapon(1, a, other), Weapon(2, b), Weapon(3, c) };

            SkillUpgradeTransaction.LinkSharedTargets(weapons);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, a.affectedWeaponIds);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, b.affectedWeaponIds);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, other.affectedWeaponIds);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, c.affectedWeaponIds);
        }

        [Test(Description = "sharedId 없는 카드는 대상 목록이 없다")]
        public void Link_NoSharedId_LeavesTargetsNull()
        {
            var plain = new SkillUpgradeOption { id = "plain", maxPickCount = 1, effects = new List<EffectDef>() };

            SkillUpgradeTransaction.LinkSharedTargets(new List<SkillData> { Weapon(1, plain) });

            Assert.IsNull(plain.affectedWeaponIds);
        }

        [Test(Description = "짝이 없는 공유 강화는 카탈로그 검증이 거절한다")]
        public void LoneSharedUpgrade_IsRejectedByCatalog()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { Weapon(1, Shared("only", "alone")) });

            Assert.Throws<System.InvalidOperationException>(() => GameDataStore.ParseSkills(json));
        }

        [Test(Description = "없어진 requiredCardIds가 값과 함께 들어오면 조용히 무시하지 않고 거절한다")]
        public void RemovedRequiredCardIds_WithValues_IsRejected()
        {
            const string json = "[{\"id\":1,\"assetKey\":\"a\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[{\"id\":\"c\",\"requiredCardIds\":[\"x\"]}]}]";

            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => GameDataStore.ParseSkills(json));
        }

        [Test(Description = "빈 requiredCardIds는 그대로 읽힌다")]
        public void RemovedRequiredCardIds_Empty_IsAccepted()
        {
            const string json = "[{\"id\":1,\"assetKey\":\"a\",\"maxLevel\":1,\"baseStats\":{},\"upgrades\":[{\"id\":\"c\",\"maxPickCount\":1,\"effects\":[],\"requiredCardIds\":[]}]}]";

            Assert.DoesNotThrow(() => Newtonsoft.Json.JsonConvert.DeserializeObject<List<SkillData>>(json));
        }
    }
}
