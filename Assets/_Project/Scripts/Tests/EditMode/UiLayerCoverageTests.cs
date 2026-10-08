using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests
{
    /// <summary>전투 UI는 월드보다 위의 "UI" 정렬 레이어에 둔다 (#249). 정렬 순서(order)는 같은 레이어 안에서만 의미가 있으므로
    /// UiSortingLayerTests가 다루지 않는 두 가지를 지킨다: 팝업 캔버스도 UI 레이어에서 HUD 위에 있는지,
    /// 월드 프리팹이 UI 레이어 이상을 쓰지 않는지</summary>
    public class UiLayerCoverageTests
    {
        private const string UiLayer = "UI";
        private const string HudPrefab = "Assets/_Project/Prefabs/UI/HudCanvas.prefab";
        private const string StageScene = "Assets/_Project/Scenes/Stage.unity";

        private static readonly string[] WorldFolders =
        {
            "Assets/_Project/Prefabs/Stage",
            "Assets/_Project/Prefabs/Skills",
            "Assets/_Project/Prefabs/Monsters",
            "Assets/_Project/Resources/VFX",
            "Assets/_Project/Resources/UI"
        };

        [Test(Description = "팝업 캔버스는 UI 정렬 레이어에서 HUD 캔버스보다 위에 그려진다")]
        public void PopupCanvas_UsesUiLayer_AboveHud()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab).GetComponent<Canvas>();
            var scene = EditorSceneManager.OpenScene(StageScene, OpenSceneMode.Additive);
            try
            {
                var popup = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                    .Single(canvas => canvas.name == "PopupCanvas");

                Assert.AreEqual(UiLayer, popup.sortingLayerName, "팝업 캔버스가 UI 정렬 레이어에 있어야 한다");
                Assert.AreEqual(hud.sortingLayerID, popup.sortingLayerID, "HUD와 팝업은 같은 레이어에서 순서로 비교된다");
                Assert.Greater(popup.sortingOrder, hud.sortingOrder, "팝업은 HUD 위에 있어야 한다");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test(Description = "월드 프리팹(몬스터·이펙트·플레이어·데미지 숫자)은 UI 정렬 레이어 이상을 쓰지 않는다")]
        public void WorldPrefabs_AreBelowUiLayer()
        {
            var uiValue = SortingLayer.GetLayerValueFromName(UiLayer);
            var offenders = new System.Collections.Generic.List<string>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", WorldFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    // SortingGroup 안의 렌더러는 그룹의 레이어를 따른다
                    var group = renderer.GetComponentInParent<SortingGroup>(true);
                    var layerId = group != null ? group.sortingLayerID : renderer.sortingLayerID;
                    if (SortingLayer.GetLayerValueFromID(layerId) >= uiValue)
                    {
                        offenders.Add($"{path} ({renderer.name})");
                    }
                }
            }

            Assert.IsEmpty(offenders, "UI 정렬 레이어 이상을 쓰는 월드 렌더러:\n" + string.Join("\n", offenders));
        }
    }
}
