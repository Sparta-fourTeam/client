using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Game.Core;
using Game.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    internal static class UiPreviewRenderer
    {
        internal readonly struct Device
        {
            public readonly string Name;
            public readonly int Width, Height, Top, Bottom;
            public Device(string name, int width, int height, int top = 0, int bottom = 0)
            { Name = name; Width = width; Height = height; Top = top; Bottom = bottom; }
        }

        internal static readonly Device[] Devices =
        {
            new("16:9 · 1080×1920", 1080, 1920), new("iPhone · 1170×2532", 1170, 2532, 141, 102),
            new("21:9 · 1080×2520", 1080, 2520, 100, 60), new("Android · 720×1600", 720, 1600, 60),
            new("소형 · 720×1280", 720, 1280), new("태블릿 · 1536×2048", 1536, 2048)
        };

        internal static List<string> Samples(string id)
        {
            if (id == "UpgradeConfirmPopup")
            {
                return new() { "기본" };
            }

            if (id == "ResultPopup")
            {
                return new() { "기본", "클리어", "패배" };
            }

            if (id?.Contains("Upgrade") == true)
            {
                return new() { "기본", "잠금", "최대 레벨" };
            }

            if (id == "RewardPopup")
            {
                return new() { "기본", "빈 목록" };
            }

            if (id == "DetailPopup")
            {
                return new() { "기본", "도감 보상 수령", "확률 정보" };
            }

            return new() { "기본" };
        }

        internal static Session CreateSession(GameObject prefab, UiComposition composition, Device device, bool safeArea, string sample, string entryId = null)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var viewport = composition == null ? new Rect(0, 0, 1, 1)
                    : AspectRatioCamera.CalculateViewport(device.Width, device.Height, 9f / 16f);
                var canvasSize = CalculateCanvasSize(device, viewport);
                var container = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(container, scene);
                container.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ((RectTransform)container.transform).sizeDelta = canvasSize;
                GameObject selected;
                if (composition == null)
                {
                    selected = Instantiate(prefab, scene, container.transform);
                    Prepare(selected, true, device, safeArea, viewport, canvasSize);
                }
                else
                {
                    composition.Validate();
                    string sceneName = composition.name.EndsWith("Ui") ? composition.name[..^2] : composition.name;
                    var hostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/Hosts/{sceneName}UiHost.prefab");
                    if (hostPrefab == null)
                    {
                        throw new InvalidOperationException("UI 호스트가 없습니다: " + sceneName);
                    }

                    var host = Instantiate(hostPrefab, scene, container.transform);
                    Prepare(host, false, device, safeArea, viewport, canvasSize);
                    selected = null;
                    foreach (var entry in composition.Entries)
                    {
                        bool isSelected = entryId == null ? entry.Prefab == prefab : entry.Id == entryId;
                        bool chrome = sceneName == "Loading" ? false : sceneName == "Lobby" ? entry.ParentPath == "NavUI/SafeArea" : entry.Id == "HudCanvas" || entry.Id == "SkillHud" || entry.Id == "StageControls";
                        if (!isSelected && !chrome)
                        {
                            continue;
                        }

                        var parent = string.IsNullOrEmpty(entry.ParentPath) ? host.transform : host.transform.Find(entry.ParentPath);
                        if (parent == null)
                        {
                            throw new InvalidOperationException(entry.Id + ": 배치 영역이 없습니다.");
                        }

                        var instance = Instantiate(entry.Prefab, scene, parent);
                        instance.transform.SetSiblingIndex(entry.SiblingIndex);
                        Prepare(instance, isSelected, device, safeArea, viewport, canvasSize);
                        if (isSelected)
                        {
                            selected = instance;
                        }
                    }
                    if (selected == null)
                    {
                        throw new InvalidOperationException("선택한 프리팹이 화면 구성에 없습니다.");
                    }
                }
                ApplySample(selected, sample);
                foreach (var text in container.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate();
                }

                foreach (var layout in container.GetComponentsInChildren<LayoutGroup>(true).Reverse())
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                }

                Canvas.ForceUpdateCanvases();
                var cameraRoot = new GameObject("Preview Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraRoot, scene);
                var camera = cameraRoot.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = canvasSize.y / 2;
                camera.rect = viewport;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.045f, 0.07f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1000;
                return new Session(scene, container, selected, camera, device);
            }
            catch
            {
                EditorSceneManager.ClosePreviewScene(scene);
                throw;
            }
        }

        internal static Texture2D Capture(GameObject prefab, UiComposition composition, Device device, bool safeArea, string sample, string entryId = null)
        {
            using var session = CreateSession(prefab, composition, device, safeArea, sample, entryId);
            return session.Capture();
        }

        // Owns an isolated preview scene; it never advances or kills another scene's tweens.
        internal sealed class Session : IDisposable
        {
            private readonly Scene _scene;
            private readonly GameObject _container;
            private readonly GameObject _selected;
            private readonly Camera _camera;
            private readonly Device _device;
            private readonly List<Tween> _tweens = new();
            private PressScaleAnimator _press;
            private float _releaseAfter;
            private bool _particlesPlaying;
            private bool _disposed;

            internal Session(Scene scene, GameObject container, GameObject selected, Camera camera, Device device)
            { _scene = scene; _container = container; _selected = selected; _camera = camera; _device = device; }

            internal string[] Buttons => _selected.GetComponentsInChildren<Button>(true)
                .Where(b => b.gameObject.activeInHierarchy && b.GetComponentInParent<NoPressFeedback>() == null)
                .Select(b => AnimationUtility.CalculateTransformPath(b.transform, _selected.transform)).ToArray();
            internal bool HasTransition => _selected.GetComponentsInChildren<PopupTransition>(true).Length > 0;
            internal bool HasParticles => _selected.GetComponentsInChildren<ParticleSystem>(true).Length > 0;
            internal bool IsAnimating => _particlesPlaying || _releaseAfter > 0 || _tweens.Any(t => t.IsActive());

            private void OwnTweens(Action action)
            {
                var existing = new HashSet<Tween>(AllTweens());
                action();
                foreach (var tween in AllTweens())
                {
                    if (existing.Contains(tween) || _tweens.Contains(tween)) { continue; }
                    tween.SetUpdate(UpdateType.Manual, true).Play();
                    _tweens.Add(tween);
                }
            }

            private static IEnumerable<Tween> AllTweens() =>
                (DOTween.PlayingTweens() ?? new List<Tween>()).Concat(DOTween.PausedTweens() ?? new List<Tween>());

            internal void Transition(bool show)
            {
                OwnTweens(() =>
                {
                    foreach (var transition in _selected.GetComponentsInChildren<PopupTransition>(true))
                    {
                        if (show) { transition.HideImmediate(); transition.Show(); }
                        else { transition.Hide(); }
                    }
                });
            }

            internal void Press(string path)
            {
                var target = string.IsNullOrEmpty(path) ? _selected.transform : _selected.transform.Find(path);
                if (target == null) { return; }
                _press ??= new PressScaleAnimator();
                OwnTweens(() => _press.Press(target));
                _releaseAfter = 0.2f;
            }

            internal void PlayParticles()
            {
                foreach (var particle in _selected.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particle.gameObject.SetActive(true);
                    particle.Simulate(0f, false, true, false);
                }
                _particlesPlaying = true;
            }

            internal void Advance(float seconds)
            {
                OwnTweens(() =>
                {
                    foreach (var tween in _tweens.ToArray())
                    {
                        if (tween.IsActive())
                        {
                            // The instance extension advances only this tween, including its actual callbacks.
                            // DOTween.ManualUpdate (the global updater) is deliberately never used.
                            tween.ManualUpdate(seconds, seconds);
                            if (tween.IsActive() && tween.IsComplete()) { tween.Kill(); }
                        }
                    }
                    if (_releaseAfter > 0)
                    {
                        _releaseAfter -= seconds;
                        if (_releaseAfter <= 0) { _press?.Release(); }
                    }
                });
                _tweens.RemoveAll(t => !t.IsActive());
                if (_particlesPlaying)
                {
                    var particles = _selected.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (var particle in particles)
                    {
                        // Simulate each system once; recursively simulating every parent would double-step children.
                        particle.Simulate(seconds, false, false, false);
                    }
                    _particlesPlaying = particles.Any(p => p.main.loop || p.time < p.main.duration || p.IsAlive(false));
                }
            }

            internal Texture2D Capture(Texture2D shot = null)
            {
                Canvas.ForceUpdateCanvases();
                var previous = RenderTexture.active;
                var rt = RenderTexture.GetTemporary(_device.Width, _device.Height, 24);
                try
                {
                    RenderTexture.active = rt;
                    GL.Clear(true, true, Color.black);
                    _camera.targetTexture = rt;
                    _camera.Render();
                    shot ??= new Texture2D(_device.Width, _device.Height, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, _device.Width, _device.Height), 0, 0);
                    shot.Apply();
                    return shot;
                }
                finally
                {
                    _camera.targetTexture = null;
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);
                }
            }

            public void Dispose()
            {
                if (_disposed) { return; }
                _disposed = true;
                foreach (var tween in _tweens) { if (tween.IsActive()) { tween.Kill(); } }
                try
                {
                    foreach (var view in _container.GetComponentsInChildren<HudView>(true)) { view.Dispose(); }
                }
                finally { EditorSceneManager.ClosePreviewScene(_scene); }
            }
        }

        internal static Vector2 CalculateCanvasSize(Device device, Rect viewport)
        {
            // Screen Space Camera canvases scale against the camera viewport, including letterboxing.
            var display = new Vector2(device.Width * viewport.width, device.Height * viewport.height);
            float scale = Mathf.Exp(Mathf.Lerp(Mathf.Log(display.x / 1080f), Mathf.Log(display.y / 1920f), 0.5f));
            return display / scale;
        }

        private static GameObject Instantiate(GameObject prefab, Scene scene, Transform parent)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            root.transform.SetParent(parent, false);
            root.SetActive(true);
            return root;
        }

        private static void Prepare(GameObject root, bool showPanel, Device device, bool safeArea, Rect viewport, Vector2 canvasSize)
        {
            UiInitialVisibility.InitializeTree(root);
            root.SetActive(true);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.enabled = false;
                }

                var rect = (RectTransform)canvas.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = canvasSize;
                rect.anchoredPosition = Vector2.zero;
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
            }
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                var so = new SerializedObject(behaviour);
                // Display-only values on the preview clone; no profile, wallet or balance table is changed.
                if (behaviour is PlayerLevelView)
                {
                    if (so.FindProperty("_levelText")?.objectReferenceValue is TMP_Text level) { level.text = "Lv.5"; }
                    if (so.FindProperty("_expText")?.objectReferenceValue is TMP_Text exp) { exp.text = "40 / 180"; }
                    if (so.FindProperty("_expFill")?.objectReferenceValue is Image fill) { fill.fillAmount = 40f / 180f; }
                }
                if (behaviour is EnergyView && so.FindProperty("_value")?.objectReferenceValue is TMP_Text energy)
                { energy.text = "97/100"; }
                if (behaviour is WalletView && so.FindProperty("_value")?.objectReferenceValue is TMP_Text gold)
                { gold.text = "1,215"; }
                if (so.FindProperty("_panel")?.objectReferenceValue is GameObject panel)
                {
                    panel.SetActive(showPanel && behaviour.transform == root.transform);
                }

                if (behaviour is SafeAreaFitter fitter)
                {
                    fitter.enabled = false;
                    var normalized = safeArea ? new Rect(0, (float)device.Bottom / device.Height, 1,
                        1f - (float)(device.Top + device.Bottom) / device.Height) : new Rect(0, 0, 1, 1);
                    SafeAreaFitter.CalculateAnchors(normalized, viewport, out var min, out var max);
                    var safe = (RectTransform)fitter.transform;
                    safe.anchorMin = min;
                    safe.anchorMax = max;
                    safe.offsetMin = safe.offsetMax = Vector2.zero;
                }
            }
            if (root.GetComponentInChildren<PlayerLevelView>(true) != null)
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.name == "Value" && text.transform.parent?.name == "Gem") { text.text = "550"; }
                }
            }
            foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 1;
            }
        }

        private static void ApplySample(GameObject root, string sample)
        {
            if (root.GetComponent<UpgradeConfirmPopupView>() is { } confirmation)
            {
                var so = new SerializedObject(confirmation);
                var data = new GameDataStore();
                var def = data.Upgrades.GetOrThrow("equipment.hat");
                ((TMP_Text)so.FindProperty("_title").objectReferenceValue).text = "장비 일괄 강화";
                ((TMP_Text)so.FindProperty("_description").objectReferenceValue).text = "후드\nLv.0 → Lv.2 · 2회 강화\n아래 재화를 소모해 강화할까요?";
                var icons = (ItemIconTable)so.FindProperty("_itemIcons").objectReferenceValue;
                ((ResultRewardListView)so.FindProperty("_rewards").objectReferenceValue).Show(
                    RewardRows.Build(icons, data, def.CostAt(1) + def.CostAt(2), 0,
                        new[] { new ItemAmount { itemId = def.MaterialItemId, quantity = def.MaterialAt(1) + def.MaterialAt(2) } }));
            }

            if (root.GetComponent<RewardOddsPopupView>() is { } odds)
            {
                var data = new GameDataStore();
                var icons = AssetDatabase.LoadAssetAtPath<ItemIconTable>("Assets/_Project/Data/ItemIconTable.asset");
                var rows = new List<RewardOddsRow>();
                foreach (var odd in RandomRewardOdds.ForExp(ItemIds.RandomSkillMaterial, data, int.MaxValue / 4))
                {
                    rows.Add(new RewardOddsRow(RewardRows.IconOf(icons, data, odd.ItemId), ItemInfo.NameOf(odd.ItemId, data), RandomRewardOdds.Percent(odd.Probability)));
                }

                odds.Show("랜덤 마법북 확률", "현재 레벨에서 열린 스킬의 마법북이 같은 확률로 나와요.", rows);
                ((GameObject)new SerializedObject(odds).FindProperty("_panel").objectReferenceValue).SetActive(true);
            }

            if (root.GetComponent<DetailPopupView>() is { } detailView && sample != "기본")
            {
                var detailSo = new SerializedObject(detailView);
                SetComponentActive(detailSo, "_claimButton", sample == "도감 보상 수령");
                SetComponentActive(detailSo, "_oddsButton", sample == "확률 정보");
                SetComponentActive(detailSo, "_goButton", false);
                var data = new GameDataStore();
                var icons = AssetDatabase.LoadAssetAtPath<ItemIconTable>("Assets/_Project/Data/ItemIconTable.asset");
                if (sample == "도감 보상 수령")
                {
                    detailView.Show(AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Sprites/Generated/Monsters/Slime.png").OfType<Sprite>().FirstOrDefault(), "슬라임", "일반 · 근접 공격", "· 쓰러지면 작은 슬라임 2마리로 나뉘어요");
                    SetComponentActive(detailSo, "_claimButton", true);
                    ((GameObject)detailSo.FindProperty("_claimBadge").objectReferenceValue)?.SetActive(true);
                }
                else
                {
                    detailView.Show(icons.GetRandom(ItemIds.RandomSkillMaterial), "랜덤 마법북", "최대 6개 획득", ItemInfo.DescriptionOf(ItemIds.RandomSkillMaterial));
                    SetComponentActive(detailSo, "_oddsButton", true);
                }
            }

            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                var so = new SerializedObject(behaviour);
                SetActive(so, "_lockOverlay", sample == "잠금");
                SetActive(so, "_lock", sample == "잠금");
                SetActive(so, "_maxLevel", sample == "최대 레벨");
                SetActive(so, "_cost", sample != "최대 레벨");
                SetActive(so, "_next", sample != "최대 레벨");
                if (behaviour is ResultRewardListView || behaviour is StageMonsterListView || behaviour is ResultDamageListView)
                {
                    behaviour.gameObject.SetActive(sample != "빈 목록");
                }

                if (sample is "클리어" or "패배")
                {
                    if (so.FindProperty("_titleText")?.objectReferenceValue is TMP_Text text)
                    {
                        text.text = sample == "클리어" ? "Clear !" : "Fail !";
                    }

                    if (behaviour is ResultTipView)
                    {
                        behaviour.gameObject.SetActive(sample == "패배");
                    }

                    if (behaviour is ResultStarsView stars)
                    {
                        stars.Show(sample == "클리어", sample == "클리어" ? 3 : 0);
                    }
                }
            }
        }

        private static void SetComponentActive(SerializedObject so, string property, bool active)
        {
            if (so.FindProperty(property)?.objectReferenceValue is Component component)
            {
                component.gameObject.SetActive(active);
            }
        }

        private static void SetActive(SerializedObject so, string property, bool active)
        {
            if (so.FindProperty(property)?.objectReferenceValue is GameObject go)
            {
                go.SetActive(active);
            }
        }
    }
}
