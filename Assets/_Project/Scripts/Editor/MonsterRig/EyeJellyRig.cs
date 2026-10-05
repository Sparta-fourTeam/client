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
    /// 눈 해파리(eyejelly_v2.png 1000x1024, 검은 배경 → 투명, 1/4 축소):
    /// 몸통 원(본 5개, 녹아내리기용) + 눈 십자 4조각 + 촉수(덩어리마다 레이어 하나, 본 2개).
    /// 모든 좌표는 축소된 이미지 픽셀, 좌하단 원점.
    /// </summary>
    public static class EyeJellyRig
    {
        private const string Png = "Assets/_Project/Art/Characters/Monsters/eyejelly_v2.png";
        private const int Scale = 4;
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;
        private static readonly string[] Arms = { "CrossU", "CrossD", "CrossL", "CrossR" };

        private sealed class Tentacle
        {
            public Color32[] Pixels;
            public Vector2 Root, Mid, Tip;
        }

        private sealed class Art
        {
            public Vector2Int Size;
            public Color32[] Body;
            public Color32[][] Cross;
            public Vector2 Center, CrossCenter;
            public float Radius;
            public List<Tentacle> Tentacles;
        }

        private static Art s_art;

        internal static MonsterRigs.Def Def()
        {
            var art = s_art ??= Prepare();
            var c = art.Center;
            float r = art.Radius;
            var cc = art.CrossCenter;
            const float loop = 3f;

            var bones = new List<BoneSpec>
            {
                Bone("Root", null, c.x, c.y, c.x, c.y + r * 0.3f),
                Bone("Top", "Root", c.x, c.y + r * 0.3f, c.x, c.y + r),
                Bone("Bottom", "Root", c.x, c.y - r * 0.2f, c.x, c.y - r),
                Bone("SideL", "Root", c.x - r * 0.4f, c.y, c.x - r, c.y),
                Bone("SideR", "Root", c.x + r * 0.4f, c.y, c.x + r, c.y),
            };
            foreach (var arm in Arms)
            {
                var d = arm switch { "CrossU" => Vector2.up, "CrossD" => Vector2.down, "CrossL" => Vector2.left, _ => Vector2.right };
                bones.Add(Bone(arm, "Root", cc.x, cc.y, cc.x + d.x * 12, cc.y + d.y * 12));
            }

            for (int i = 0; i < art.Tentacles.Count; i++)
            {
                var t = art.Tentacles[i];
                bones.Add(Bone($"T{i}A", null, t.Root.x, t.Root.y, t.Mid.x, t.Mid.y));
                bones.Add(Bone($"T{i}B", $"T{i}A", t.Mid.x, t.Mid.y, t.Tip.x, t.Tip.y));
            }

            // 촉수마다 다른 박자(루프당 1~2회)·위상·폭·방향
            float Hash(int n) { float v = Mathf.Sin(n * 12.9898f) * 43758.5453f; return v - Mathf.Floor(v); }
            var waves = art.Tentacles.Select((_, i) => (cycles: 1f + Mathf.Floor(Hash(i + 7) * 2f), phase: Hash(i + 3) * Tau,
                amp: 8f + 7f * Hash(i + 11), sign: Hash(i + 19) > 0.5f ? 1f : -1f)).ToArray();

            Pose Move(float t)
            {
                float ph = t * Tau / loop;
                float bob = 0.02f * Mathf.Sin(ph);
                var p = P().B("Root", dy: bob, rot: 2f * Mathf.Sin(ph + 1f));
                for (int i = 0; i < waves.Length; i++)
                {
                    var w = waves[i];
                    float a = w.cycles * ph + w.phase;
                    p.B($"T{i}A", dy: bob, rot: w.sign * w.amp * Mathf.Sin(a));
                    p.B($"T{i}B", rot: w.sign * w.amp * 1.3f * Mathf.Sin(a - 0.9f));
                }

                return p;
            }


            // 녹음: melt 0..1 (위가 내려앉고 옆으로 퍼지며 바닥으로), 촉수 fall 0..1 (떨어져 바닥으로)
            Pose Melt(float melt, float fall, float alpha = 1f)
            {
                var p = P().Tint(0f, alpha)
                    .B("Root", dy: -0.22f * melt)
                    .B("Top", dx: 0f, dy: -0.18f * melt)
                    .B("Bottom", dy: 0.05f * melt)
                    .B("SideL", dx: -0.14f * melt, dy: -0.08f * melt)
                    .B("SideR", dx: 0.14f * melt, dy: -0.08f * melt);
                for (int i = 0; i < art.Tentacles.Count; i++)
                {
                    var t = art.Tentacles[i];
                    float floor = (t.Root.y - 8f) / Ppu;
                    float side = Mathf.Sign(t.Root.x - c.x);
                    float pop = Mathf.Clamp01(fall * 4f) * 0.04f; // 떨어지기 전 바깥으로 툭
                    p.B($"T{i}A", dx: side * (pop + 0.06f * fall * Hash(i + 5)), dy: -floor * Mathf.Pow(fall, 1.6f),
                        rot: side * (40f + 60f * Hash(i + 9)) * fall);
                    p.B($"T{i}B", rot: side * 30f * fall);
                }

                return p;
            }

            return new MonsterRigs.Def
            {
                Name = "EyeJelly",
                Size = art.Size,
                PivotPx = c,
                CustomLayers = () => Layers(art),
                GridStep = 8,
                Bones = bones.ToArray(),
                MoveLength = loop,
                MoveKeys = 12,
                Move = Move,
                // 얼굴에 크기가 서로 다른 십자 10개가 툭툭 생김 → 각 십자에서 번쩍 → 십자들이 사라짐 (번쩍일 때 몸이 움찔)
                Attack = new[]
                {
                    (0f, P()),
                    (0.45f, P().B("Root", dy: 0.01f)),
                    (0.52f, P().B("Root", dy: -0.012f, rot: -2f)),
                    (0.65f, P().B("Root", dy: 0.005f, rot: 1f)),
                    (0.9f, P()),
                },
                AttackExtra = clip => ClipFx.AddCurves(clip, CrossRoot, CrossTracks(art)),
                OnAttach = body => ClipFx.CreateObjects(body, CrossRoot, Vector2.zero, CrossTracks(art)),
                // 움찔 → 촉수가 툭툭 떨어져 나감 → 몸체가 주저앉으며 녹아 퍼짐 → 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.08f, Melt(0f, 0f).Tint(1f)),
                    (0.25f, Melt(0.1f, 0.25f)),
                    (0.55f, Melt(0.45f, 1f)),
                    (0.95f, Melt(1f, 1f)),
                    (1.3f, Melt(1.05f, 1f, 0f)),
                },
            };
        }

        // ---------- 얼굴 가득 십자 + 번쩍임 ----------

        private const string CrossRoot = "CrossFx";
        private const string CrossSpritePath = "Assets/_Project/Art/VFX/Enemy/eyejelly_cross.png";

        private static List<ClipFx.Track> CrossTracks(Art art)
        {
            WriteCrossSprite(art);
            string sparkle = ClipFx.Generated("fx_sparkle", (u, v) =>
            {
                float r2 = u * u + v * v;
                float core = Mathf.Exp(-r2 * 18f);
                float rays = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 2f) * Mathf.Exp(-v * v * 220f)
                             + Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 2f) * Mathf.Exp(-u * u * 220f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(core + rays));
            });

            float Hash(int n) { float x = Mathf.Sin(n * 12.9898f) * 43758.5453f; return x - Mathf.Floor(x); }
            const float baseScale = 64f / Ppu;              // 스프라이트(PPU 64)를 원래 십자 크기(PPU 200)로
            float face = art.Radius / 1.24f * 0.8f / Ppu;  // 십자가 놓일 얼굴 반경(유닛)
            var ink = Color.white;                          // 스프라이트 색 그대로
            var flash = Color.white;

            // 얼굴 원 안에서 서로 너무 겹치지 않게 자리 고르기 (고정 해시라 빌드마다 같다)
            var spots = new List<Vector2>();
            for (int n = 0; spots.Count < 10 && n < 400; n++)
            {
                var pt = new Vector2(Hash(n * 2 + 1) * 2f - 1f, Hash(n * 2 + 2) * 2f - 1f) * face;
                if (pt.magnitude > face)
                {
                    continue;
                }

                if (spots.Any(q => Vector2.Distance(q, pt) < face * 0.5f) || pt.magnitude < face * 0.25f)
                {
                    continue;
                }

                spots.Add(pt);
            }

            var tracks = new List<ClipFx.Track>();
            for (int k = 0; k < spots.Count; k++)
            {
                var pos = spots[k];
                float size = (0.3f + 0.25f * Hash(k + 50)) * baseScale;
                float tin = 0.06f + 0.035f * k;
                float tf = 0.48f + 0.02f * k;
                var sz = Vector2.one * size;
                tracks.Add(new ClipFx.Track
                {
                    Name = "Cross" + k,
                    Sprite = CrossSpritePath,
                    Order = 30,
                    Rotation = (Hash(k + 80) - 0.5f) * 40f,
                    Keys = new[]
                    {
                        (0f, pos, Vector2.zero, ClipFx.Alpha(ink, 0f)),
                        (tin, pos, Vector2.zero, ink),
                        (tin + 0.06f, pos, sz * 1.25f, ink),
                        (tin + 0.1f, pos, sz, ink),
                        (tf + 0.12f, pos, sz, ink),
                        (tf + 0.24f, pos, sz * 0.6f, ClipFx.Alpha(ink, 0f)),
                    },
                });
                tracks.Add(new ClipFx.Track
                {
                    Name = "Flash" + k,
                    Sprite = sparkle,
                    Order = 31,
                    Rotation = Hash(k + 90) * 45f,
                    Keys = new[]
                    {
                        (0f, pos, Vector2.zero, ClipFx.Alpha(flash, 0f)),
                        (tf, pos, Vector2.zero, ClipFx.Alpha(flash, 0f)),
                        (tf + 0.04f, pos, Vector2.one * (size * 6f), flash),
                        (tf + 0.07f, pos, Vector2.one * (size * 7f), flash),
                        (tf + 0.22f, pos, Vector2.one * (size * 9f), ClipFx.Alpha(flash, 0f)),
                    },
                });
            }

            // 얼굴 전체를 덮는 큰 섬광 (번쩍이는 순간을 확실하게)
            var burst = new Color(1f, 1f, 0.95f, 1f);
            tracks.Add(new ClipFx.Track
            {
                Name = "Burst",
                Sprite = ClipFx.Soft,
                Order = 32,
                Keys = new[]
                {
                    (0f, Vector2.zero, Vector2.zero, ClipFx.Alpha(burst, 0f)),
                    (0.5f, Vector2.zero, Vector2.one * 0.3f, ClipFx.Alpha(burst, 0f)),
                    (0.55f, Vector2.zero, Vector2.one * 1.1f, burst),
                    (0.62f, Vector2.zero, Vector2.one * 1.3f, ClipFx.Alpha(burst, 0.85f)),
                    (0.8f, Vector2.zero, Vector2.one * 1.6f, ClipFx.Alpha(burst, 0f)),
                },
            });

            return tracks;
        }

        // 원래 십자 획(4조각 합친 것)을 잘라 스프라이트로 저장
        private static void WriteCrossSprite(Art art)
        {
            int w = art.Size.x, h = art.Size.y;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int i = 0; i < w * h; i++)
            {
                if (art.Cross.Any(a => a[i].a > 0))
                {
                    int x = i % w, y = i / w;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            }

            int size = Mathf.Max(maxX - minX, maxY - minY) + 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(new Color32[size * size]);
            var cc = art.CrossCenter;
            for (int i = 0; i < w * h; i++)
            {
                var px = art.Cross.Select(a => a[i]).FirstOrDefault(col => col.a > 0);
                if (px.a == 0)
                {
                    continue;
                }

                int x = i % w - Mathf.RoundToInt(cc.x) + size / 2, y = i / w - Mathf.RoundToInt(cc.y) + size / 2;
                if (x >= 0 && y >= 0 && x < size && y < size)
                {
                    tex.SetPixel(x, y, px);
                }
            }

            MonsterAnimationBaker.EnsureFolder("Assets/_Project/Art/VFX/Enemy");
            File.WriteAllBytes(Path.GetFullPath(CrossSpritePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CrossSpritePath, ImportAssetOptions.ForceUpdate);
        }

        // ---------- (이전) 눈 구멍 + 빔 ----------

        private const string BeamRoot = "BeamFx";

        private static List<ClipFx.Track> BeamTracks()
        {
            string hole = ClipFx.Generated("fx_hole", (u, v) =>
                new Color(0.08f, 0.08f, 0.13f, Mathf.Clamp01((1f - Mathf.Sqrt(u * u + v * v)) / 0.12f)));
            string beam = ClipFx.Generated("fx_beam", (u, v) =>
            {
                float core = Mathf.Pow(Mathf.Clamp01(1f - v * v), 3f);
                float ends = Mathf.Clamp01((1f - Mathf.Abs(u)) * 6f);
                return Color.Lerp(new Color(0.4f, 0.85f, 1f, 1f), Color.white, core) * new Color(1f, 1f, 1f, core * ends);
            });

            var white = Color.white;
            var tiny = Vector2.one * 0.01f;
            var openHole = Vector2.one * 0.2f;
            var glow = new Color(0.5f, 0.9f, 1f, 0.8f);
            const float beamLength = 2.6f;
            Vector2 BeamPos(float len) => new(0f, -len * 0.5f); // 스프라이트 중심 기준이라 길이의 절반만큼 아래
            return new List<ClipFx.Track>
            {
                new()
                {
                    Name = "Hole", Sprite = hole, Order = 30,
                    Keys = new[]
                    {
                        (0f, Vector2.zero, tiny, ClipFx.Alpha(white, 0f)),
                        (0.12f, Vector2.zero, tiny, white),
                        (0.25f, Vector2.zero, openHole, white),
                        (0.75f, Vector2.zero, openHole, white),
                        (0.88f, Vector2.zero, tiny, ClipFx.Alpha(white, 0f)),
                    },
                },
                new()
                {
                    // 빔: 아래로 뻗어 나가고(길이) → 굵기가 살짝 출렁이다 → 가늘어지며 사라짐
                    Name = "Beam", Sprite = beam, Order = 31, Rotation = 90f,
                    Keys = new[]
                    {
                        (0f, BeamPos(0.05f), new Vector2(0.05f, 0.1f), ClipFx.Alpha(white, 0f)),
                        (0.26f, BeamPos(0.05f), new Vector2(0.05f, 0.1f), ClipFx.Alpha(white, 0f)),
                        (0.34f, BeamPos(beamLength), new Vector2(beamLength, 0.16f), white),
                        (0.48f, BeamPos(beamLength), new Vector2(beamLength, 0.12f), white),
                        (0.6f, BeamPos(beamLength), new Vector2(beamLength, 0.15f), white),
                        (0.72f, BeamPos(beamLength), new Vector2(beamLength, 0.02f), ClipFx.Alpha(white, 0f)),
                    },
                },
                new()
                {
                    Name = "Glow", Sprite = ClipFx.Soft, Order = 32,
                    Keys = new[]
                    {
                        (0f, Vector2.zero, tiny, ClipFx.Alpha(glow, 0f)),
                        (0.26f, Vector2.zero, tiny, ClipFx.Alpha(glow, 0f)),
                        (0.34f, Vector2.zero, Vector2.one * 0.45f, glow),
                        (0.6f, Vector2.zero, Vector2.one * 0.4f, glow),
                        (0.72f, Vector2.zero, Vector2.one * 0.2f, ClipFx.Alpha(glow, 0f)),
                    },
                },
            };
        }

        private static void CrossFade(AnimationClip clip)
        {
            foreach (var arm in Arms)
            {
                string path = "eyejelly_" + arm.ToLowerInvariant();
                foreach (var ch in new[] { "r", "g", "b" })
                {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SpriteRenderer), "m_Color." + ch),
                        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.88f, 1f)));
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SpriteRenderer), "m_Color.a"),
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.12f, 1f), new Keyframe(0.25f, 0f),
                        new Keyframe(0.75f, 0f), new Keyframe(0.88f, 1f)));
            }
        }

        // ---------- 그림 준비 ----------

        private static List<LayerSpec> Layers(Art art)
        {
            var layers = new List<LayerSpec>();
            for (int i = 0; i < art.Tentacles.Count; i++)
            {
                layers.Add(new LayerSpec { Name = $"eyejelly_t{i}", Pixels = art.Tentacles[i].Pixels, PixelsSize = art.Size, Bones = new[] { $"T{i}A", $"T{i}B" } });
            }

            layers.Add(new LayerSpec { Name = "eyejelly_body", Pixels = art.Body, PixelsSize = art.Size, Bones = new[] { "Root", "Top", "Bottom", "SideL", "SideR" } });
            for (int i = 0; i < Arms.Length; i++)
            {
                layers.Add(new LayerSpec { Name = "eyejelly_" + Arms[i].ToLowerInvariant(), Pixels = art.Cross[i], PixelsSize = art.Size, Bones = new[] { Arms[i] } });
            }

            return layers;
        }

        private static Art Prepare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var src = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);
            float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

            // 1) 가장자리에서 이어진 검정(밝기 < 0.06) = 배경 → 투명. 경계의 어두운 픽셀은 밝기로 반투명 처리
            var bg = new bool[src.Length];
            var q = new Queue<int>();
            for (int x = 0; x < w; x++) { q.Enqueue(x); q.Enqueue((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { q.Enqueue(y * w); q.Enqueue(y * w + w - 1); }
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                if (bg[i] || Lum(src[i]) >= 0.06f)
                {
                    continue;
                }

                bg[i] = true;
                foreach (int j in N4(i, w, h))
                {
                    if (!bg[j])
                    {
                        q.Enqueue(j);
                    }
                }
            }

            // 촉수와 몸이 둘러싼 안쪽의 검정(가장자리와 안 이어진 배경)도 배경. 몸통 원판 안쪽은 제외.
            {
                double bx = 0, by = 0;
                int bn = 0;
                for (int i = 0; i < src.Length; i++)
                {
                    if (Lum(src[i]) > 0.8f) { bx += i % w; by += i / w; bn++; }
                }

                var bc = new Vector2((float)(bx / bn), (float)(by / bn));
                float br = Mathf.Sqrt(bn / Mathf.PI) * 1.3f;
                for (int i = 0; i < src.Length; i++)
                {
                    if (!bg[i] && Lum(src[i]) < 0.06f && Vector2.Distance(new Vector2(i % w, i / w), bc) > br)
                    {
                        bg[i] = true;
                    }
                }
            }

            var px = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (bg[i])
                {
                    continue;
                }

                bool edge = N4(i, w, h).Any(j => bg[j]);
                float a = edge ? Mathf.Clamp01(Lum(src[i]) / 0.2f) : 1f;
                px[i] = new Color32(src[i].r, src[i].g, src[i].b, (byte)(a * 255));
            }

            // 2) 작은 덩어리(워터마크, 점) 제거
            foreach (var comp in Components(px.Length, w, h, i => px[i].a > 0))
            {
                if (comp.Count < 1500)
                {
                    foreach (int i in comp)
                    {
                        px[i] = default;
                    }
                }
            }

            // 3) 몸통 원: 흰 원판 중심/반지름, 테두리까지 포함(+ 원에 붙은 흰 물방울)
            double sx = 0, sy = 0;
            int n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a > 0 && Lum(px[i]) > 0.8f) { sx += i % w; sy += i / w; n++; }
            }

            var center = new Vector2((float)(sx / n), (float)(sy / n));
            float white = Mathf.Sqrt(n / Mathf.PI);
            float rim = white * 1.24f;
            bool InBody(int i) => Vector2.Distance(new Vector2(i % w, i / w), center) <= rim;

            // 4) 눈 십자: 원 안쪽(흰 반지름의 85%)의 어두운 덩어리
            var cross = new bool[px.Length];
            foreach (var comp in Components(px.Length, w, h, i => px[i].a > 128 && Lum(px[i]) < 0.5f))
            {
                if (comp.Count >= 30 && comp.All(i => Vector2.Distance(new Vector2(i % w, i / w), center) < white * 0.85f))
                {
                    foreach (int i in comp)
                    {
                        cross[i] = true;
                    }
                }
            }

            for (int pass = 0; pass < 4; pass++)
            {
                var next = (bool[])cross.Clone();
                for (int i = 0; i < px.Length; i++)
                {
                    if (cross[i])
                    {
                        foreach (int j in N4(i, w, h))
                        {
                            if (px[j].a > 128 && Lum(px[j]) < 0.95f)
                            {
                                next[j] = true;
                            }
                        }
                    }
                }

                cross = next;
            }

            var cols = new int[w];
            var rows = new int[h];
            for (int i = 0; i < px.Length; i++)
            {
                if (cross[i]) { cols[i % w]++; rows[i / w]++; }
            }

            var crossCenter = new Vector2(System.Array.IndexOf(cols, cols.Max()), System.Array.IndexOf(rows, rows.Max()));

            float pr = 0, pg = 0, pb = 0;
            int pn = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (cross[i] || px[i].a < 250 || Lum(px[i]) < 0.9f || Vector2.Distance(new Vector2(i % w, i / w), crossCenter) > white * 0.5f)
                {
                    continue;
                }

                pr += px[i].r; pg += px[i].g; pb += px[i].b; pn++;
            }

            var paper = new Color32((byte)(pr / pn), (byte)(pg / pn), (byte)(pb / pn), 255);
            var body = new Color32[px.Length];
            var arms = Enumerable.Range(0, 4).Select(_ => new Color32[px.Length]).ToArray();
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0)
                {
                    continue;
                }

                if (cross[i])
                {
                    var d = new Vector2(i % w, i / w) - crossCenter;
                    int arm = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x < 0 ? 2 : 3) : (d.y > 0 ? 0 : 1);
                    arms[arm][i] = px[i];
                    body[i] = paper;
                }
                else if (InBody(i))
                {
                    body[i] = px[i];
                }
            }

            // 5) 원 밖 = 촉수. 몸 둘레 띠(rim ~ rim2)를 빼고 덩어리를 나눠야 뿌리끼리 붙지 않는다.
            //    띠의 픽셀은 나중에 가까운 촉수로 번져 붙인다. 원 밖의 밝은 흰 물방울은 몸통.
            float rim2 = white * 1.42f;
            float Dist(int i) => Vector2.Distance(new Vector2(i % w, i / w), center);
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a > 0 && !InBody(i) && Lum(px[i]) > 0.7f && Dist(i) < rim2 + 40f)
                {
                    body[i] = px[i];
                }
            }

            bool Tent(int i) => px[i].a > 0 && !InBody(i) && body[i].a == 0;

            var label = Enumerable.Repeat(-1, px.Length).ToArray();
            var seeds = Components(px.Length, w, h, i => Tent(i) && Dist(i) > rim2).Where(c => c.Count >= 300).ToList();
            for (int k = 0; k < seeds.Count; k++)
            {
                foreach (int i in seeds[k])
                {
                    label[i] = k;
                }
            }

            var grow = new Queue<int>(seeds.SelectMany(c => c));
            while (grow.Count > 0)
            {
                int i = grow.Dequeue();
                foreach (int j in N4(i, w, h))
                {
                    if (label[j] < 0 && Tent(j)) { label[j] = label[i]; grow.Enqueue(j); }
                }
            }

            var tentacles = new List<Tentacle>();
            for (int k = 0; k < seeds.Count; k++)
            {
                var comp = Enumerable.Range(0, px.Length).Where(i => label[i] == k).ToList();
                var t = new Tentacle { Pixels = new Color32[px.Length] };
                foreach (int i in comp)
                {
                    t.Pixels[i] = px[i];
                }

                Vector2 P(int i) => new(i % w, i / w);
                var root = comp.OrderBy(i => Vector2.Distance(P(i), center)).First();
                var tip = comp.OrderByDescending(i => Vector2.Distance(P(i), P(root))).First();
                float half = Vector2.Distance(P(root), P(tip)) * 0.5f;
                var mid = comp.OrderBy(i => Mathf.Abs(Vector2.Distance(P(i), P(root)) - half) + Vector2.Distance(P(i), (P(root) + P(tip)) * 0.5f) * 0.3f).First();
                t.Root = P(root) / Scale; t.Mid = P(mid) / Scale; t.Tip = P(tip) / Scale;
                tentacles.Add(t);
            }

            // 남은(어느 촉수에도 안 붙은) 원 밖 조각은 몸통으로
            for (int i = 0; i < px.Length; i++)
            {
                if (Tent(i) && label[i] < 0)
                {
                    body[i] = px[i];
                }
            }

            tentacles = tentacles.Select(t => { t.Pixels = DownscaleStroke(t.Pixels, w, h); return t; }).ToList();
            return new Art
            {
                Size = new Vector2Int(w / Scale, h / Scale),
                Body = DownscaleStroke(body, w, h),
                Cross = arms.Select(a => DownscaleStroke(a, w, h)).ToArray(),
                Center = center / Scale,
                CrossCenter = crossCenter / Scale,
                Radius = rim / Scale,
                Tentacles = tentacles,
            };
        }

        // 4x4에서 가장 불투명한 픽셀을 남긴다 (가는 선이 흐려지지 않게)
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

        private static IEnumerable<int> N4(int i, int w, int h)
        {
            int x = i % w, y = i / w;
            if (x > 0)
            {
                yield return i - 1;
            }

            if (x < w - 1)
            {
                yield return i + 1;
            }

            if (y > 0)
            {
                yield return i - w;
            }

            if (y < h - 1)
            {
                yield return i + w;
            }
        }

        private static List<List<int>> Components(int n, int w, int h, System.Func<int, bool> test)
        {
            var seen = new bool[n];
            var result = new List<List<int>>();
            var queue = new Queue<int>();
            for (int s = 0; s < n; s++)
            {
                if (seen[s] || !test(s))
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
                    foreach (int j in N4(i, w, h))
                    {
                        if (!seen[j] && test(j)) { seen[j] = true; queue.Enqueue(j); }
                    }
                }

                result.Add(comp);
            }

            return result;
        }

        /// <summary>준비 결과 확인: 촉수마다 색, 십자 검정, 몸통 원본</summary>
        public static string WritePreview(string path)
        {
            s_art = null;
            var art = s_art = Prepare();
            var tex = new Texture2D(art.Size.x, art.Size.y, TextureFormat.RGBA32, false);
            var output = new Color[art.Size.x * art.Size.y];
            for (int i = 0; i < output.Length; i++)
            {
                Color c = new Color(0.75f, 0.85f, 1f, 1f);
                if (art.Body[i].a > 0)
                {
                    c = Color.Lerp(c, art.Body[i], art.Body[i].a / 255f);
                }

                for (int k = 0; k < art.Tentacles.Count; k++)
                {
                    if (art.Tentacles[k].Pixels[i].a > 0)
                    {
                        c = Color.HSVToRGB(k * 0.618f % 1f, 0.9f, 0.9f);
                    }
                }

                foreach (var a in art.Cross)
                {
                    if (a[i].a > 0)
                    {
                        c = Color.black;
                    }
                }

                output[i] = c;
            }

            tex.SetPixels(output);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"size={art.Size} center={art.Center} r={art.Radius:F0} cross={art.CrossCenter} tentacles={art.Tentacles.Count}";
        }
    }
}
