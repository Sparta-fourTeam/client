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
    /// 뇌 거미(brain.png 329x300): 흰 껍질(좌/우 반쪽) + 팔 6 + 다리 4 (그림 한 장, 파츠별 메시) + 뇌(별도 레이어, 녹는 본 4개).
    /// 모든 파츠 본은 루트이고, 따라가는 동작은 FK로 직접 계산한다. 좌표는 PNG 픽셀, 위 기준(td)으로 적고 V()로 변환한다.
    /// </summary>
    public static class BrainSpiderRig
    {
        private const string Png = "Assets/_Project/Sprites/brain.png";
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;
        private const int H = 300;

        private static Vector2 V(float x, float tdY) => new(x, H - 1 - tdY);

        private sealed class Piece
        {
            public string Name;
            public Vector2[] Line; // 배정용 선 (첫 점 = 회전 중심)
            public bool Leg, Left;
            public int Rank;
            // 회전 중심: 팔다리는 실제로 몸통 원을 벗어나는 지점(그림에서 계산), 껍질은 경첩
            public Vector2 Pivot => s_pivots != null && s_pivots.TryGetValue(Name, out var p) ? p : Line[0];
            public Vector2 Tip => Line[^1];
        }

        private static Piece Limb(string name, bool leg, params Vector2[] line) =>
            new() { Name = name, Line = line, Leg = leg, Left = line[0].x < BodyCenter.x, Rank = 0 };

        private static readonly Vector2 BodyCenter = V(177, 128);
        private static readonly Vector2 BodyRadius = new(74, 80); // 둥근 몸통보다 조금 크게: 안쪽은 전부 껍질
        private static Dictionary<string, Vector2> s_pivots;
        private static readonly Vector2 BrainCenter = V(178, 121);
        private static readonly Vector2 BrainRadius = new(56, 50);
        private static readonly Vector2 HingeL = V(125, 182), HingeR = V(225, 182);

        private static readonly Piece[] Pieces =
        {
            Limb("ArmTop", false, V(140, 60), V(128, 29), V(125, 7)),
            Limb("ArmUL", false, V(110, 85), V(95, 40), V(40, 49), V(10, 75)),
            Limb("ArmML", false, V(110, 115), V(75, 110), V(40, 139), V(25, 165)),
            Limb("ArmUR", false, V(230, 70), V(250, 10), V(300, 30), V(312, 55)),
            Limb("ArmMR", false, V(240, 110), V(290, 100), V(315, 120)),
            Limb("ArmMid", false, V(205, 195), V(210, 245)),
            Limb("LegL1", true, V(115, 170), V(100, 205), V(118, 290)),
            Limb("LegL2", true, V(148, 192), V(140, 270)),
            Limb("LegR1", true, V(244, 182), V(256, 215), V(254, 255), V(252, 268), V(266, 290)),
            Limb("LegR2", true, V(268, 165), V(286, 190), V(286, 240), V(282, 270), V(276, 292)),
            new() { Name = "ShellL", Line = new[] { HingeL, V(140, 90) }, Left = true, Rank = 1 },
            new() { Name = "ShellR", Line = new[] { HingeR, V(210, 90) }, Left = false, Rank = 1 },
        };

        private static Piece Get(string n) => Pieces.First(p => p.Name == n);
        private static IEnumerable<Piece> Limbs => Pieces.Where(p => !p.Name.StartsWith("Shell"));

        private static float Hash(int n) { float v = Mathf.Sin(n * 12.9898f) * 43758.5453f; return v - Mathf.Floor(v); }

        internal static MonsterRigs.Def Def()
        {
            if (s_pivots == null)
            {
                Prepare();
            }

            var bones = Pieces.Select(p => Bone(p.Name, null, p.Pivot.x, p.Pivot.y, p.Tip.x, p.Tip.y)).ToList();
            var b = BrainCenter;
            bones.Add(Bone("Brain", null, b.x, b.y, b.x, b.y + 15));
            bones.Add(Bone("BrainTop", "Brain", b.x, b.y + 15, b.x, b.y + BrainRadius.y));
            bones.Add(Bone("BrainBottom", "Brain", b.x, b.y - 15, b.x, b.y - BrainRadius.y));
            bones.Add(Bone("BrainL", "Brain", b.x - 20, b.y, b.x - BrainRadius.x, b.y));
            bones.Add(Bone("BrainR", "Brain", b.x + 20, b.y, b.x + BrainRadius.x, b.y));

            const float loop = 1.6f;
            var legs = Pieces.Where(p => p.Leg).ToArray();
            var arms = Limbs.Where(p => !p.Leg).ToArray();

            Pose Move(float t)
            {
                float ph = t * Tau / loop;
                var lift = new Vector2(0f, 0.012f * Mathf.Sin(2f * ph));
                var p = Rigid(lift);
                // 다리 4개: 같은 속도(루프당 2걸음)로 박자만 서로 다르게 — 들어 올릴 때만 살짝 뜸
                for (int i = 0; i < legs.Length; i++)
                {
                    float a = 2f * ph + i * 1.7f;
                    p.B(legs[i].Name, dx: lift.x, dy: lift.y * 0.3f + 0.006f * Mathf.Max(0f, Mathf.Sin(a)), rot: 4f * Mathf.Sin(a));
                }

                // 팔 6개: 각자 다른 방향·박자로 흔들림
                for (int i = 0; i < arms.Length; i++)
                {
                    float cycles = 1f + Mathf.Floor(Hash(i + 2) * 2f);
                    float sign = Hash(i + 13) > 0.5f ? 1f : -1f;
                    p.B(arms[i].Name, dx: lift.x, dy: lift.y, rot: sign * (3f + 3f * Hash(i + 7)) * Mathf.Sin(cycles * ph + Hash(i) * Tau));
                }

                return p;
            }

            return new MonsterRigs.Def
            {
                Name = "BrainSpider",
                Size = new Vector2Int(329, 300),
                PivotPx = new Vector2(165, 150),
                CustomLayers = Layers,
                Bones = bones.ToArray(),
                MoveLength = loop,
                MoveKeys = 12,
                Move = Move,
                // 팔들이 각자 다른 방향으로 뻗어 → 낚아채듯 몸 쪽으로 끌어옴 → 뇌가 꿈틀하며 소용돌이 뇌파 → 복귀
                Attack = new[]
                {
                    (0f, P()),
                    (0.3f, Grab(reach: 0f, curl: 6f, brain: 0.3f)),
                    (0.5f, Grab(reach: 0f, curl: 7f, brain: 1f)),
                    (0.65f, Grab(reach: 0f, curl: 7f, brain: -0.6f)),
                    (0.8f, Grab(reach: 0f, curl: 6f, brain: 0.8f)),
                    (0.95f, Grab(reach: 0f, curl: 4f, brain: -0.3f)),
                    (1.1f, P()),
                },
                AttackExtra = clip => ClipFx.AddCurves(clip, WaveRoot, WaveTracks()),
                OnAttach = body => ClipFx.CreateObjects(body, WaveRoot, (BrainCenter - new Vector2(165, 150)) / Ppu, WaveTracks()),
                // 움찔 → 껍질에 금 → 왼/오른쪽 반쪽이 팔다리째 바깥으로 벗겨지며 떨어져 사라짐 → 남은 뇌가 떨어져 녹아내리며 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.15f, Peel(0.03f).Merge(BrainMelt(0f, 0f))),
                    (0.5f, Peel(0.45f).Merge(BrainMelt(0f, 0f))),
                    (0.85f, Peel(1f).Merge(BrainMelt(0f, 0.15f))),
                    (1.1f, Peel(1f).Merge(BrainMelt(0.35f, 1f))),
                    (1.45f, Peel(1f).Merge(BrainMelt(1f, 1f))),
                    (1.6f, Peel(1f).Merge(BrainMelt(1.05f, 1f))),
                },
                DieExtra = DieColors,
            };
        }

        // ---------- 자세 ----------

        // 전체가 같이 이동
        private static Pose Rigid(Vector2 move)
        {
            var p = P();
            foreach (var piece in Pieces)
            {
                p.B(piece.Name, dx: move.x, dy: move.y);
            }

            p.B("Brain", dx: move.x, dy: move.y);
            return p;
        }

        // 잡기: reach = 팔이 자기 방향으로 뻗는 양(유닛, 음수면 몸 쪽으로 당김), curl = 몸 쪽으로 감는 각도, brain = 뇌 꿈틀
        private static Pose Grab(float reach, float curl, float brain)
        {
            var p = P();
            int i = 0;
            foreach (var arm in Limbs.Where(x => !x.Leg))
            {
                var dir = (arm.Tip - arm.Pivot).normalized;
                var toCenter = (BodyCenter - arm.Tip).normalized;
                float inward = Mathf.Sign(dir.x * toCenter.y - dir.y * toCenter.x);
                float spread = (Hash(i + 21) - 0.5f) * 24f; // 뻗을 때 각자 다른 방향으로
                p.B(arm.Name, dx: dir.x * reach, dy: dir.y * reach,
                    rot: inward * curl + spread * Mathf.Clamp01(reach / 0.12f));
                i++;
            }

            // 다리는 버티듯 살짝 벌어짐, 뇌는 위아래로 꿈틀
            foreach (var leg in Limbs.Where(x => x.Leg))
            {
                p.B(leg.Name, rot: (leg.Left ? -4f : 4f) * Mathf.Abs(reach) / 0.14f);
            }

            p.B("BrainTop", dy: 0.015f * brain).B("BrainL", dx: -0.008f * brain).B("BrainR", dx: 0.008f * brain);
            return p;
        }

        // 껍질 벗겨짐: 반쪽이 바닥 쪽 경첩을 축으로 바깥으로 젖혀지고, 붙은 팔다리도 같이 돌아가며 떨어진다.
        private static Pose Peel(float k)
        {
            var p = P();
            foreach (var piece in Pieces)
            {
                bool left = piece.Left;
                var hinge = left ? HingeL : HingeR;
                float angle = (left ? 1f : -1f) * 75f * k;
                var fall = new Vector2((left ? -0.12f : 0.12f) * k, -0.35f * k * k);
                var world = hinge + (Vector2)(Quaternion.Euler(0f, 0f, angle) * (piece.Pivot - hinge)) + fall * Ppu;
                p.B(piece.Name, dx: (world.x - piece.Pivot.x) / Ppu, dy: (world.y - piece.Pivot.y) / Ppu, rot: angle);
            }

            return p;
        }

        // 뇌: drop 0..1 = 바닥까지 떨어짐, melt 0..1 = 위가 내려앉고 옆으로 퍼짐
        private static Pose BrainMelt(float melt, float drop)
        {
            float floor = (BrainCenter.y - BrainRadius.y - 6f) / Ppu;
            return P()
                .B("Brain", dy: -floor * drop * drop)
                .B("BrainTop", dy: -0.17f * melt)
                .B("BrainBottom", dy: 0.04f * melt)
                .B("BrainL", dx: -0.1f * melt, dy: -0.07f * melt)
                .B("BrainR", dx: 0.1f * melt, dy: -0.07f * melt);
        }

        // 껍질(그림 본체)과 뇌를 따로 사라지게 (둘 다 처음에 붉게 움찔)
        private static void DieColors(AnimationClip clip)
        {
            void Color(string path, float fadeStart, float fadeEnd)
            {
                void Set(string prop, params (float t, float v)[] keys) =>
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SpriteRenderer), prop),
                        new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray()));
                Set("m_Color.r", (0f, 1f), (0.06f, 1f), (0.14f, 1f));
                Set("m_Color.g", (0f, 1f), (0.06f, 0.35f), (0.14f, 1f));
                Set("m_Color.b", (0f, 1f), (0.06f, 0.35f), (0.14f, 1f));
                Set("m_Color.a", (0f, 1f), (fadeStart, 1f), (fadeEnd, 0f));
            }

            Color("brainspider", 0.35f, 0.85f);
            Color("brainspider_brain", 1.2f, 1.6f);
        }

        // ---------- 소용돌이 뇌파 ----------

        private const string WaveRoot = "WaveFx";

        private static List<ClipFx.Track> WaveTracks()
        {
            string spiral = ClipFx.Generated("fx_spiral", (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                if (r > 1f)
                {
                    return Color.clear;
                }

                float theta = Mathf.Atan2(v, u) / Tau + 0.5f;
                float d = Mathf.Abs(Mathf.Repeat(r * 2.5f - theta, 1f) - 0.5f); // 2.5바퀴 아르키메데스 나선
                float line = Mathf.Clamp01(1f - Mathf.Abs(d - 0.5f) / 0.15f);
                return new Color(1f, 1f, 1f, line * Mathf.Clamp01((1f - r) * 4f) * Mathf.Clamp01(r * 6f));
            });

            var purple = new Color(0.7f, 0.3f, 1f, 1f);
            var tracks = new List<ClipFx.Track>();
            for (int k = 0; k < 3; k++)
            {
                float t0 = 0.45f + 0.1f * k;
                tracks.Add(new ClipFx.Track
                {
                    Name = "Spiral" + k,
                    Sprite = spiral,
                    Order = 40,
                    Rotation = k * 60f,
                    Spin = 320f,
                    Keys = new[]
                    {
                        (0f, Vector2.zero, Vector2.one * 0.2f, ClipFx.Alpha(purple, 0f)),
                        (t0, Vector2.zero, Vector2.one * 0.2f, ClipFx.Alpha(purple, 0f)),
                        (t0 + 0.06f, Vector2.zero, Vector2.one * 0.5f, purple),
                        (t0 + 0.42f, Vector2.zero, Vector2.one * 1.7f, ClipFx.Alpha(purple, 0f)),
                    },
                });
            }

            return tracks;
        }

        // ---------- 그림 준비 ----------

        private static List<LayerSpec> Layers()
        {
            var (main, brain, owner) = Prepare();
            var size = new Vector2Int(329, 300);
            var mesh = GolemStones.BuildMesh(owner, size.x, size.y, Pieces.Length, i => Pieces[i].Name, i => Pieces[i].Rank, minCell: 2);
            return new List<LayerSpec>
            {
                new()
                {
                    Name = "brainspider", Pixels = main, PixelsSize = size, Bones = Pieces.Select(p => p.Name).ToArray(),
                    MeshVertices = mesh.vertices, MeshIndices = mesh.indices, MeshVertexBone = mesh.vertexBone,
                },
                new() { Name = "brainspider_brain", Pixels = brain, PixelsSize = size, Bones = new[] { "Brain", "BrainTop", "BrainBottom", "BrainL", "BrainR" } },
            };
        }

        // 뇌 = 뇌 타원 안(+외곽선 3px), 껍질 = 몸 원 안(뇌 제외, 좌/우 반), 팔다리 = 원 밖(덩어리별로 가까운 팔다리 선)
        private static (Color32[] main, Color32[] brain, int[] owner) Prepare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var src = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);

            Vector2 P(int i) => new(i % w, i / w);
            float Ellipse(int i, float margin)
            {
                var d = P(i) - BrainCenter;
                return d.x * d.x / ((BrainRadius.x + margin) * (BrainRadius.x + margin)) + d.y * d.y / ((BrainRadius.y + margin) * (BrainRadius.y + margin));
            }

            float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            // 뇌: 타원 안쪽 깊은 곳은 전부, 가장자리 띠는 회색·외곽선만 (흰 껍질은 껍질로)
            bool InBrain(int i) => Ellipse(i, -8f) <= 1f || (Ellipse(i, 2f) <= 1f && Lum(src[i]) < 0.85f);

            bool InBody(int i)
            {
                var d = P(i) - BodyCenter;
                return d.x * d.x / (BodyRadius.x * BodyRadius.x) + d.y * d.y / (BodyRadius.y * BodyRadius.y) <= 1f;
            }

            var main = new Color32[src.Length];
            var brain = new Color32[src.Length];
            var owner = Enumerable.Repeat(-1, src.Length).ToArray();
            int shellL = System.Array.FindIndex(Pieces, p => p.Name == "ShellL");
            int shellR = System.Array.FindIndex(Pieces, p => p.Name == "ShellR");
            var limbIndex = Enumerable.Range(0, Pieces.Length).Where(k => !Pieces[k].Name.StartsWith("Shell")).ToArray();

            int Nearest(Vector2 pt) => limbIndex.OrderBy(k =>
            {
                var line = Pieces[k].Line;
                float d = float.MaxValue;
                for (int s = 0; s < line.Length - 1; s++)
                {
                    d = Mathf.Min(d, GolemRig.Distance(pt, line[s], line[s + 1]));
                }

                return d;
            }).First();

            for (int i = 0; i < src.Length; i++)
            {
                if (src[i].a <= 8)
                {
                    continue;
                }

                if (InBrain(i)) { brain[i] = src[i]; continue; }
                main[i] = src[i];
                if (InBody(i))
                {
                    owner[i] = P(i).x < BrainCenter.x ? shellL : shellR;
                }
            }

            // 원 밖 덩어리: 80% 이상이 한 팔다리면 통째로, 아니면 픽셀마다
            var seen = new bool[src.Length];
            var queue = new Queue<int>();
            for (int s = 0; s < src.Length; s++)
            {
                if (seen[s] || main[s].a == 0 || owner[s] >= 0)
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
                    foreach (int j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                    {
                        if (j >= 0 && !seen[j] && main[j].a > 0 && owner[j] < 0) { seen[j] = true; queue.Enqueue(j); }
                    }
                }

                var nearest = comp.Select(i => Nearest(P(i))).ToArray();
                int top = nearest.GroupBy(k => k).OrderByDescending(g => g.Count()).First().Key;
                bool whole = nearest.Count(k => k == top) >= comp.Count * 0.8f;
                for (int c = 0; c < comp.Count; c++)
                {
                    owner[comp[c]] = whole ? top : nearest[c];
                }
            }

            // 팔다리 회전 중심 = 그 팔다리에서 몸통 중심에 가장 가까운 픽셀 (원을 벗어나는 지점)
            s_pivots = new Dictionary<string, Vector2>();
            for (int k = 0; k < Pieces.Length; k++)
            {
                if (Pieces[k].Name.StartsWith("Shell"))
                {
                    continue;
                }

                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < owner.Length; i++)
                {
                    if (owner[i] != k)
                    {
                        continue;
                    }

                    float d = Vector2.Distance(P(i), BodyCenter);
                    if (d < bestD) { bestD = d; best = i; }
                }

                if (best >= 0)
                {
                    s_pivots[Pieces[k].Name] = P(best);
                }
            }

            return (main, brain, owner);
        }

        /// <summary>파츠 분리 디버그</summary>
        public static string WritePreview(string path)
        {
            var (main, brain, owner) = Prepare();
            var tex = new Texture2D(329, 300, TextureFormat.RGBA32, false);
            for (int i = 0; i < main.Length; i++)
            {
                Color c = new Color(0.75f, 0.85f, 1f, 1f);
                if (owner[i] >= 0)
                {
                    c = Color.Lerp(main[i], Color.HSVToRGB(owner[i] * 0.618f % 1f, 0.9f, 1f), 0.55f);
                }

                if (brain[i].a > 0)
                {
                    c = Color.Lerp(brain[i], Color.magenta, 0.25f);
                }

                tex.SetPixel(i % 329, i / 329, c);
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"pieces={owner.Where(o => o >= 0).Distinct().Count()}/{Pieces.Length}";
        }
    }
}
