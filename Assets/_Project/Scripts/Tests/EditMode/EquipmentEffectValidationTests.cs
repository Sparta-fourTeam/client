using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>전투에 적용할 방법이 없는 장비 효과는 부팅 때 거부해, 로비에만 보이고 전투에 안 들어가는 장비가 생기지 않게 한다</summary>
    public sealed class EquipmentEffectValidationTests
    {
        private static UpgradeDefinition Row(string upgradeId, string kind) => new UpgradeDefinition
        {
            UpgradeId = upgradeId,
            MaxLevel = 10,
            Effects = new List<UpgradeStatEffect> { new UpgradeStatEffect { Kind = kind, ValuePerLevel = 5 } },
        };

        [Test(Description = "전투 효과로 바꿀 수 없는 장비 효과 종류는 거부한다")]
        public void UnknownEquipmentKind_IsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => PermanentSkillEffect.ValidateEquipment(Row("equipment.hat", "unknownKind")));
        }

        [Test(Description = "장비가 아닌 행(스킬 영구 강화 등)은 장비 효과 규칙으로 검사하지 않는다")]
        public void NonEquipmentRow_IsNotChecked()
        {
            Assert.DoesNotThrow(() => PermanentSkillEffect.ValidateEquipment(Row("shuriken", "unknownKind")));
        }
    }
}
