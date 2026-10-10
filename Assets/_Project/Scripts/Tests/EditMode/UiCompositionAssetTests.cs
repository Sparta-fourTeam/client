using System.Linq;
using System.Threading;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class UiCompositionAssetTests
    {
        [Test]
        public void UiPrefabsAreFullyActiveForAuthoring()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(node.gameObject.activeSelf, Is.True, path + "/" + node.name);
                }
            }
        }

        [Test]
        public void InitialVisibilityAppliesInheritedOverridesOnceWithoutResettingLiveState()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/Lobby/SkillScreen.prefab"));
            try
            {
                var so = new SerializedObject(root.GetComponent<UiInitialVisibility>());
                GameObject[] Read(string name)
                {
                    var prop = so.FindProperty(name);
                    return Enumerable.Range(0, prop.arraySize)
                        .Select(i => (GameObject)prop.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                }
                var hidden = Read("_hiddenOnStart");
                var shown = Read("_shownOnStart");
                Assert.That(hidden, Is.Not.Empty);
                Assert.That(shown, Is.Not.Empty);
                UiInitialVisibility.InitializeTree(root);
                Assert.That(hidden.All(o => !o.activeSelf), Is.True);
                Assert.That(shown.All(o => o.activeSelf), Is.True);
                hidden[0].SetActive(true);
                shown[0].SetActive(false);
                UiInitialVisibility.InitializeTree(root);
                Assert.That(hidden[0].activeSelf, Is.True, "Live UI must not be hidden again.");
                Assert.That(shown[0].activeSelf, Is.False, "Live selection must not be reset.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Lobby")]
        [TestCase("Stage")]
        public void IndependentUiIsComposedAsPeersRatherThanEmbeddedScreens(string sceneName)
        {
            var composition = AssetDatabase.LoadAssetAtPath<UiComposition>($"Assets/_Project/Settings/UI/{sceneName}Ui.asset");
            var paths = composition.Entries.Select(e => AssetDatabase.GetAssetPath(e.Prefab)).ToHashSet();
            foreach (var entry in composition.Entries)
            {
                var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(entry.Prefab));
                try
                {
                    foreach (var node in root.GetComponentsInChildren<Transform>(true).Skip(1))
                    {
                        string nested = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject);
                        Assert.That(paths.Contains(nested), Is.False, entry.Id + " embeds independent UI: " + nested);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        [TestCase("Lobby")]
        [TestCase("Stage")]
        public void AuthoredCompositionBindsAndInitializesEveryScreen(string sceneName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/Hosts/{sceneName}UiHost.prefab");
            Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab);
            try
            {
                var host = root.GetComponent<UiHost>();
                var registry = new UiRegistry();
                host.BuildAsync(new DirectUiPrefabProvider(), registry, _ => { }, CancellationToken.None).GetAwaiter().GetResult();
                Assert.That(root.GetComponentsInChildren<HudView>(true), Is.Not.Empty);
                if (sceneName == "Lobby")
                {
                    Assert.That(registry.Get<LobbyNavView>(), Is.Not.Null);
                    Assert.That(registry.Get<MissionScreenView>(), Is.Not.Null);
                    Assert.That(registry.Get<ProfilePopupView>(), Is.Not.Null);
                    Assert.That(registry.Get<ProfileOpenButton>(), Is.Not.Null);
                }
                else
                {
                    Assert.That(registry.Get<CardSelectView>(), Is.Not.Null);
                    Assert.That(registry.Get<PauseMenuView>(), Is.Not.Null);
                    Assert.That(registry.Get<ResultPopupView>(), Is.Not.Null);
                    Assert.That(registry.Get<SubmittingView>(), Is.Not.Null);
                }
                Assert.That(host.transform.Find(sceneName == "Lobby" ? "NavUI/Popups" : "PopupCanvas/Popups"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Lobby")]
        [TestCase("Stage")]
        public void SceneContainsOneHostAndNoSerializedScreenInstances(string sceneName)
        {
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/_Project/Scenes/{sceneName}.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<UiHost>(true)).Count(), Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<HudView>(true)), Is.Empty);
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<LobbyNavView>(true)), Is.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
