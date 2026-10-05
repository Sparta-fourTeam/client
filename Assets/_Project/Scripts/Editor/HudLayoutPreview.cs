using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>HudCanvas 프리팹을 여러 기기 해상도로 렌더링해서 PNG로 저장한다.
    /// Game 뷰는 한 번에 한 비율만 보여주므로, 해상도별 겹침과 잘림을 한눈에 비교하려고 만들었다.
    /// CanvasScaler(1080x1920, Match 0.5)가 기기마다 만드는 Canvas 크기를 같은 식으로 계산하고,
    /// 노치와 홈 인디케이터는 Safe Area 여백으로 흉내 낸다 (빨간 띠가 여백). 실제 기기 동작은 Device Simulator로 확인한다</summary>
    public static class HudLayoutPreview
    {
        private const string HudPrefabPath = "Assets/_Project/Prefabs/HudCanvas.prefab";
        private const float ReferenceWidth = 1080f;
        private const float ReferenceHeight = 1920f;
        private const float MatchWidthOrHeight = 0.5f;

        private readonly struct Device
        {
            public readonly string Name;
            public readonly int Width;
            public readonly int Height;
            // 픽셀 단위 Safe Area 여백
            public readonly int Left;
            public readonly int Right;
            public readonly int Top;
            public readonly int Bottom;

            public Device(string name, int width, int height, int left, int right, int top, int bottom)
            {
                Name = name;
                Width = width;
                Height = height;
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }
        }

        private static readonly Device[] Devices =
        {
            new Device("01_16x9_1080x1920", 1080, 1920, 0, 0, 0, 0),
            new Device("02_iPhone_1170x2532", 1170, 2532, 0, 0, 141, 102),
            new Device("03_Tall21x9_1080x2520", 1080, 2520, 0, 0, 100, 60),
            new Device("04_Android_720x1600", 720, 1600, 0, 0, 60, 0),
            new Device("05_Small_720x1280", 720, 1280, 0, 0, 0, 0),
            new Device("06_Tablet3x4_1536x2048", 1536, 2048, 0, 0, 0, 0),
        };

        [MenuItem("Tools/Project Nova/HUD Layout Preview")]
        private static void Run()
        {
            Debug.Log($"[HudLayoutPreview] {RenderAll()}");
        }

        /// <summary>모든 기기 프리셋을 렌더링하고 저장 폴더 경로를 돌려준다</summary>
        public static string RenderAll()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "HudPreview");
            Directory.CreateDirectory(dir);

            var shots = new List<Texture2D>();
            foreach (Device device in Devices)
            {
                Texture2D shot = Render(device, out float canvasWidth, out float canvasHeight);
                File.WriteAllBytes(Path.Combine(dir, device.Name + ".png"), shot.EncodeToPNG());
                Debug.Log($"[HudLayoutPreview] {device.Name}: Canvas {canvasWidth:0}x{canvasHeight:0}");
                shots.Add(shot);
            }

            Texture2D sheet = BuildSheet(shots, 900);
            File.WriteAllBytes(Path.Combine(dir, "sheet.png"), sheet.EncodeToPNG());

            foreach (Texture2D shot in shots)
            {
                Object.DestroyImmediate(shot);
            }

            Object.DestroyImmediate(sheet);
            return dir;
        }

        private static Texture2D Render(Device device, out float canvasWidth, out float canvasHeight)
        {
            // CanvasScaler의 Scale With Screen Size: 폭과 높이 비율을 Match 값으로 섞어 배율을 정한다
            float scale = Mathf.Exp(Mathf.Lerp(
                Mathf.Log(device.Width / ReferenceWidth),
                Mathf.Log(device.Height / ReferenceHeight),
                MatchWidthOrHeight));
            canvasWidth = device.Width / scale;
            canvasHeight = device.Height / scale;

            Scene scene = EditorSceneManager.NewPreviewScene();
            PrefabUtility.LoadPrefabContentsIntoPreviewScene(HudPrefabPath, scene);
            GameObject root = scene.GetRootGameObjects()[0];
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.enabled = false;
                }

                var rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.sizeDelta = new Vector2(canvasWidth, canvasHeight);
                rootRect.localScale = Vector3.one;
                rootRect.localPosition = Vector3.zero;

                ApplySafeArea(rootRect, device, scale);
                FillSampleData(root.transform);
                RebuildLayout(root);

                return Capture(scene, canvasWidth, canvasHeight);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // SafeAreaFitter가 하는 일을 여백 값으로 직접 한다. 여백 영역은 빨간 띠로 그린다
        private static void ApplySafeArea(RectTransform root, Device device, float scale)
        {
            var safe = (RectTransform)root.Find("SafeArea");
            safe.anchorMin = new Vector2((float)device.Left / device.Width, (float)device.Bottom / device.Height);
            safe.anchorMax = new Vector2(1f - (float)device.Right / device.Width, 1f - (float)device.Top / device.Height);
            safe.offsetMin = Vector2.zero;
            safe.offsetMax = Vector2.zero;

            AddInsetBand(root, "InsetTop", new Vector2(0, 1), new Vector2(1, 1), device.Top / scale);
            AddInsetBand(root, "InsetBottom", new Vector2(0, 0), new Vector2(1, 0), device.Bottom / scale);
        }

        private static void AddInsetBand(RectTransform root, string name, Vector2 anchorMin, Vector2 anchorMax, float height)
        {
            if (height <= 0f)
            {
                return;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMin.y);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, height);
            go.GetComponent<Image>().color = new Color(1f, 0.15f, 0.15f, 0.45f);
        }

        // 실제 게임에서 들어올 값의 대표 샘플. 가장 긴 문자열에 가깝게 잡아 겹침을 잡는다
        private static void FillSampleData(Transform root)
        {
            SetText(root, "SafeArea/StageLabel", "Stage 12");
            SetText(root, "SafeArea/Wave/Timer", "12:41");
            SetText(root, "SafeArea/Wave/WaveTitle/Wave_Value", "13/20");
            SetText(root, "SafeArea/WallHp/HpText", "1000 / 1000");
            SetFill(root, "SafeArea/Wave/GaugeFill", 0.6f);
            SetFill(root, "SafeArea/WallHp/HpFill", 0.75f);
        }

        private static void SetText(Transform root, string path, string text)
        {
            Transform target = root.Find(path);
            if (target != null && target.TryGetComponent(out TMP_Text tmp))
            {
                tmp.text = text;
            }
        }

        private static void SetFill(Transform root, string path, float amount)
        {
            Transform target = root.Find(path);
            if (target != null && target.TryGetComponent(out Image image))
            {
                image.fillAmount = amount;
            }
        }

        // 미리보기 씬에서는 캔버스 갱신이 자동으로 돌지 않아서 텍스트 메시와 레이아웃을 직접 갱신한다
        private static void RebuildLayout(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.ForceMeshUpdate();
            }

            foreach (LayoutGroup group in root.GetComponentsInChildren<LayoutGroup>(true))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static Texture2D Capture(Scene scene, float canvasWidth, float canvasHeight)
        {
            int width = Mathf.RoundToInt(canvasWidth);
            int height = Mathf.RoundToInt(canvasHeight);

            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = canvasHeight * 0.5f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.2f, 0.27f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;

            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        // 기기별 결과를 같은 높이로 줄여 가로로 나란히 붙인 한 장
        private static Texture2D BuildSheet(List<Texture2D> shots, int height)
        {
            const int gap = 16;
            int totalWidth = gap;
            var widths = new List<int>();
            foreach (Texture2D shot in shots)
            {
                int w = Mathf.RoundToInt(shot.width * (height / (float)shot.height));
                widths.Add(w);
                totalWidth += w + gap;
            }

            var sheet = new Texture2D(totalWidth, height + gap * 2, TextureFormat.RGBA32, false);
            var background = new Color[sheet.width * sheet.height];
            for (int i = 0; i < background.Length; i++)
            {
                background[i] = new Color(0.08f, 0.08f, 0.08f, 1f);
            }

            sheet.SetPixels(background);

            int x = gap;
            for (int s = 0; s < shots.Count; s++)
            {
                Texture2D shot = shots[s];
                for (int py = 0; py < height; py++)
                {
                    for (int px = 0; px < widths[s]; px++)
                    {
                        sheet.SetPixel(x + px, gap + py, shot.GetPixelBilinear(px / (float)widths[s], py / (float)height));
                    }
                }

                x += widths[s] + gap;
            }

            sheet.Apply();
            return sheet;
        }
    }
}
