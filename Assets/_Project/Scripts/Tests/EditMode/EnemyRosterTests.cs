using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    // 몬스터 등급·공격 방식 판정, 스테이지 로스터의 무작위 선택, 몬스터 프리팹 표
    public sealed class EnemyRosterTests
    {
        // 항상 지정한 값만 돌려주는 랜덤
        private sealed class FixedRandom : IRandomProvider
        {
            private readonly float _value;
            public FixedRandom(float value) => _value = value;
            public float Range(float min, float max) => _value;
        }

        private sealed class CountingRandom : IRandomProvider
        {
            public int Calls;

            public float Range(float min, float max)
            {
                Calls++;
                return 0f;
            }
        }

        private static MonsterDefinition Monster(int id, bool boss = false, bool elite = false, float projectileSpeed = 0f) =>
            new MonsterDefinition { Id = id, Hp = 10, AttackInterval = 1f, IsBoss = boss, IsElite = elite, ProjectileSpeed = projectileSpeed };

        // 일반 3개, 엘리트 1개, 보스 1개
        private static EnemyRoster CreateRoster() => new EnemyRoster(new[]
        {
            Monster(1), Monster(10, elite: true), Monster(2), Monster(100, boss: true), Monster(3),
        });

        // ───────── 등급과 공격 방식 ─────────

        [Test]
        public void GetEnemyType_FollowsBossAndEliteFlags()
        {
            Assert.AreEqual(EnemyType.Normal, Monster(1).GetEnemyType());
            Assert.AreEqual(EnemyType.Elite, Monster(1, elite: true).GetEnemyType());
            Assert.AreEqual(EnemyType.Boss, Monster(1, boss: true).GetEnemyType());
        }

        [Test]
        public void GetAttackType_RangedOnlyWithProjectileSpeed()
        {
            Assert.AreEqual(AttackType.Melee, Monster(1).GetAttackType());
            Assert.AreEqual(AttackType.Ranged, Monster(1, projectileSpeed: 8f).GetAttackType());
        }

        [Test]
        public void Validate_RejectsBossAndEliteTogether()
        {
            Assert.Throws<InvalidOperationException>(() => Monster(1, boss: true, elite: true).Validate());
            Assert.DoesNotThrow(() => Monster(1, elite: true).Validate());
        }

        // ───────── 로스터 ─────────

        // 일반 3개 중 랜덤 값의 정수 부분으로 고른다 (목록 순서 유지)
        [TestCase(0f, 1)]
        [TestCase(0.99f, 1)]
        [TestCase(1f, 2)]
        [TestCase(1.5f, 2)]
        [TestCase(2.99f, 3)]
        public void Pick_Normal_PicksByRandomIndex(float randomValue, int expectedMonsterId)
        {
            var monster = CreateRoster().Pick(EnemyType.Normal, new FixedRandom(randomValue));

            Assert.AreEqual(expectedMonsterId, monster.Id);
        }

        [Test]
        public void Pick_RandomOutOfRange_ClampsToEnds()
        {
            var roster = CreateRoster();

            Assert.AreEqual(3, roster.Pick(EnemyType.Normal, new FixedRandom(3f)).Id);
            Assert.AreEqual(1, roster.Pick(EnemyType.Normal, new FixedRandom(-1f)).Id);
        }

        [Test]
        public void Pick_NeverReturnsOtherType()
        {
            var roster = CreateRoster();

            for (float v = 0f; v <= 3f; v += 0.25f)
            {
                Assert.AreEqual(EnemyType.Normal, roster.Pick(EnemyType.Normal, new FixedRandom(v)).GetEnemyType(), $"random={v}");
            }
        }

        [TestCase(EnemyType.Elite, 10)]
        [TestCase(EnemyType.Boss, 100)]
        public void Pick_SingleMonsterType_AlwaysReturnsIt(EnemyType type, int expectedMonsterId)
        {
            Assert.AreEqual(expectedMonsterId, CreateRoster().Pick(type, new FixedRandom(0.7f)).Id);
        }

        [Test(Description = "등급이 하나뿐이면 랜덤을 부르지 않는다")]
        public void Pick_SingleMonster_DoesNotUseRandom()
        {
            var roster = new EnemyRoster(new[] { Monster(1) });
            var random = new CountingRandom();

            roster.Pick(EnemyType.Normal, random);

            Assert.AreEqual(0, random.Calls);
        }

        [Test(Description = "스테이지에 그 등급이 없으면 일반 등급에서 고른다")]
        public void Pick_MissingType_FallsBackToNormal()
        {
            var roster = new EnemyRoster(new[] { Monster(1), Monster(2) });

            Assert.AreEqual(EnemyType.Normal, roster.Pick(EnemyType.Elite, new FixedRandom(0f)).GetEnemyType());
            Assert.AreEqual(EnemyType.Normal, roster.Pick(EnemyType.Boss, new FixedRandom(0f)).GetEnemyType());
        }

        [Test]
        public void Pick_NoNormalAndMissingType_Throws()
        {
            var roster = new EnemyRoster(new[] { Monster(100, boss: true) });

            Assert.Throws<InvalidOperationException>(() => roster.Pick(EnemyType.Elite, new FixedRandom(0f)));
        }

        // ───────── 몬스터 프리팹 표 ─────────

        [Test]
        public void AssetTable_FindsEntriesById()
        {
            var table = MonsterAssetTable.Create(new Dictionary<int, MonsterAssetEntry>
            {
                { 1, new MonsterAssetEntry() },
                { 5, new MonsterAssetEntry() },
            });
            try
            {
                Assert.IsTrue(table.TryGet(5, out _));
                Assert.IsFalse(table.TryGet(2, out _));
                Assert.Throws<InvalidOperationException>(() => table.GetOrThrow(2));
                Assert.AreEqual(2, table.Entries.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(table); }
        }

        // ───────── 실제 데이터 ─────────

        [Test]
        public void RealData_GradesAndAttackTypesFollowMonstersRows()
        {
            var store = new GameDataStore();

            Assert.AreEqual(EnemyType.Elite, store.Monsters.GetOrThrow(11).GetEnemyType());
            Assert.AreEqual(EnemyType.Elite, store.Monsters.GetOrThrow(12).GetEnemyType());
            Assert.AreEqual(EnemyType.Boss, store.Monsters.GetOrThrow(100).GetEnemyType());
            Assert.AreEqual(EnemyType.Normal, store.Monsters.GetOrThrow(1).GetEnemyType());
            Assert.AreEqual(AttackType.Ranged, store.Monsters.GetOrThrow(1).GetAttackType());
            Assert.AreEqual(AttackType.Melee, store.Monsters.GetOrThrow(2).GetAttackType());
        }
    }
}
