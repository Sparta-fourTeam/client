using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests
{
    /// <summary>HUD와 팝업 캔버스(Screen Space - Camera)는 스프라이트와 같은 정렬 큐에서 그려진다.
    /// 이펙트·플레이어·몬스터 같은 월드 프리팹이 UI보다 큰 정렬 값을 쓰면 UI를 가리므로, 새 프리팹이 늘어도 그렇게 되지 않게 지킨다</summary>
    public class UiSortingOrderTests
    {
        private static readonly string[] WorldFolders =
        {
            "Assets/_Project/Prefabs/Stage",
            "Assets/_Project/Prefabs/Skills",
            "Assets/_Project/Prefabs/Monsters",
            "Assets/_Project/Resources/VFX",
            "Assets/_Project/Resources/UI"
        };

        private const string HudPrefab = "Assets/_Project/Prefabs/UI/HudCanvas.prefab";
        private const string StageScene = "Assets/_Project/Scenes/Stage.unity";

        /// <summary>월드 프리팹이 실제로 그려지는 정렬 값 중 가장 큰 값. SortingGroup 안의 렌더러는 그룹의 순서를 따른다</summary>
        private static int MaxWorldOrder(out string where)
        {
            var max = int.MinValue;
            where = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", WorldFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var group = renderer.GetComponentInParent<SortingGroup>(true);
                    var order = group != null ? group.sortingOrder : renderer.sortingOrder;
                    if (order > max)
                    {
                        max = order;
                        where = $"{path} ({renderer.name})";
                    }
                }
            }

            return max;
        }

        [Test(Description = "HUD 캔버스는 어떤 월드 프리팹(이펙트·플레이어·몬스터·데미지 숫자)보다도 위에 그려진다")]
        public void HudCanvas_IsAboveEveryWorldRenderer()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab).GetComponent<Canvas>();
            var world = MaxWorldOrder(out var where);

            Assert.Greater(hud.sortingOrder, world, $"가장 큰 월드 정렬 값: {world} — {where}");
        }

        [Test(Description = "팝업 캔버스는 HUD 위, 모든 월드 프리팹 위에 그려진다")]
        public void PopupCanvas_IsAboveHudAndWorld()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab).GetComponent<Canvas>();
            var scene = EditorSceneManager.OpenScene(StageScene, OpenSceneMode.Additive);
            try
            {
                var popup = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .Single(canvas => canvas.name == "PopupCanvas");
                var world = MaxWorldOrder(out var where);

                Assert.Greater(popup.sortingOrder, hud.sortingOrder, "팝업은 HUD 위에 있어야 한다");
                Assert.Greater(popup.sortingOrder, world, $"가장 큰 월드 정렬 값: {world} — {where}");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
