using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Editor
{
    /// <summary>
    /// Wave 프리팹(Enemy 루트)에 애니메이션 몬스터 프리팹을 "Visual" 자식으로 붙인다.
    /// 기존 스프라이트 크기·중심에 맞춰 배치하고 루트 SpriteRenderer는 끈다.
    /// 일반 몬스터는 탈색 머티리얼, 엘리트는 원래 색 + 1.2배 크기.
    /// 여러 번 실행해도 기존 Visual을 지우고 다시 만든다.
    /// </summary>
    public static class WaveMonsterVisualLinker
    {
        private const string WaveFolder = "Assets/_Project/Prefabs/Stage";
        private const string MonsterFolder = "Assets/_Project/Prefabs/Monsters";
        private const string DesaturateShader = "Game/2D/Sprite-Lit-Desaturate";
        private const string DesaturateMaterialPath = "Assets/_Project/Materials/Monster_Desaturate.mat";
        private const string VisualName = "Visual";
        private const float EliteScale = 1.2f;

        private static readonly (string wave, string monster, bool elite)[] Links =
        {
            ("Slime", "Slime_Animated", false),
            ("Elite_Slime", "Slime_Animated", true),
            ("Bat", "Bat_Animated", false),
            ("Elite_Bat", "Bat_Animated", true),
            ("Skeleton", "Skeleton_Animated", false),
        };

        [MenuItem("Tools/Monster/Link Visuals To Wave Prefabs")]
        public static void LinkAll()
        {
            var desaturate = LoadOrCreateDesaturateMaterial();
            foreach (var (wave, monster, elite) in Links)
            {
                Link($"{WaveFolder}/{wave}.prefab", $"{MonsterFolder}/{monster}.prefab", elite, desaturate);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WaveMonsterVisualLinker] Wave 프리팹 연결 완료");
        }

        private static void Link(string wavePath, string monsterPath, bool elite, Material desaturate)
        {
            var monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(monsterPath);
            if (monsterPrefab == null)
            {
                Debug.LogWarning($"[WaveMonsterVisualLinker] 몬스터 프리팹 없음: {monsterPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(wavePath);
            try
            {
                var old = root.transform.Find(VisualName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var rootRenderer = root.GetComponent<SpriteRenderer>();
                if (rootRenderer == null || rootRenderer.sprite == null)
                {
                    Debug.LogWarning($"[WaveMonsterVisualLinker] {wavePath}: 기준이 될 SpriteRenderer가 없어 건너뜀");
                    return;
                }

                var target = rootRenderer.sprite.bounds; // 루트 로컬 공간
                rootRenderer.enabled = false;

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(monsterPrefab, root.transform);
                visual.name = VisualName;
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                SetLayerRecursively(visual, root.layer);

                // 비주얼의 크기를 기존 스프라이트 높이에 맞추고 중심을 맞춘다.
                var source = LocalBounds(visual.transform, root.transform);
                float scale = source.size.y > 0f ? target.size.y / source.size.y : 1f;
                if (elite)
                {
                    scale *= EliteScale;
                }

                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = target.center - source.center * scale;

                // 파츠 간 정렬 순서는 유지하고 바깥과는 기존 렌더러의 정렬로 묶는다.
                var group = visual.GetComponent<SortingGroup>();
                if (group == null)
                {
                    group = visual.AddComponent<SortingGroup>();
                }

                group.sortingLayerID = rootRenderer.sortingLayerID;
                group.sortingOrder = rootRenderer.sortingOrder;

                if (!elite)
                {
                    foreach (var r in visual.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        r.sharedMaterial = desaturate;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, wavePath);
                Debug.Log($"[WaveMonsterVisualLinker] {wavePath} ← {monsterPrefab.name} (scale {scale:0.###}{(elite ? ", elite" : "")})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // space 기준으로 본 visual 아래 모든 스프라이트의 경계 (visual 스케일 1 기준)
        private static Bounds LocalBounds(Transform visual, Transform space)
        {
            bool has = false;
            var bounds = new Bounds();
            foreach (var r in visual.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r.sprite == null)
                {
                    continue;
                }

                var b = r.sprite.bounds;
                var m = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }

        private static Material LoadOrCreateDesaturateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DesaturateMaterialPath);
            if (mat != null)
            {
                return mat;
            }

            var shader = Shader.Find(DesaturateShader);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"셰이더를 찾을 수 없음: {DesaturateShader}");
            }

            var folder = System.IO.Path.GetDirectoryName(DesaturateMaterialPath)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder)!.Replace('\\', '/'), System.IO.Path.GetFileName(folder));
            }

            mat = new Material(shader) { name = "Monster_Desaturate" };
            AssetDatabase.CreateAsset(mat, DesaturateMaterialPath);
            return mat;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
