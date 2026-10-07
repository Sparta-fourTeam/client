using System.Collections.Generic;
using Game.Core;
using Game.Core.Messages;
using Game.View;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class SkillDamageRowsTests
    {
        private sealed class Status : ISkillStatus
        {
            public int Id { get; set; }
            public int Level { get; set; }
            public string AssetKey => "k";
            public float CooldownRatio => 0;
        }

        private static List<ISkillStatus> Owned(params (int id, int level)[] skills)
        {
            var list = new List<ISkillStatus>();
            foreach (var (id, level) in skills) { list.Add(new Status { Id = id, Level = level }); }
            return list;
        }

        [Test(Description = "피해량 순서를 그대로 지키고 보유 스킬의 레벨을 붙인다")]
        public void Build_KeepsOrderAndAddsLevel()
        {
            var rows = SkillDamageRows.Build(
                new[] { new SkillDamage(2, 500), new SkillDamage(1, 120) }, Owned((1, 3), (2, 7)), null);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual((7, 500), (rows[0].Level, rows[0].Damage));
            Assert.AreEqual((3, 120), (rows[1].Level, rows[1].Damage));
        }

        [Test(Description = "최대 5줄까지만 만든다")]
        public void Build_CapsAtFiveRows()
        {
            var damages = new List<SkillDamage>();
            var owned = new List<(int, int)>();
            for (int i = 1; i <= 8; i++) { damages.Add(new SkillDamage(i, 100 - i)); owned.Add((i, 1)); }

            var rows = SkillDamageRows.Build(damages, Owned(owned.ToArray()), null);

            Assert.AreEqual(SkillDamageRows.MaxRows, rows.Count);
            Assert.AreEqual(99, rows[0].Damage);
        }

        [Test(Description = "보유 목록에 없는 스킬은 레벨을 알 수 없어 뺀다")]
        public void Build_SkipsSkillsNotOwned()
        {
            var rows = SkillDamageRows.Build(new[] { new SkillDamage(9, 999), new SkillDamage(1, 10) }, Owned((1, 2)), null);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(10, rows[0].Damage);
        }

        [Test(Description = "피해 기록이나 보유 목록이 없으면 빈 목록이다 (영역이 꺼진다)")]
        public void Build_EmptyInputs_ReturnEmpty()
        {
            Assert.IsEmpty(SkillDamageRows.Build(null, Owned((1, 1)), null));
            Assert.IsEmpty(SkillDamageRows.Build(new[] { new SkillDamage(1, 5) }, null, null));
            Assert.IsEmpty(SkillDamageRows.Build(new SkillDamage[0], Owned((1, 1)), null));
        }
    }
}
