using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    // 소환 패시브가 가리키는 몬스터 검증: 있어야 하고, 자기 자신이면 안 되고, 다시 소환하는 몬스터면 안 된다(연쇄 소환 금지)
    public sealed class SpawnReferenceTests
    {
        private static PassiveDefinition SpawnOf(int monsterId) =>
            new PassiveDefinition { Kind = PassiveKind.Spawn, Trigger = "OnDeath", MonsterId = monsterId, Count = 2 };

        private static MonsterDefinition Monster(int id, params PassiveDefinition[] passives) =>
            new MonsterDefinition { Id = id, Hp = 10, AttackInterval = 1f, Passives = new List<PassiveDefinition>(passives) };

        private static Dictionary<int, MonsterDefinition> Table(params MonsterDefinition[] monsters)
        {
            var table = new Dictionary<int, MonsterDefinition>();
            foreach (var monster in monsters) { table[monster.Id] = monster; }
            return table;
        }

        [Test]
        public void SpawningADifferentPlainMonster_IsAccepted()
        {
            var table = Table(Monster(1, SpawnOf(3)), Monster(3));

            Assert.DoesNotThrow(() => GameDataStore.ValidatePassiveReferences(table));
        }

        [Test(Description = "자기 자신을 소환하면 거부한다")]
        public void SpawningItself_IsRejected()
        {
            var table = Table(Monster(1, SpawnOf(1)));

            var error = Assert.Throws<InvalidOperationException>(() => GameDataStore.ValidatePassiveReferences(table));

            StringAssert.Contains("자기 자신", error.Message);
        }

        [Test(Description = "소환 대상이 다시 소환하는 몬스터면 거부한다 (연쇄 소환 금지)")]
        public void SpawningASpawner_IsRejected()
        {
            var table = Table(Monster(1, SpawnOf(2)), Monster(2, SpawnOf(3)), Monster(3));

            var error = Assert.Throws<InvalidOperationException>(() => GameDataStore.ValidatePassiveReferences(table));

            StringAssert.Contains("연쇄 소환", error.Message);
        }

        [Test(Description = "서로 소환하는 순환도 거부한다")]
        public void SpawnCycle_IsRejected()
        {
            var table = Table(Monster(1, SpawnOf(2)), Monster(2, SpawnOf(1)));

            Assert.Throws<InvalidOperationException>(() => GameDataStore.ValidatePassiveReferences(table));
        }

        [Test(Description = "소환 대상이 소환 외의 패시브만 가지면 허용한다")]
        public void SpawningAMonsterWithOtherPassives_IsAccepted()
        {
            var immune = new PassiveDefinition { Kind = PassiveKind.Immunity, Statuses = new[] { "Stun" } };
            var table = Table(Monster(1, SpawnOf(2)), Monster(2, immune));

            Assert.DoesNotThrow(() => GameDataStore.ValidatePassiveReferences(table));
        }
    }
}
