using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Game.Editor.MonsterRig.MonsterRigBuilder;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 박쥐: bat_parts.png(날개 2장 + 몸통이 떨어져 놓인 그림)에서 파츠 3개를 투명 영역으로 분리하고,
    /// 날개 안쪽 끝이 몸통 위쪽 양옆에 살짝 겹치도록 조립한다. 날개는 본 2개(안쪽 → 끝), 몸통은 본 1개.
    /// </summary>
    public static class BatRig
    {
        private const string Png = "Assets/_Project/Art/Characters/Monsters/bat_parts.png";
        private const int Pad = 4;
        private const float Overlap = 8f; // 날개가 몸통 안쪽으로 파고드는 양(px)

        internal sealed class Art
        {
            public Vector2Int Size;
            public Vector2 Pivot;
            public Color32[] Canvas;
            public List<LayerSpec> Layers;
            public BoneSpec[] Bones;
            public Vector2 Mouth; // 초음파가 나오는 곳 (캔버스 픽셀)
        }

        internal static Art Assemble()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var src = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);

            // 투명 영역으로 덩어리 나누기 → 큰 3개: 가장 아래 = 몸통, 나머지 왼쪽/오른쪽 = 날개
            var parts = Components(src, w, h).OrderByDescending(c => c.Count).Take(3).ToList();
            if (parts.Count < 3)
            {
                throw new System.InvalidOperationException("bat_parts.png에서 파츠 3개를 찾지 못함");
            }

            Vector2 Center(List<int> c) => c.Aggregate(Vector2.zero, (s, i) => s + new Vector2(i % w, i / w)) / c.Count;
            var body = parts.OrderBy(c => Center(c).y).First();
            var wings = parts.Where(c => c != body).OrderBy(c => Center(c).x).ToList();
            var (wingL, wingR) = (wings[0], wings[1]);

            var bodyBox = Box(body, w);
            float bodyW = bodyBox.z - bodyBox.x, bodyH = bodyBox.w - bodyBox.y;
            var anchorL = new Vector2(bodyBox.x + Overlap, bodyBox.y + bodyH * 0.7f);
            var anchorR = new Vector2(bodyBox.z - Overlap, bodyBox.y + bodyH * 0.7f);

            // 날개의 안쪽 끝(몸 쪽)과 바깥 끝(날개 끝)
            var innerL = Edge(wingL, w, right: true);
            var tipL = Edge(wingL, w, right: false);
            var innerR = Edge(wingR, w, right: false);
            var tipR = Edge(wingR, w, right: true);
            var moveL = anchorL - innerL;
            var moveR = anchorR - innerR;

            // 조립 후 영역 → 캔버스 크기
            var all = new[]
            {
                (body, Vector2.zero), (wingL, moveL), (wingR, moveR),
            };
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var (c, m) in all)
            {
                var b = Box(c, w);
                minX = Mathf.Min(minX, b.x + m.x); minY = Mathf.Min(minY, b.y + m.y);
                maxX = Mathf.Max(maxX, b.z + m.x); maxY = Mathf.Max(maxY, b.w + m.y);
            }

            var origin = new Vector2(Mathf.Floor(minX) - Pad, Mathf.Floor(minY) - Pad);
            int cw = Mathf.CeilToInt(maxX - origin.x) + Pad, ch = Mathf.CeilToInt(maxY - origin.y) + Pad;

            Color32[] Place(List<int> c, Vector2 move)
            {
                var layer = new Color32[cw * ch];
                int dx = Mathf.RoundToInt(move.x - origin.x), dy = Mathf.RoundToInt(move.y - origin.y);
                foreach (int i in c)
                {
                    int x = i % w + dx, y = i / w + dy;
                    if (x >= 0 && y >= 0 && x < cw && y < ch)
                    {
                        layer[y * cw + x] = src[i];
                    }
                }

                return layer;
            }

            var bodyPx = Place(body, Vector2.zero);
            var wingLPx = Place(wingL, moveL);
            var wingRPx = Place(wingR, moveR);
            Vector2 C(Vector2 p) => p - origin; // 원본 좌표 → 캔버스 좌표

            var iL = C(anchorL); var tL = C(tipL + moveL);
            var iR = C(anchorR); var tR = C(tipR + moveR);
            var bodyCenter = C(new Vector2((bodyBox.x + bodyBox.z) * 0.5f, (bodyBox.y + bodyBox.w) * 0.5f));
            var size = new Vector2Int(cw, ch);

            var composite = new Color32[cw * ch];
            foreach (var layer in new[] { wingLPx, wingRPx, bodyPx })
            {
                for (int i = 0; i < composite.Length; i++)
                {
                    if (layer[i].a > 0)
                    {
                        composite[i] = layer[i];
                    }
                }
            }

            return new Art
            {
                Size = size,
                Pivot = bodyCenter,
                Canvas = composite,
                Mouth = C(new Vector2((bodyBox.x + bodyBox.z) * 0.5f, bodyBox.y + bodyH * 0.25f)),
                Layers = new List<LayerSpec>
                {
                    new() { Name = "bat_wing_l", Pixels = wingLPx, PixelsSize = size, Bones = new[] { "WingL1", "WingL2" } },
                    new() { Name = "bat_wing_r", Pixels = wingRPx, PixelsSize = size, Bones = new[] { "WingR1", "WingR2" } },
                    new() { Name = "bat_body", Pixels = bodyPx, PixelsSize = size, Bones = new[] { "Body" } },
                },
                Bones = new[]
                {
                    MonsterRigs.Bone("Body", null, bodyCenter.x, bodyCenter.y - bodyH * 0.3f, bodyCenter.x, bodyCenter.y + bodyH * 0.3f),
                    MonsterRigs.Bone("WingL1", "Body", iL.x, iL.y, (iL.x + tL.x) * 0.5f, (iL.y + tL.y) * 0.5f),
                    MonsterRigs.Bone("WingL2", "WingL1", (iL.x + tL.x) * 0.5f, (iL.y + tL.y) * 0.5f, tL.x, tL.y),
                    MonsterRigs.Bone("WingR1", "Body", iR.x, iR.y, (iR.x + tR.x) * 0.5f, (iR.y + tR.y) * 0.5f),
                    MonsterRigs.Bone("WingR2", "WingR1", (iR.x + tR.x) * 0.5f, (iR.y + tR.y) * 0.5f, tR.x, tR.y),
                },
            };
        }

        // ---------- 초음파 (클립 안에서 움직이는 링 스프라이트 3개) ----------

        private const string SonicRoot = "SonicFx";
        private const int Waves = 3;
        private const float WaveStart = 0.24f, WaveGap = 0.07f, WaveLife = 0.32f, WaveTravel = 0.55f;
        private const float StartScale = 0.3f, EndScale = 1.1f, Flatten = 0.5f;
        private static readonly Color WaveColor = new(0.8f, 0.95f, 1f, 0.9f);
        private const string RingPath = "Assets/_Project/Art/VFX/Enemy/fx_ring.png";

        // Animator가 붙은 Body 아래에 투명한 링 3개를 둔다 (Attack 클립만 보이게 만든다).
        internal static void CreateSonicObjects(GameObject body, Art art)
        {
            var sprite = RingSprite();
            var root = new GameObject(SonicRoot);
            root.transform.SetParent(body.transform, false);
            root.transform.localPosition = (art.Mouth - art.Pivot) / 200f;
            for (int i = 0; i < Waves; i++)
            {
                var wave = new GameObject("Wave" + i);
                wave.transform.SetParent(root.transform, false);
                wave.transform.localScale = new Vector3(StartScale, StartScale * Flatten, 1f);
                var r = wave.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.color = new Color(WaveColor.r, WaveColor.g, WaveColor.b, 0f);
                r.sortingOrder = 10;
            }
        }

        // 링마다: 시작 시간에 나타나 → 아래로 날아가며 커지고 → 사라짐. (위치/크기/색은 성분을 모두 넣는다)
        internal static void AddSonicCurves(AnimationClip clip)
        {
            void Set(string path, System.Type type, string prop, params (float t, float v)[] keys)
            {
                var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
                for (int i = 0; i < curve.length; i++)
                {
                    UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Linear);
                    UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Linear);
                }

                UnityEditor.AnimationUtility.SetEditorCurve(clip, UnityEditor.EditorCurveBinding.FloatCurve(path, type, prop), curve);
            }

            for (int i = 0; i < Waves; i++)
            {
                string path = $"{SonicRoot}/Wave{i}";
                float t0 = WaveStart + WaveGap * i, t1 = t0 + WaveLife;
                Set(path, typeof(Transform), "m_LocalPosition.y", (0f, 0f), (t0, 0f), (t1, -WaveTravel));
                Set(path, typeof(Transform), "m_LocalScale.x", (0f, StartScale), (t0, StartScale), (t1, EndScale));
                Set(path, typeof(Transform), "m_LocalScale.y", (0f, StartScale * Flatten), (t0, StartScale * Flatten), (t1, EndScale * Flatten));
                Set(path, typeof(Transform), "m_LocalScale.z", (0f, 1f), (t1, 1f));
                Set(path, typeof(SpriteRenderer), "m_Color.r", (0f, WaveColor.r), (t1, WaveColor.r));
                Set(path, typeof(SpriteRenderer), "m_Color.g", (0f, WaveColor.g), (t1, WaveColor.g));
                Set(path, typeof(SpriteRenderer), "m_Color.b", (0f, WaveColor.b), (t1, WaveColor.b));
                Set(path, typeof(SpriteRenderer), "m_Color.a", (0f, 0f), (t0, 0f), (t0 + 0.03f, WaveColor.a), (t1, 0f));
            }

            UnityEditor.EditorUtility.SetDirty(clip);
        }

        // fx_ring.png를 스프라이트로 (지름 1유닛)
        private static Sprite RingSprite()
        {
            if (!File.Exists(Path.GetFullPath(RingPath)))
            {
                GroundSlamFx.BuildSonic();
            }

            var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(RingPath);
            if (importer.textureType != UnityEditor.TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, 64f))
            {
                importer.textureType = UnityEditor.TextureImporterType.Sprite;
                importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.SaveAndReimport();
            }

            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        }

        public static void WritePreview(string path)
        {
            var art = Assemble();
            var tex = new Texture2D(art.Size.x, art.Size.y, TextureFormat.RGBA32, false);
            tex.SetPixels32(art.Canvas);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        // ---------- helpers ----------

        private static List<List<int>> Components(Color32[] px, int w, int h)
        {
            var seen = new bool[px.Length];
            var result = new List<List<int>>();
            var queue = new Queue<int>();
            for (int s = 0; s < px.Length; s++)
            {
                if (seen[s] || px[s].a <= 8)
                {
                    continue;
                }

                var comp = new List<int>();
                seen[s] = true;
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    comp.Add(i);
                    int x = i % w, y = i / w;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                            {
                                continue;
                            }

                            int j = ny * w + nx;
                            if (!seen[j] && px[j].a > 8) { seen[j] = true; queue.Enqueue(j); }
                        }
                    }
                }

                result.Add(comp);
            }

            return result;
        }

        // (minX, minY, maxX, maxY)
        private static Vector4 Box(List<int> c, int w) =>
            new(c.Min(i => i % w), c.Min(i => i / w), c.Max(i => i % w), c.Max(i => i / w));

        // 덩어리의 가장 오른쪽(right) 또는 왼쪽 끝 6px 띠의 평균 점
        private static Vector2 Edge(List<int> c, int w, bool right)
        {
            int edge = right ? c.Max(i => i % w) : c.Min(i => i % w);
            var band = c.Where(i => Mathf.Abs(i % w - edge) <= 6).ToList();
            return new Vector2(edge, (float)band.Average(i => i / w));
        }
    }
}
