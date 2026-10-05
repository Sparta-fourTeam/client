using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>스폰 프리팹의 적 엔트리가 Monsters 테이블 행을 제대로 가리키는지 지킨다</summary>
    public sealed class EnemyPrefabMonsterLinkTests
    {
        private const string SpawnPrefab = "Assets/_Project/Prefabs/Stage/Spawn.prefab";

        [Test]
        public void EveryEntry_PointsToExistingMonster_WithRangedProjectileSpeed()
        {
            var store = new GameDataStore();
            var factory = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPrefab).GetComponentInChildren<EnemyFactory>(true);
            var entries = new SerializedObject(factory).FindProperty("_enemyPrefabEntries");

            Assert.Greater(entries.arraySize, 0);
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                int monsterId = entry.FindPropertyRelative("MonsterId").intValue;
                Assert.IsTrue(store.Monsters.Contains(monsterId), $"엔트리 {i}: Monsters에 MonsterId {monsterId}가 없습니다");

                bool ranged = entry.FindPropertyRelative("AttackType").enumValueIndex == (int)AttackType.Ranged;
                if (ranged)
                {
                    Assert.Greater(store.Monsters.GetOrThrow(monsterId).ProjectileSpeed, 0f, $"엔트리 {i}: 원거리인데 투사체 속도가 0입니다");
                }
            }
        }
    }
}
