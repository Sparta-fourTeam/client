using Game.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>스폰 프리팹의 팩토리가 연결한 몬스터 프리팹 표가 Monsters 테이블과 어긋나지 않는지 지킨다</summary>
    public sealed class EnemyPrefabMonsterLinkTests
    {
        private const string SpawnPrefab = "Assets/_Project/Prefabs/Stage/Spawn.prefab";

        private static MonsterAssetTable LoadAssets()
        {
            var factory = AssetDatabase.LoadAssetAtPath<GameObject>(SpawnPrefab).GetComponentInChildren<EnemyFactory>(true);
            return new SerializedObject(factory).FindProperty("_assets").objectReferenceValue as MonsterAssetTable;
        }

        [Test]
        public void EveryMonster_HasPrefab_AndRangedHasProjectile()
        {
            var store = new GameDataStore();
            var assets = LoadAssets();
            Assert.IsNotNull(assets, "Spawn.prefab의 EnemyFactory에 MonsterAssetTable이 연결돼 있어야 합니다");

            foreach (var monster in store.Monsters.Values)
            {
                Assert.IsTrue(assets.TryGet(monster.Id, out var entry), $"MonsterId {monster.Id}에 해당하는 표 항목이 없습니다");
                Assert.IsNotNull(entry.prefab, $"MonsterId {monster.Id}: prefab이 없습니다");
                Assert.IsNotNull(entry.prefab.GetComponent<Enemy>(), $"MonsterId {monster.Id}: Enemy가 없습니다");
                Assert.IsNotNull(entry.prefab.GetComponentInChildren<Animator>(), $"MonsterId {monster.Id}: Animator가 없습니다");

                if (monster.GetAttackType() == AttackType.Ranged)
                {
                    Assert.IsNotNull(entry.projectilePrefab, $"MonsterId {monster.Id}: 원거리인데 projectilePrefab이 없습니다");
                }
            }
        }

        [Test]
        public void EveryTableEntry_PointsToExistingMonster()
        {
            var store = new GameDataStore();

            foreach (var pair in LoadAssets().Entries)
            {
                Assert.IsTrue(store.Monsters.Contains(pair.Key), $"표 항목의 MonsterId {pair.Key}가 Monsters에 없습니다");
            }
        }
    }
}
