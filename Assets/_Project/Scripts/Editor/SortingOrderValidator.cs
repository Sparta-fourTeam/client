using System.Collections.Generic;
using System.Text;
using Game.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>프리팹의 sortingOrder가 SortingBands 구간을 지키는지 검사한다.
    /// 몬스터 구간(-402 ~ -10)은 EnemyDepthSorter가 쓰는 전용 구간이고, 바닥 연출은 바닥 구간(-450 ~ -410)에 있어야 몬스터 뒤에 그려진다</summary>
    public static class SortingOrderValidator
    {
        // 몬스터 뒤, 배경 앞에 그려져야 하는 바닥 연출. 새 바닥 연출은 여기에 추가한다
        private static readonly string[] GroundPrefabs =
        {
            "Assets/_Project/Prefabs/Skills/FrostPrison.prefab",
            "Assets/_Project/Resources/VFX/ElectricField.prefab",
            "Assets/_Project/Prefabs/Skills/Log.prefab",
            "Assets/_Project/Prefabs/Skills/BurningRootWave.prefab",
            "Assets/_Project/Prefabs/Skills/LargeRootWave.prefab",
            "Assets/_Project/Prefabs/Stage/Fx_GroundSlam.prefab",
            "Assets/_Project/Prefabs/Stage/Fx_SonicWave.prefab",
        };

        [MenuItem("Tools/Project Nova/Validate Sorting Orders")]
        public static void Validate()
        {
            var report = new StringBuilder();
            int problems = 0;

            foreach (string path in GroundPrefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    report.AppendLine($"바닥 연출 프리팹을 찾을 수 없다: {path}");
                    problems++;
                    continue;
                }

                foreach (int order in Orders(prefab))
                {
                    if (!SortingBands.IsGround(order))
                    {
                        report.AppendLine($"{path}: 바닥 구간({SortingBands.GroundMin} ~ {SortingBands.GroundMax}) 밖의 순서 {order}");
                        problems++;
                    }
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs", "Assets/_Project/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                foreach (int order in Orders(prefab))
                {
                    if (SortingBands.IsEnemyReserved(order))
                    {
                        report.AppendLine($"{path}: 몬스터 전용 구간({SortingBands.EnemyBack} ~ {SortingBands.EnemyFront})의 순서 {order}");
                        problems++;
                    }
                }
            }

            // 스킬·이펙트는 SortingGroup 하나로 묶어야 다른 이펙트와 파츠가 섞이지 않는다. 바닥 연출과 몬스터 이펙트는 구간 값으로 따로 관리한다
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Skills", "Assets/_Project/Resources/VFX" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || System.Array.IndexOf(GroundPrefabs, path) >= 0 || path.Contains("/Enemy"))
                {
                    continue;
                }

                if (prefab.GetComponent<SortingGroup>() == null)
                {
                    report.AppendLine($"{path}: 루트에 SortingGroup이 없다");
                    problems++;
                }
            }

            if (problems == 0)
            {
                Debug.Log("[SortingOrder] 구간 위반 없음");
            }
            else
            {
                Debug.LogWarning($"[SortingOrder] 위반 {problems}건\n{report}");
            }
        }

        // 루트에 SortingGroup이 있으면 바깥과는 그 순서로만 비교되므로 안쪽 파츠 순서는 보지 않는다
        private static IEnumerable<int> Orders(GameObject prefab)
        {
            if (prefab.TryGetComponent<SortingGroup>(out var rootGroup))
            {
                yield return rootGroup.sortingOrder;
                yield break;
            }

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                yield return r.sortingOrder;
            }

            foreach (var g in prefab.GetComponentsInChildren<SortingGroup>(true))
            {
                yield return g.sortingOrder;
            }
        }
    }
}
