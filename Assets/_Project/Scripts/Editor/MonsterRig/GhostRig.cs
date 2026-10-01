using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Game.Editor.MonsterRig.MonsterRigBuilder;
using static Game.Editor.MonsterRig.MonsterRigs;
using Pose = Game.Editor.MonsterRig.MonsterRigs.Pose;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 유령(ghost_v2.png, 1024px → 1/4로 축소): 몸통 한 장(본 6개) + 얼굴 십자를 떼어낸 4조각(본 4개).
    /// Move: 왼→오른쪽 바람에 펄럭임 / Attack: 십자가 열려 구멍 → 바람을 빨아들임 / Die: 파랗게 위로 떠오르며 성불.
    /// 좌표는 축소된 이미지(256x245) 픽셀, 좌하단 원점.
    /// </summary>
    public static class GhostRig
    {
        private const string Png = "Assets/_Project/Art/Monsters/ghost_v2.png";
        private const int Scale = 4;
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;
        private static readonly string[] Arms = { "CrossU", "CrossD", "CrossL", "CrossR" };

        private sealed class Art
        {
            public Vector2Int Size;
            public Color32[] Body;
            public Color32[][] Cross; // U, D, L, R
            public Vector2 CrossCenter;
        }

        private static Art s_art;

        internal static MonsterRigs.Def Def()
        {
            var art = s_art ??= Prepare();
            var cc = art.CrossCenter;
            var pivot = new Vector2(128, 122);
            const float walk = 2f;

            var bones = new List<BoneSpec>
            {
                Bone("Root", null, 130, 132, 122, 175),
                Bone("Head", "Root", 122, 175, 118, 222),
                Bone("Tail", "Root", 150, 104, 170, 37),
                Bone("TailM", "Root", 120, 104, 112, 47),
                Bone("WispL", "Root", 92, 134, 54, 94),
                Bone("WispR", "Root", 165, 139, 219, 115),
                Bone("CrossU", "Head", cc.x, cc.y, cc.x, cc.y + 15),
                Bone("CrossD", "Head", cc.x, cc.y, cc.x, cc.y - 15),
                Bone("CrossL", "Head", cc.x, cc.y, cc.x - 15, cc.y),
                Bone("CrossR", "Head", cc.x, cc.y, cc.x + 15, cc.y),
            };

            // 바람(왼→오른쪽): 몸이 오른쪽으로 기울어 흔들리고, 자락은 앞(왼쪽)에서 뒤(오른쪽)로 물결이 전해지듯 늦게 펄럭인다.
            Pose Move(float t)
            {
                float ph = t * Tau / walk;
                float s = Mathf.Sin(ph);
                return P()
                    .B("Root", dx: 0.012f * s, dy: 0.02f * Mathf.Sin(ph + 1f), rot: -3f - 2f * s)
                    .B("Head", rot: -2f * Mathf.Sin(ph - 0.5f))
                    .B("WispL", rot: 10f * Mathf.Sin(ph - 0.6f))
                    .B("TailM", rot: 7f * Mathf.Sin(ph - 1.0f) + 2f * Mathf.Sin(2f * ph - 1.2f))
                    .B("WispR", rot: -6f + 10f * Mathf.Sin(ph - 1.3f))
                    .B("Tail", rot: -4f + 9f * Mathf.Sin(ph - 1.7f) + 3f * Mathf.Sin(2f * ph - 2f));
            }

            // 십자가 벌어지는 양(유닛)과 몸 자세
            Pose Suck(float open, float lean, float wisps)
            {
                return P()
                    .B("Root", dy: 0.01f * open / 0.035f, rot: lean)
                    .B("WispL", rot: wisps).B("WispR", rot: -wisps).B("Tail", rot: -wisps * 0.5f)
                    .B("CrossU", dy: open).B("CrossD", dy: -open).B("CrossL", dx: -open).B("CrossR", dx: open);
            }

            Pose Rise(float dy, float stretch, float wisps, Color color) =>
                P().B("Root", dy: dy).B("Head", dy: stretch).B("WispL", rot: -wisps).B("WispR", rot: wisps).Colored(color);

            return new MonsterRigs.Def
            {
                Name = "Ghost",
                Size = art.Size,
                PivotPx = pivot,
                CustomLayers = () => Layers(art),
                GridStep = 8,
                Bones = bones.ToArray(),
                MoveLength = walk,
                MoveKeys = 8,
                Move = Move,
                // 십자가 바깥으로 벌어지며 사라지고 → 그 자리에 구멍이 열려 바람을 빨아들임 → 구멍이 닫히고 십자가 돌아옴
                Attack = new[]
                {
                    (0f, P()),
                    (0.12f, Suck(0.01f, 3f, 6f)),
                    (0.25f, Suck(0.035f, 4f, 12f)),
                    (0.5f, Suck(0.035f, 5f, 18f)),
                    (0.72f, Suck(0.035f, 3f, 8f)),
                    (0.85f, P()),
                },
                AttackExtra = clip =>
                {
                    ClipFx.AddCurves(clip, SuckRoot, SuckTracks());
                    CrossFade(clip);
                },
                OnAttach = body => ClipFx.CreateObjects(body, SuckRoot, (cc - pivot) / Ppu, SuckTracks()),
                // 성불: 하늘색 → 파란색으로 물들며 수직으로 떠올라 몸이 위로 살짝 늘어나고 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.25f, Rise(0.06f, 0.02f, 10f, new Color(0.75f, 0.88f, 1f, 1f))),
                    (0.6f, Rise(0.3f, 0.05f, 16f, new Color(0.5f, 0.72f, 1f, 0.75f))),
                    (1.1f, Rise(0.7f, 0.08f, 20f, new Color(0.4f, 0.65f, 1f, 0f))),
                },
            };
        }

        // ---------- 구멍 + 빨려 드는 바람 (Attack 클립 안 스프라이트) ----------

        private const string SuckRoot = "SuckFx";

        private static List<ClipFx.Track> SuckTracks()
        {
            string hole = ClipFx.Generated("fx_hole", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return new Color(0.08f, 0.08f, 0.13f, Mathf.Clamp01((1f - r) / 0.12f));
            });
            string streak = ClipFx.Generated("fx_streak", (u, v) =>
            {
                float a = Mathf.Pow(Mathf.Clamp01(1f - v * v * 9f), 2f) * Mathf.Clamp01(1f - Mathf.Abs(u)) * Mathf.Clamp01((u + 1f) * 1.5f);
                return new Color(1f, 1f, 1f, a);
            });

            var tracks = new List<ClipFx.Track>();
            var dark = new Color(1f, 1f, 1f, 1f);
            var tiny = Vector2.one * 0.01f;
            var open = Vector2.one * 0.22f;
            tracks.Add(new ClipFx.Track
            {
                Name = "Hole",
                Sprite = hole,
                Order = 6,
                Keys = new[]
                {
                    (0f, Vector2.zero, tiny, ClipFx.Alpha(dark, 0f)),
                    (0.12f, Vector2.zero, tiny, dark),
                    (0.25f, Vector2.zero, open, dark),
                    (0.72f, Vector2.zero, open, dark),
                    (0.85f, Vector2.zero, tiny, ClipFx.Alpha(dark, 0f)),
                },
            });

            var wind = new Color(0.5f, 0.62f, 0.8f, 0.9f);
            for (int k = 0; k < 8; k++)
            {
                float angle = k * 45f + 20f;
                var dir = (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector2.right);
                float t0 = 0.25f + 0.03f * k;
                var from = dir * 0.42f;
                var to = dir * 0.06f;
                tracks.Add(new ClipFx.Track
                {
                    // 스트릭 텍스처는 오른쪽(+u)이 머리라서, 안쪽(구멍)을 향하도록 180° 돌린다
                    Name = "Wind" + k,
                    Sprite = streak,
                    Order = 7,
                    Rotation = angle + 180f,
                    Keys = new[]
                    {
                        (0f, from, new Vector2(0.3f, 0.09f), ClipFx.Alpha(wind, 0f)),
                        (t0, from, new Vector2(0.3f, 0.09f), ClipFx.Alpha(wind, 0f)),
                        (t0 + 0.06f, Vector2.Lerp(from, to, 0.25f), new Vector2(0.28f, 0.09f), wind),
                        (t0 + 0.3f, to, new Vector2(0.08f, 0.05f), ClipFx.Alpha(wind, 0f)),
                    },
                });
            }

            return tracks;
        }

        // 십자 조각은 벌어지며 사라졌다가 구멍이 닫힐 때 돌아온다 (렌더러 알파만, rgb는 그대로)
        private static void CrossFade(AnimationClip clip)
        {
            foreach (var arm in Arms)
            {
                string path = "ghost_" + arm.ToLowerInvariant();
                foreach (var ch in new[] { "r", "g", "b" })
                {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SpriteRenderer), "m_Color." + ch),
                        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f)));
                }

                var alpha = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.12f, 1f), new Keyframe(0.25f, 0f),
                    new Keyframe(0.72f, 0f), new Keyframe(0.85f, 1f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SpriteRenderer), "m_Color.a"), alpha);
            }
        }

        // ---------- 그림 준비 ----------

        private static List<LayerSpec> Layers(Art art)
        {
            var layers = new List<LayerSpec>
            {
                new() { Name = "ghost_body", Pixels = art.Body, PixelsSize = art.Size, Bones = new[] { "Root", "Head", "Tail", "TailM", "WispL", "WispR" } },
            };
            for (int i = 0; i < Arms.Length; i++)
            {
                layers.Add(new LayerSpec { Name = "ghost_" + Arms[i].ToLowerInvariant(), Pixels = art.Cross[i], PixelsSize = art.Size, Bones = new[] { Arms[i] } });
            }

            return layers;
        }

        // 원본에서 십자(얼굴 안의 어두운 획)를 떼어 4조각으로 나누고, 떼어낸 자리는 주변 색으로 메운 뒤 1/4로 줄인다.
        private static Art Prepare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var src = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);

            float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            bool Dark(int i) => src[i].a > 128 && Lum(src[i]) < 0.45f;

            // 얼굴 범위(원본, 좌하단 원점) 안에 완전히 들어오는 어두운 덩어리 = 십자
            var face = new RectInt(370, h - 1 - 425, 225, 240);
            var cross = new bool[src.Length];
            var seen = new bool[src.Length];
            var queue = new Queue<int>();
            for (int s = 0; s < src.Length; s++)
            {
                if (seen[s] || !Dark(s))
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
                            if (!seen[j] && Dark(j)) { seen[j] = true; queue.Enqueue(j); }
                        }
                    }
                }

                bool inside = comp.Count >= 150 && comp.All(i => face.Contains(new Vector2Int(i % w, i / w)));
                if (inside)
                {
                    foreach (int i in comp)
                    {
                        cross[i] = true;
                    }
                }
            }

            // 획 주변 회색 번짐까지 포함
            var grown = (bool[])cross.Clone();
            for (int pass = 0; pass < 5; pass++)
            {
                var next = (bool[])grown.Clone();
                for (int i = 0; i < src.Length; i++)
                {
                    if (!grown[i])
                    {
                        continue;
                    }

                    int x = i % w, y = i / w;
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && src[ny * w + nx].a > 128 && Lum(src[ny * w + nx]) < 0.96f)
                        {
                            next[ny * w + nx] = true;
                        }
                    }
                }

                grown = next;
            }

            // 십자 중심: 가장 많이 겹친 열(세로 획)과 행(가로 획)
            var cols = new int[w];
            var rows = new int[h];
            for (int i = 0; i < src.Length; i++)
            {
                if (cross[i]) { cols[i % w]++; rows[i / w]++; }
            }

            var center = new Vector2(System.Array.IndexOf(cols, cols.Max()), System.Array.IndexOf(rows, rows.Max()));

            // 몸통: 십자 자리를 얼굴의 흰 바탕색(주변 밝은 픽셀 평균)으로 메운다
            var body = (Color32[])src.Clone();
            float fr = 0, fg = 0, fb = 0;
            int fn = 0;
            for (int i = 0; i < src.Length; i++)
            {
                if (grown[i] || src[i].a < 250 || Lum(src[i]) < 0.9f || !face.Contains(new Vector2Int(i % w, i / w)))
                {
                    continue;
                }

                fr += src[i].r; fg += src[i].g; fb += src[i].b; fn++;
            }

            var paper = new Color32((byte)(fr / fn), (byte)(fg / fn), (byte)(fb / fn), 255);
            for (int i = 0; i < src.Length; i++)
            {
                if (grown[i])
                {
                    body[i] = paper;
                }
            }

            // 십자 4조각 (중심 기준 가로/세로 우세 방향)
            var arms = Enumerable.Range(0, 4).Select(_ => new Color32[src.Length]).ToArray();
            for (int i = 0; i < src.Length; i++)
            {
                if (!grown[i])
                {
                    continue;
                }

                var d = new Vector2(i % w, i / w) - center;
                int arm = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x < 0 ? 2 : 3) : (d.y > 0 ? 0 : 1);
                arms[arm][i] = src[i];
            }

            int ow = w / Scale, oh = h / Scale;
            return new Art
            {
                Size = new Vector2Int(ow, oh),
                Body = Downscale(body, w, h),
                Cross = arms.Select(a => DownscaleStroke(a, w, h)).ToArray(),
                CrossCenter = center / Scale,
            };
        }

        // 4x4 평균 (알파 가중)
        private static Color32[] Downscale(Color32[] px, int w, int h)
        {
            int ow = w / Scale, oh = h / Scale;
            var result = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
            {
                for (int x = 0; x < ow; x++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int dy = 0; dy < Scale; dy++)
                    {
                        for (int dx = 0; dx < Scale; dx++)
                        {
                            var c = px[(y * Scale + dy) * w + x * Scale + dx];
                            float ca = c.a / 255f;
                            r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca;
                        }
                    }

                    result[y * ow + x] = a > 0f
                        ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / (Scale * Scale) * 255f))
                        : new Color32(0, 0, 0, 0);
                }
            }

            return result;
        }

        // 선(획)은 평균하면 흐려지므로, 4x4 칸에서 가장 진한 픽셀을 남긴다.
        private static Color32[] DownscaleStroke(Color32[] px, int w, int h)
        {
            int ow = w / Scale, oh = h / Scale;
            var result = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
            {
                for (int x = 0; x < ow; x++)
                {
                    Color32 best = default;
                    for (int dy = 0; dy < Scale; dy++)
                    {
                        for (int dx = 0; dx < Scale; dx++)
                        {
                            var c = px[(y * Scale + dy) * w + x * Scale + dx];
                            if (c.a > best.a)
                            {
                                best = c;
                            }
                        }
                    }

                    result[y * ow + x] = best;
                }
            }

            return result;
        }

        /// <summary>준비된 그림 확인용: 몸통(십자 제거) + 십자 조각을 색으로 표시, 본 위치 점</summary>
        public static string WritePreview(string path)
        {
            s_art = null;
            var art = s_art = Prepare();
            var tex = new Texture2D(art.Size.x * 2, art.Size.y, TextureFormat.RGBA32, false);
            Color[] tint = { Color.red, Color.green, Color.blue, Color.yellow };
            for (int y = 0; y < art.Size.y; y++)
            {
                for (int x = 0; x < art.Size.x; x++)
                {
                    int i = y * art.Size.x + x;
                    tex.SetPixel(x, y, art.Body[i]);
                    Color c = Color.clear;
                    for (int k = 0; k < 4; k++)
                    {
                        if (art.Cross[k][i].a > 0)
                        {
                            c = tint[k];
                        }
                    }

                    tex.SetPixel(art.Size.x + x, y, c.a > 0 ? c : (Color)art.Body[i] * 0.6f);
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"size={art.Size} crossCenter={art.CrossCenter}";
        }
    }
}
