using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    public class EnemyPrefabTableTests
    {
        // 항상 지정한 값만 돌려주는 랜덤
        private class FixedRandomProvider : IRandomProvider
        {
            private readonly float _value;
            public FixedRandomProvider(float value) => _value = value;
            public float Range(float min, float max) => _value;
        }

        // Prefab 없이 Speed로 엔트리를 구분 (테이블은 Prefab을 검사하지 않음)
        private static EnemyPrefabEntry Entry(EnemyType type, float speed)
        {
            return new EnemyPrefabEntry { Type = type, Speed = speed };
        }

        private static EnemyPrefabTable CreateTable()
        {
            return new EnemyPrefabTable(new List<EnemyPrefabEntry>
            {
                Entry(EnemyType.Normal, 1f),
                Entry(EnemyType.Elite, 10f),
                Entry(EnemyType.Normal, 2f),
                Entry(EnemyType.Boss, 100f),
                Entry(EnemyType.Normal, 3f),
            });
        }

        // Normal 3개 중 랜덤 값의 정수 부분으로 고른다 (리스트 순서 유지)
        [TestCase(0f, 1f)]
        [TestCase(0.99f, 1f)]
        [TestCase(1f, 2f)]
        [TestCase(1.5f, 2f)]
        [TestCase(2.99f, 3f)]
        public void GetRandomEntry_Normal_PicksByRandomIndex(float randomValue, float expectedSpeed)
        {
            var table = CreateTable();

            var entry = table.GetRandomEntry(EnemyType.Normal, new FixedRandomProvider(randomValue));

            Assert.AreEqual(EnemyType.Normal, entry.Type);
            Assert.AreEqual(expectedSpeed, entry.Speed);
        }

        [Test]
        public void GetRandomEntry_RandomReturnsMax_ClampsToLastEntry()
        {
            var table = CreateTable();

            // Range(0, 3)이 3을 돌려줘도(최대값 포함) 범위를 벗어나지 않음
            var entry = table.GetRandomEntry(EnemyType.Normal, new FixedRandomProvider(3f));

            Assert.AreEqual(3f, entry.Speed);
        }

        [Test]
        public void GetRandomEntry_RandomReturnsNegative_ClampsToFirstEntry()
        {
            var table = CreateTable();

            var entry = table.GetRandomEntry(EnemyType.Normal, new FixedRandomProvider(-1f));

            Assert.AreEqual(1f, entry.Speed);
        }

        [Test]
        public void GetRandomEntry_NeverReturnsOtherType()
        {
            var table = CreateTable();

            for (float v = 0f; v <= 3f; v += 0.25f)
            {
                var entry = table.GetRandomEntry(EnemyType.Normal, new FixedRandomProvider(v));
                Assert.AreEqual(EnemyType.Normal, entry.Type, $"random={v}");
            }
        }

        [TestCase(EnemyType.Elite, 10f)]
        [TestCase(EnemyType.Boss, 100f)]
        public void GetRandomEntry_SingleEntryType_AlwaysReturnsIt(EnemyType type, float expectedSpeed)
        {
            var table = CreateTable();

            var entry = table.GetRandomEntry(type, new FixedRandomProvider(0.7f));

            Assert.AreEqual(type, entry.Type);
            Assert.AreEqual(expectedSpeed, entry.Speed);
        }

        [Test]
        public void GetRandomEntry_MissingType_Throws()
        {
            var table = new EnemyPrefabTable(new List<EnemyPrefabEntry>
            {
                Entry(EnemyType.Normal, 1f),
            });

            Assert.Throws<InvalidOperationException>(
                () => table.GetRandomEntry(EnemyType.Boss, new FixedRandomProvider(0f)));
        }
    }
}
