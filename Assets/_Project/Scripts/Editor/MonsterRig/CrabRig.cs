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
    /// 갑옷 게(crab.png 237x234): 몸통(귀 포함) + 눈(별도 레이어) + 다리 6개(각자 레이어, 몸통 뒤).
    /// 다리는 외곽선을 따라 그린 다각형으로 자르고, 큰 다리에 가려진 뒷다리의 윗부분은 그려서 채운다.
    /// 좌표는 위 기준(td)으로 적고 V()로 변환한다.
    /// </summary>
    public static class CrabRig
    {
        private const string Png = "Assets/_Project/Art/Characters/Monsters/Source/crab.png";
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;
        private const int W = 237, H = 234;

        private static Vector2 V(float x, float tdY) => new(x, H - 1 - tdY);

        private sealed class Leg
        {
            public string Name;
            public Vector2 Pivot, Tip;   // 위 기준
            public Vector2[][] Shapes;   // 위 기준 다각형들 (합집합)
            public int Group;            // 걸음 조
            public bool Left;
        }

        private static readonly Vector2 EyeCenter = V(158, 95);
        private const float EyeRadius = 58f;
        private static readonly Vector2 BodyCenter = V(118, 110);

        // PSD 레이어 순서(아래 → 위)대로: 가려진 뒷다리 → 맨 왼쪽 다리 → 나머지
        private static readonly Leg[] Legs =
        {
            // 맨 왼쪽: 큰 다리 + 그 뒤에 겹친 가는 다리를 한 파츠로
            new() { Name = "LegA", Pivot = new(18, 132), Tip = new(24, 224), Group = 0, Left = true,
                Shapes = new[]
                {
                    new Vector2[] { new(1, 128), new(20, 117), new(33, 128), new(32, 160), new(31, 200), new(27, 227), new(19, 224), new(11, 190), new(4, 160), new(0, 140) },
                    new Vector2[] { new(24, 158), new(37, 158), new(39, 197), new(33, 198), new(26, 176) },
                } },
            new() { Name = "LegB", Pivot = new(85, 130), Tip = new(92, 232), Group = 1, Left = true,
                Shapes = new[] { new Vector2[] { new(68, 127), new(101, 124), new(101, 142), new(99, 233), new(89, 234), new(77, 190), new(69, 150) } } },
            new() { Name = "LegC", Pivot = new(152, 168), Tip = new(148, 194), Group = 0, Left = false,
                Shapes = new[] { new Vector2[] { new(143, 167), new(161, 166), new(159, 181), new(151, 196), new(145, 193), new(144, 179) } } },
            new() { Name = "LegD", Pivot = new(189, 150), Tip = new(180, 191), Group = 1, Left = false,
                Shapes = new[] { new Vector2[] { new(177, 151), new(199, 146), new(198, 171), new(187, 187), new(180, 193), new(177, 171) } } },
            new() { Name = "LegE", Pivot = new(205, 136), Tip = new(213, 203), Group = 0, Left = false,
                Shapes = new[] { new Vector2[] { new(197, 141), new(204, 128), new(221, 107), new(233, 104), new(237, 120), new(235, 150), new(226, 186), new(215, 207), new(209, 202), new(214, 170), new(222, 136), new(214, 135), new(204, 147) } } },
        };


        internal static MonsterRigs.Def Def()
        {
            var bones = new List<BoneSpec>
            {
                Bone("Body", null, BodyCenter.x, BodyCenter.y, BodyCenter.x, BodyCenter.y + 40),
                Bone("Eye", null, EyeCenter.x, EyeCenter.y, EyeCenter.x + 20, EyeCenter.y),
            };
            foreach (var leg in Legs)
            {
                var p = V(leg.Pivot.x, leg.Pivot.y);
                var t = V(leg.Tip.x, leg.Tip.y);
                bones.Add(Bone(leg.Name, null, p.x, p.y, t.x, t.y));
            }

            const float loop = 1f;

            Pose Move(float t)
            {
                float ph = t * Tau / loop, s = Mathf.Sin(ph);
                var body = new Vector2(0f, 0.008f * Mathf.Abs(s));
                var p = Upper(body, 1.5f * s);
                // 다리: 두 조가 엇갈려 앞뒤로 흔들리며 앞으로 갈 때 살짝 들림 (회전 중심은 몸통 아래 뿌리)
                foreach (var leg in Legs)
                {
                    float a = ph + leg.Group * Mathf.PI;
                    p.B(leg.Name, dy: body.y * 0.3f + 0.008f * Mathf.Max(0f, Mathf.Sin(a)), rot: 7f * Mathf.Sin(a));
                }

                return p;
            }

            return new MonsterRigs.Def
            {
                Name = "ArmoredCrab",
                Size = new Vector2Int(W, H),
                PivotPx = new Vector2(119, 0),
                CustomLayers = Layers,
                GridStep = 6,
                Bones = bones.ToArray(),
                MoveLength = loop,
                MoveKeys = 8,
                Move = Move,
                // 몸을 살짝 웅크림 → 눈에 불이 들어옴(빛이 모임) → 아래로 빔 발사(몸은 반동으로 살짝 들림) → 복귀
                Attack = new[]
                {
                    (0f, P()),
                    (0.22f, Upper(new Vector2(0f, -0.01f), 0f)),
                    (0.38f, Upper(new Vector2(0f, -0.012f), 0f)),
                    (0.44f, Upper(new Vector2(0f, 0.02f), 0f)),
                    (0.7f, Upper(new Vector2(0f, 0.015f), 0f)),
                    (0.95f, P()),
                },
                AttackExtra = clip => ClipFx.AddCurves(clip, BeamRoot, BeamTracks()),
                OnAttach = body => ClipFx.CreateObjects(body, BeamRoot, (EyeCenter - new Vector2(119, 0)) / Ppu, BeamTracks()),
                // 움찔 → 눈이 어둡게 꺼짐 → 다리가 바깥으로 벌어지며 몸통이 바닥으로 주저앉음 → 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.1f, Sink(0.05f)),
                    (0.45f, Sink(0.7f)),
                    (0.6f, Sink(1.05f)),
                    (0.7f, Sink(1f)),
                    (1.2f, Sink(1f)),
                },
                DieExtra = DieColors,
            };
        }

        // 몸통과 눈이 함께 이동·회전 (몸 중심 기준)
        private static Pose Upper(Vector2 body, float rot)
        {
            var p = P();
            var q = Quaternion.Euler(0f, 0f, rot);
            var eye = BodyCenter + (Vector2)(q * (EyeCenter - BodyCenter)) + body * Ppu;
            p.B("Body", dx: body.x, dy: body.y, rot: rot);
            p.B("Eye", dx: (eye.x - EyeCenter.x) / Ppu, dy: (eye.y - EyeCenter.y) / Ppu, rot: rot);
            return p;
        }

        // 주저앉기: k 0..1 (몸통이 바닥까지 내려앉고 다리는 바깥으로 벌어짐)
        private static Pose Sink(float k)
        {
            var p = Upper(new Vector2(0f, -0.13f * k), -4f * k);
            foreach (var leg in Legs)
            {
                float outward = leg.Left ? -1f : 1f;
                p.B(leg.Name, dx: outward * 0.03f * k, dy: -0.03f * k, rot: -outward * 28f * k);
            }

            return p;
        }

        // 눈: 처음에 붉게 움찔 → 어두운 색으로 꺼짐 → 마지막에 함께 사라짐. 몸통과 다리도 움찔 후 사라짐.
        private static void DieColors(AnimationClip clip)
        {
            void Set(string path, string prop, params (float t, float v)[] keys) =>
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SpriteRenderer), prop),
                    new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray()));

            foreach (var path in new[] { "crab_body" }.Concat(Legs.Select(l => "crab_" + l.Name.ToLowerInvariant())))
            {
                Set(path, "m_Color.r", (0f, 1f), (0.06f, 1f), (0.14f, 1f));
                Set(path, "m_Color.g", (0f, 1f), (0.06f, 0.35f), (0.14f, 1f));
                Set(path, "m_Color.b", (0f, 1f), (0.06f, 0.35f), (0.14f, 1f));
                Set(path, "m_Color.a", (0f, 1f), (0.85f, 1f), (1.2f, 0f));
            }

            Set("crab_eye", "m_Color.r", (0f, 1f), (0.06f, 1f), (0.14f, 1f), (0.45f, 0.22f));
            Set("crab_eye", "m_Color.g", (0f, 1f), (0.06f, 0.35f), (0.14f, 1f), (0.45f, 0.24f));
            Set("crab_eye", "m_Color.b", (0f, 1f), (0.06f, 0.35f), (0.14f, 1f), (0.45f, 0.35f));
            Set("crab_eye", "m_Color.a", (0f, 1f), (0.85f, 1f), (1.2f, 0f));
        }

        // ---------- 눈 불 + 아래로 빔 ----------

        private const string BeamRoot = "BeamFx";

        private static List<ClipFx.Track> BeamTracks()
        {
            string beam = ClipFx.Generated("fx_beam", (u, v) =>
            {
                float core = Mathf.Pow(Mathf.Clamp01(1f - v * v), 3f);
                float ends = Mathf.Clamp01((1f - Mathf.Abs(u)) * 6f);
                return Color.Lerp(new Color(0.4f, 0.85f, 1f, 1f), Color.white, core) * new Color(1f, 1f, 1f, core * ends);
            });

            var white = Color.white;
            var glow = new Color(0.75f, 0.95f, 1f, 1f);
            const float len = 2.4f;
            Vector2 BeamPos(float l) => new(0f, -(l * 0.5f + 0.1f)); // 눈 아래쪽에서 시작해 수직 아래로
            return new List<ClipFx.Track>
            {
                new()
                {
                    Name = "EyeGlow", Sprite = ClipFx.Soft, Order = 30,
                    Keys = new[]
                    {
                        (0f, Vector2.zero, Vector2.one * 0.3f, ClipFx.Alpha(glow, 0f)),
                        (0.2f, Vector2.zero, Vector2.one * 0.3f, ClipFx.Alpha(glow, 0f)),
                        (0.38f, Vector2.zero, Vector2.one * 0.75f, ClipFx.Alpha(glow, 0.9f)),
                        (0.46f, Vector2.zero, Vector2.one * 0.95f, white),
                        (0.7f, Vector2.zero, Vector2.one * 0.8f, ClipFx.Alpha(glow, 0.8f)),
                        (0.85f, Vector2.zero, Vector2.one * 0.5f, ClipFx.Alpha(glow, 0f)),
                    },
                },
                new()
                {
                    Name = "Beam", Sprite = beam, Order = 31, Rotation = 90f,
                    Keys = new[]
                    {
                        (0f, BeamPos(0.05f), new Vector2(0.05f, 0.1f), ClipFx.Alpha(white, 0f)),
                        (0.4f, BeamPos(0.05f), new Vector2(0.05f, 0.1f), ClipFx.Alpha(white, 0f)),
                        (0.46f, BeamPos(len), new Vector2(len, 0.2f), white),
                        (0.58f, BeamPos(len), new Vector2(len, 0.15f), white),
                        (0.68f, BeamPos(len), new Vector2(len, 0.18f), white),
                        (0.8f, BeamPos(len), new Vector2(len, 0.02f), ClipFx.Alpha(white, 0f)),
                    },
                },
            };
        }

        // ---------- 그림 준비 ----------

        private static List<LayerSpec> Layers()
        {
            var (body, eye, legs) = Prepare();
            var size = new Vector2Int(W, H);
            var layers = Legs.Select(l => new LayerSpec
            {
                Name = "crab_" + l.Name.ToLowerInvariant(),
                Pixels = legs[l.Name],
                PixelsSize = size,
                Bones = new[] { l.Name },
            }).ToList();
            layers.Add(new LayerSpec { Name = "crab_body", Pixels = body, PixelsSize = size, Bones = new[] { "Body" } });
            layers.Add(new LayerSpec { Name = "crab_eye", Pixels = eye, PixelsSize = size, Bones = new[] { "Eye" } });
            return layers;
        }

        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static (Color32[] body, Color32[] eye, Dictionary<string, Color32[]> legs) Prepare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var src = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            Vector2 Td(int i) => new(i % W, H - 1 - i / W);
            int Index(int x, int td) => (H - 1 - td) * W + x;

            var body = new Color32[src.Length];
            var eye = new Color32[src.Length];
            var legs = Legs.ToDictionary(l => l.Name, _ => new Color32[src.Length]);
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].a <= 8)
                {
                    continue;
                }

                if (Vector2.Distance(new Vector2(i % W, i / W), EyeCenter) <= EyeRadius + 3f) { eye[i] = src[i]; continue; }
                var leg = Legs.LastOrDefault(l => l.Shapes.Any(s => Inside(s, Td(i)))); // 앞(나중) 다리가 우선
                if (leg != null)
                {
                    legs[leg.Name][i] = src[i];
                }
                else
                {
                    body[i] = src[i];
                }
            }

            // 다리 테두리: 다리에 붙은 어두운 외곽선 픽셀을 4px까지 다리로 가져온다 (뿌리 근처 6px는 몸통 외곽선이라 제외)
            float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            foreach (var leg in Legs)
            {
                var px = legs[leg.Name];
                int top = int.MaxValue;
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a > 0)
                    {
                        top = Mathf.Min(top, H - 1 - i / W);
                    }
                }

                for (int pass = 0; pass < 4; pass++)
                {
                    var grow = new List<int>();
                    for (int i = 0; i < px.Length; i++)
                    {
                        if (body[i].a == 0 || H - 1 - i / W < top + 6)
                        {
                            continue;
                        }

                        if (Lum(body[i]) > 0.45f && body[i].a > 200)
                        {
                            continue; // 밝은 몸통 면은 제외, 외곽선/반투명만
                        }

                        int x = i % W, y = i / W;
                        bool touch = (x > 0 && px[i - 1].a > 0) || (x < W - 1 && px[i + 1].a > 0) ||
                                     (y > 0 && px[i - W].a > 0) || (y < H - 1 && px[i + W].a > 0);
                        if (touch)
                        {
                            grow.Add(i);
                        }
                    }

                    foreach (int i in grow) { px[i] = body[i]; body[i] = default; }
                }
            }

            // 몸통에 남은 작은 부스러기(다리 옆 외곽선 조각)는 6px 안의 가까운 다리로, 없으면 지운다
            {
                var seen = new bool[body.Length];
                var queue = new Queue<int>();
                for (int s0 = 0; s0 < body.Length; s0++)
                {
                    if (seen[s0] || body[s0].a == 0)
                    {
                        continue;
                    }

                    var comp = new List<int>();
                    seen[s0] = true;
                    queue.Enqueue(s0);
                    while (queue.Count > 0)
                    {
                        int i = queue.Dequeue();
                        comp.Add(i);
                        int x = i % W, y = i / W;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= W || ny >= H)
                                {
                                    continue;
                                }

                                int j = ny * W + nx;
                                if (!seen[j] && body[j].a > 0) { seen[j] = true; queue.Enqueue(j); }
                            }
                        }
                    }

                    if (comp.Count >= 60)
                    {
                        continue;
                    }

                    string owner = null;
                    float best = 6.5f;
                    foreach (var leg in Legs)
                    {
                        var px = legs[leg.Name];
                        foreach (int i in comp)
                        {
                            int x = i % W, y = i / W;
                            for (int dy = -6; dy <= 6; dy++)
                            {
                                for (int dx = -6; dx <= 6; dx++)
                                {
                                    int nx = x + dx, ny = y + dy;
                                    if (nx < 0 || ny < 0 || nx >= W || ny >= H || px[ny * W + nx].a == 0)
                                    {
                                        continue;
                                    }

                                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                                    if (d < best) { best = d; owner = leg.Name; }
                                }
                            }
                        }
                    }

                    foreach (int i in comp)
                    {
                        if (owner != null)
                        {
                            legs[owner][i] = body[i];
                        }

                        body[i] = default;
                    }
                }
            }

            // 다리 뿌리를 몸통 뒤로 8px 늘려 그린다 (흔들려도 이음새에 틈이 안 보이게, 몸통이 덮는다)
            foreach (var leg in Legs)
            {
                var px = legs[leg.Name];
                int top = int.MaxValue;
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a > 0)
                    {
                        top = Mathf.Min(top, H - 1 - i / W);
                    }
                }

                for (int x = 0; x < W; x++)
                {
                    int i0 = Index(x, top + 1);
                    if (top + 1 >= H || px[i0].a == 0)
                    {
                        continue;
                    }

                    for (int d = 1; d <= 8 && top + 1 - d >= 0; d++)
                    {
                        int j = Index(x, top + 1 - d);
                        if (body[j].a > 0 && px[j].a == 0)
                        {
                            px[j] = px[i0];
                        }
                    }
                }
            }

            return (body, eye, legs);
        }

        /// <summary>다리를 뺀 몸통+눈만 3배로 (다리 자리에 남은 외곽선 확인용)</summary>
        public static void WriteBodyOnly(string path)
        {
            var (body, eye, _) = Prepare();
            const int S = 3;
            var tex = new Texture2D(W * S, H * S, TextureFormat.RGBA32, false);
            for (int y = 0; y < H * S; y++)
            {
                for (int x = 0; x < W * S; x++)
                {
                    int i = (y / S) * W + x / S;
                    Color c = new Color(1f, 0.85f, 0.4f, 1f);
                    if (body[i].a > 0)
                    {
                        c = Color.Lerp(c, body[i], body[i].a / 255f);
                    }

                    if (eye[i].a > 0)
                    {
                        c = eye[i];
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>파츠 분리 디버그: 다리마다 색, 그려 넣은 부분 포함</summary>
        public static string WritePreview(string path)
        {
            var (body, eye, legs) = Prepare();
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < body.Length; i++)
            {
                Color c = new Color(0.75f, 0.85f, 1f, 1f);
                if (body[i].a > 0)
                {
                    c = body[i];
                }

                for (int k = 0; k < Legs.Length; k++)
                {
                    if (legs[Legs[k].Name][i].a > 0)
                    {
                        c = Color.Lerp(legs[Legs[k].Name][i], Color.HSVToRGB(k * 0.618f % 1f, 0.9f, 1f), 0.55f);
                    }
                }

                if (eye[i].a > 0)
                {
                    c = eye[i];
                }

                tex.SetPixel(i % W, i / W, c);
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return "legs=" + string.Join(",", Legs.Select(l => $"{l.Name}:{legs[l.Name].Count(c => c.a > 0)}"));
        }
    }
}
