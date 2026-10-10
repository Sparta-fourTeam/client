using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Editor;
using Game.View;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class EditorToolsTests
    {
        [Test]
        public void AuthoredUiHasNoBrokenReferencesOrUnreviewedReplacements()
        {
            var issues = UiAssetAudit.InspectAll();
            Assert.That(issues, Is.Empty, string.Join("\n", issues.Select(i => i.AssetPath + "/" + i.ObjectPath + ": " + i.Message)));
        }

        [Test]
        public void AuditDistinguishesBoundStateTextFromReplacedText()
        {
            var root = new GameObject("Root", typeof(RectTransform), typeof(FormHintIconView));
            try
            {
                var bound = new GameObject("Blocked", typeof(RectTransform), typeof(TextMeshProUGUI));
                bound.transform.SetParent(root.transform);
                bound.SetActive(false);
                var obsolete = new GameObject("OldLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                obsolete.transform.SetParent(root.transform);
                obsolete.SetActive(false);
                var art = new GameObject("Art_Glyph", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                art.transform.SetParent(root.transform);
                art.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
                var so = new SerializedObject(root.GetComponent<FormHintIconView>());
                so.FindProperty("_blockedMark").objectReferenceValue = bound;
                so.ApplyModifiedPropertiesWithoutUndo();
                var issues = new List<UiAssetAudit.Issue>();
                UiAssetAudit.InspectPrefab(root, "Example.prefab", issues);
                Assert.That(issues.Count, Is.EqualTo(1));
                Assert.That(issues[0].ObjectPath, Is.EqualTo("OldLabel"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SaveResetKeepsAnExactRecoverableBackup()
        {
            string folder = Path.Combine(Path.GetTempPath(), "nova-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string save = Path.Combine(folder, "save.json");
                byte[] contents = System.Text.Encoding.UTF8.GetBytes("{\"gold\":123,\"name\":\"검증\"}");
                File.WriteAllBytes(save, contents);
                string backup = PlaytestWindow.BackupAndReset(save);
                Assert.That(File.Exists(save), Is.False);
                Assert.That(File.ReadAllBytes(backup), Is.EqualTo(contents));
                File.Copy(backup, save);
                string nextBackup = PlaytestWindow.BackupAndReset(save);
                Assert.That(nextBackup, Is.Not.EqualTo(backup));
                Assert.That(File.Exists(backup), Is.True);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void PlaytestDisablesGameActionsOutsidePlayMode()
        {
            var window = ScriptableObject.CreateInstance<PlaytestWindow>();
            try
            {
                window.CreateGUI();
                foreach (string name in new[] { "launchButton", "pauseButton", "resumeButton", "forfeitButton", "breakWallButton", "failButton", "retryButton" })
                {
                    Assert.That(window.rootVisualElement.Q<UnityEngine.UIElements.Button>(name).enabledSelf, Is.False, name);
                }
            }
            finally { Object.DestroyImmediate(window); }
        }

        [UnityTest]
        public IEnumerator WorkspaceBrowsesCompositionsAndCommonAssets()
        {
            RequireGraphics();
            var window = ScriptableObject.CreateInstance<UiWorkspaceWindow>();
            try
            {
                window.Show();
                yield return null;
                Assert.That(window.rootVisualElement.Q<ListView>("assetList").itemsSource.Count, Is.EqualTo(18));
                var group = window.rootVisualElement.Q<DropdownField>("groupField");
                group.value = "전투 화면";
                Assert.That(window.rootVisualElement.Q<ListView>("assetList").itemsSource.Count, Is.EqualTo(8));
                Assert.That(window.rootVisualElement.Q<UnityEngine.UIElements.Image>("previewImage").image, Is.Not.Null);
                group.value = "공통 요소";
                Assert.That(window.rootVisualElement.Q<ListView>("assetList").itemsSource.Count, Is.GreaterThan(10));
                Assert.That(window.rootVisualElement.Q<UnityEngine.UIElements.Button>("compositionButton").enabledSelf, Is.False);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [TestCase("Lobby", "CharacterScreen", 0)]
        [TestCase("Lobby", "MissionScreen", 1)]
        [TestCase("Stage", "HudCanvas", 5)]
        [TestCase("Lobby", "UpgradeConfirmPopup", 0)]
        public void ComposedPreviewRendersAndLeavesAssetsUntouched(string sceneName, string id, int device)
        {
            RequireGraphics();
            var composition = AssetDatabase.LoadAssetAtPath<UiComposition>($"Assets/_Project/Settings/UI/{sceneName}Ui.asset");
            var prefab = composition.Entries.Single(e => e.Id == id).Prefab;
            string path = AssetDatabase.GetAssetPath(prefab);
            byte[] original = File.ReadAllBytes(path);
            var shot = UiPreviewRenderer.Capture(prefab, composition, UiPreviewRenderer.Devices[device], true, "기본");
            try
            {
                Assert.That(shot.width, Is.EqualTo(UiPreviewRenderer.Devices[device].Width));
                var colors = shot.GetPixels32();
                Assert.That(colors.Where((_, i) => i % 100 == 0).Distinct().Count(), Is.GreaterThan(100), "Preview is empty or collapsed.");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
                Directory.CreateDirectory("Logs/ToolChecks");
                File.WriteAllBytes($"Logs/ToolChecks/{id}-{device}.png", shot.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(shot); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void PreviewCanvasSizeMatchesUnityScreenSpaceCamera(int deviceIndex)
        {
            RequireGraphics();
            var device = UiPreviewRenderer.Devices[deviceIndex];
            var viewport = AspectRatioCamera.CalculateViewport(device.Width, device.Height, 9f / 16f);
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            try
            {
                var cameraRoot = new GameObject("Native camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraRoot, scene);
                var camera = cameraRoot.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                target = new RenderTexture(device.Width, device.Height, 24);
                target.Create();
                camera.targetTexture = target;
                camera.rect = viewport;
                var root = new GameObject("Native canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                Canvas.ForceUpdateCanvases();
                var expected = ((RectTransform)root.transform).rect.size;
                var actual = UiPreviewRenderer.CalculateCanvasSize(device, viewport);
                Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
                Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }
        }

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("Preview requires a graphics device.");
            }
        }
    }
}
