using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static Game.Editor.MonsterRig.MonsterRigBuilder;
using static Game.Editor.MonsterRig.MonsterRigs;
using Pose = Game.Editor.MonsterRig.MonsterRigs.Pose;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 거미: 몸통, 머리, 이빨 2, 다리 6개 × (허벅지 / 종아리) = 파츠 16개, 본 16개(모두 루트).
    /// 외곽선으로 나눈 조각을 관절 선분 기준으로 파츠에 묶고, 여러 파츠에 걸친 조각만 픽셀 단위로 나눈다.
    /// 좌표는 spider.png(449x291) 픽셀, 좌하단 원점.
    /// </summary>
    public static class SpiderRig
    {
        private const string Png = "Assets/_Project/Art/Characters/Monsters/spider.png";
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;

        private sealed class Piece
        {
            public string Name;
            public Vector2[] Line;   // 픽셀 배정용 선 (본은 첫 점 → 마지막 점)
            public float Radius;
            public int Rank;         // 그리는 순서
            public int Side;         // 다리: -1 왼쪽, +1 오른쪽
            public Vector2 Pivot => Line[0];
            public Vector2 Tip => Line[^1];
        }

        private static Vector2 V(float x, float y) => new(x, y);

        // 다리: 몸 쪽 관절(엉덩이) → 무릎 → (꺾임) → 발끝. 오른쪽 R2/R3과 L3은 몸 뒤라 먼저 그린다.
        private static readonly Piece[] Pieces =
        {
            Leg("L1U", -1, 2, V(145, 150), V(55, 248)), Leg("L1L", -1, 2, V(55, 248), V(8, 130), V(25, 25)),
            Leg("L2U", -1, 2, V(150, 170), V(85, 260)), Leg("L2L", -1, 2, V(85, 260), V(72, 140), V(75, 60)),
            Leg("L3U", -1, 0, V(165, 190), V(140, 280)), Leg("L3L", -1, 0, V(140, 280), V(115, 230), V(88, 160)),
            Leg("R1U", 1, 2, V(245, 140), V(330, 262)), Leg("R1L", 1, 2, V(330, 262), V(352, 115), V(352, 5)),
            Leg("R2U", 1, 0, V(275, 145), V(372, 262)), Leg("R2L", 1, 0, V(372, 262), V(380, 160), V(381, 70)),
            Leg("R3U", 1, 0, V(300, 195), V(360, 268)), Leg("R3L", 1, 0, V(360, 268), V(430, 140), V(440, 45)),
            new() { Name = "Body", Line = new[] { V(215, 178), V(250, 232), V(292, 250) }, Radius = 30f, Rank = 1 },
            new() { Name = "Head", Line = new[] { V(185, 150), V(165, 145), V(205, 135) }, Radius = 32f, Rank = 3 },
            new() { Name = "FangL", Line = new[] { V(150, 112), V(150, 82) }, Radius = 6f, Rank = 4 },
            new() { Name = "FangR", Line = new[] { V(195, 114), V(195, 78) }, Radius = 6f, Rank = 4 },
        };

        private static Piece Leg(string name, int side, int rank, params Vector2[] line) =>
            new() { Name = name, Line = line, Radius = 0f, Rank = rank, Side = side };

        private static Piece Get(string name) => Pieces.First(p => p.Name == name);

        // 교차 보행: A조(L1, R2, L3)와 B조(R1, L2, R3)가 반 박자씩 엇갈린다.
        private static readonly (string leg, float phase)[] Gait =
        {
            ("L1", 0f), ("R2", 0f), ("L3", 0f), ("R1", Mathf.PI), ("L2", Mathf.PI), ("R3", Mathf.PI),
        };

        internal static MonsterRigs.Def Def()
        {
            const float length = 0.9f;

            Pose Move(float t)
            {
                float ph = t * Tau / length;
                var body = new Vector2(0f, 0.01f * Mathf.Pow(Mathf.Sin(2f * ph), 2f));
                var p = P();
                BodyAndHead(p, body, 1.2f * Mathf.Sin(ph), Vector2.zero, 0f, 4f * Mathf.Sin(2f * ph));
                foreach (var (leg, phase) in Gait)
                {
                    float s = Mathf.Sin(ph + phase), up = Mathf.Max(0f, s);
                    int side = Get(leg + "U").Side;
                    // 들어 올릴 때 무릎이 올라가고 발끝이 안으로 접히며, 앞뒤로 조금 흔들린다
                    LegFk(p, leg, body, upper: -side * (12f * up) + side * 4f * Mathf.Cos(ph + phase), lower: side * 9f * up);
                }

                return p;
            }

            return new MonsterRigs.Def
            {
                Name = "Spider",
                Size = new Vector2Int(449, 291),
                PivotPx = new Vector2(225, 0),
                CustomLayers = Layers,
                Bones = Pieces.Select(p => Bone(p.Name, null, p.Pivot.x, p.Pivot.y, p.Tip.x, p.Tip.y)).ToArray(),
                MoveLength = length,
                MoveKeys = 8,
                Move = Move,
                // 몸을 뒤로 당기며 이빨을 벌림 → 몸과 머리를 앞으로 내밀고 → 이빨이 양쪽에서 닫히며 깨묾 → 복귀
                Attack = new[]
                {
                    (0f, P()),
                    (0.15f, Bite(lunge: -0.4f, fangs: 25f)),
                    (0.3f, Bite(lunge: 1f, fangs: 28f)),
                    (0.36f, Bite(lunge: 1.1f, fangs: -14f)),
                    (0.46f, Bite(lunge: 0.9f, fangs: -12f)),
                    (0.62f, P()),
                },
                // 움찔하며 살짝 들림 → 다리를 양옆으로 쫙 펼치며 몸이 바닥에 철퍼덕 → 작게 튕김 → 꿈틀 → 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.06f, Splat(splay: -0.08f, drop: -0.02f, fangs: 10f).Tint(1f)),
                    (0.28f, Splat(splay: 1f, drop: 1f, fangs: 18f)),
                    (0.36f, Splat(splay: 1.05f, drop: 0.88f, fangs: 14f)),
                    (0.45f, Splat(splay: 1f, drop: 1f, fangs: 12f)),
                    (0.7f, Splat(splay: 0.94f, drop: 1f, fangs: 6f)),
                    (1.1f, Splat(splay: 1f, drop: 1f, fangs: 6f).Tint(0f, 0f)),
                },
            };
        }

        // ---------- 자세 ----------

        // 몸통 + 머리(+이빨). head = 몸 대비 머리 추가 이동/회전, fangs = 이빨 벌림(+)/닫힘(-)
        private static void BodyAndHead(Pose p, Vector2 body, float bodyRot, Vector2 head, float headRot, float fangs)
        {
            var bodyPivot = Get("Body").Pivot;
            Place(p, "Body", body, bodyRot, bodyPivot, body, bodyRot);
            var headMove = body + head;
            Place(p, "Head", headMove, bodyRot + headRot, bodyPivot, body, bodyRot);
            foreach (var (fang, open) in new[] { ("FangL", -fangs), ("FangR", fangs) })
            {
                Place(p, fang, headMove, bodyRot + headRot + open, Get("Head").Pivot, headMove, bodyRot + headRot);
            }
        }

        // 부모(pivot 기준 move/rot)를 따라가며 자기 회전 rot를 가진 파츠 배치
        private static void Place(Pose p, string name, Vector2 move, float rot, Vector2 parentPivot, Vector2 parentMove, float parentRot)
        {
            var self = Get(name).Pivot;
            var world = parentPivot + parentMove * Ppu + (Vector2)(Quaternion.Euler(0f, 0f, parentRot) * (self - parentPivot));
            world += (move - parentMove) * Ppu;
            p.B(name, dx: (world.x - self.x) / Ppu, dy: (world.y - self.y) / Ppu, rot: rot);
        }

        // 다리 FK: 엉덩이는 몸을 따라 이동, 허벅지 회전 → 종아리는 무릎을 따라감
        private static void LegFk(Pose p, string leg, Vector2 hipMove, float upper, float lower)
        {
            var u = Get(leg + "U");
            var l = Get(leg + "L");
            var hip = u.Pivot + hipMove * Ppu;
            var knee = hip + (Vector2)(Quaternion.Euler(0f, 0f, upper) * (l.Pivot - u.Pivot));
            p.B(u.Name, dx: hipMove.x, dy: hipMove.y, rot: upper);
            p.B(l.Name, dx: (knee.x - l.Pivot.x) / Ppu, dy: (knee.y - l.Pivot.y) / Ppu, rot: upper + lower);
        }

        // 다리 IK: 엉덩이가 몸을 따라 움직여도 발끝은 바닥에 고정. 무릎 꺾이는 방향은 원래 그림대로.
        private static void LegIk(Pose p, string leg, Vector2 hipMove)
        {
            var u = Get(leg + "U");
            var l = Get(leg + "L");
            Vector2 hip0 = u.Pivot, knee0 = l.Pivot, foot = l.Tip;
            float a = Vector2.Distance(hip0, knee0), b = Vector2.Distance(knee0, foot);
            var hip = hip0 + hipMove * Ppu;
            var toFoot = foot - hip;
            float d = Mathf.Clamp(toFoot.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.01f);
            float bend = Mathf.Acos(Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f)) * Mathf.Rad2Deg;
            float sign = Mathf.Sign(Cross(knee0 - hip0, foot - hip0)); // 원래 무릎이 발끝 선의 어느 쪽인지
            float thigh = Angle(toFoot) - sign * bend;
            var knee = hip + (Vector2)(Quaternion.Euler(0f, 0f, thigh) * Vector2.right * a);
            p.B(u.Name, dx: hipMove.x, dy: hipMove.y, rot: Mathf.DeltaAngle(Angle(knee0 - hip0), thigh));
            p.B(l.Name, dx: (knee.x - knee0.x) / Ppu, dy: (knee.y - knee0.y) / Ppu,
                rot: Mathf.DeltaAngle(Angle(foot - knee0), Angle(foot - knee)));
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        // 깨물기: lunge 1 = 머리 쪽(왼쪽 아래)으로 최대한 내민 자세, 음수면 뒤로 당김. 발은 IK로 고정.
        private static Pose Bite(float lunge, float fangs)
        {
            var p = P();
            var body = new Vector2(-0.06f, -0.07f) * lunge;
            var head = new Vector2(-0.045f, -0.055f) * lunge;
            BodyAndHead(p, body, 3f * lunge, head, 4f * lunge, fangs);
            foreach (var (leg, _) in Gait)
            {
                LegIk(p, leg, body);
            }

            return p;
        }

        // 다리를 펼친 목표 각도(도): 앞다리일수록 앞쪽, 끝마디는 바닥으로 조금 처진다.
        private static readonly Dictionary<string, float> SplayAngle = new()
        {
            ["L1"] = 205f,
            ["L2"] = 182f,
            ["L3"] = 160f,
            ["R1"] = -25f,
            ["R2"] = -2f,
            ["R3"] = 20f,
        };

        // 엎어짐: splay 0..1 = 다리 펼침, drop 0..1 = 몸이 바닥까지 내려간 정도
        private static Pose Splat(float splay, float drop, float fangs)
        {
            var p = P();
            var body = new Vector2(0f, -0.25f * drop);
            BodyAndHead(p, body, 0f, new Vector2(0f, -0.02f * drop), 0f, fangs);
            foreach (var (leg, _) in Gait)
            {
                var u = Get(leg + "U");
                var l = Get(leg + "L");
                float target = SplayAngle[leg];
                float droop = u.Side < 0 ? 8f : -8f;
                float upper = Mathf.DeltaAngle(Angle(l.Pivot - u.Pivot), target) * splay;
                float lower = Mathf.DeltaAngle(Angle(l.Tip - l.Pivot), target + droop) * splay;
                LegFk(p, leg, body, upper, lower - upper);
            }

            return p;
        }

        // ---------- 파츠 분리 ----------

        private static List<LayerSpec> Layers()
        {
            var (owner, r) = PieceOwner();
            var mesh = GolemStones.BuildMesh(owner, r.Width, r.Height, Pieces.Length, i => Pieces[i].Name, i => Pieces[i].Rank);
            return new List<LayerSpec>
            {
                new()
                {
                    Name = "spider",
                    Pixels = r.Image,
                    PixelsSize = new Vector2Int(r.Width, r.Height),
                    MeshVertices = mesh.vertices,
                    MeshIndices = mesh.indices,
                    MeshVertexBone = mesh.vertexBone,
                },
            };
        }

        // 조각의 80% 이상이 한 파츠면 조각째로, 아니면(몸통과 다리가 붙은 조각 등) 픽셀마다 가까운 파츠로
        private static (int[] owner, GolemStones.Result r) PieceOwner()
        {
            var r = Segment();
            int n = r.Owner.Length;
            var nearest = new int[n];
            for (int i = 0; i < n; i++)
            {
                nearest[i] = -1;
                if (r.Owner[i] < 0)
                {
                    continue;
                }

                var pt = new Vector2(i % r.Width, i / r.Width);
                float best = float.MaxValue;
                for (int k = 0; k < Pieces.Length; k++)
                {
                    var line = Pieces[k].Line;
                    float d = float.MaxValue;
                    for (int s = 0; s < line.Length - 1; s++)
                    {
                        d = Mathf.Min(d, GolemRig.Distance(pt, line[s], line[s + 1]));
                    }

                    d -= Pieces[k].Radius;
                    if (d < best) { best = d; nearest[i] = k; }
                }
            }

            var owner = new int[n];
            foreach (var st in r.Stones)
            {
                var votes = new int[Pieces.Length];
                int total = 0;
                for (int i = 0; i < n; i++)
                {
                    if (r.Owner[i] == st.Id) { votes[nearest[i]]++; total++; }
                }

                int top = System.Array.IndexOf(votes, votes.Max());
                bool whole = votes[top] >= total * 0.8f;
                for (int i = 0; i < n; i++)
                {
                    if (r.Owner[i] == st.Id)
                    {
                        owner[i] = whole ? top : nearest[i];
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (r.Owner[i] < 0)
                {
                    owner[i] = -1;
                }
            }

            return (owner, r);
        }

        internal static GolemStones.Result Segment()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var pixels = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);
            return GolemStones.Segment(pixels, w, h, new int[pixels.Length], 1, -1,
                c => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f < 0.22f, 40);
        }

        /// <summary>파츠 분리 디버그 이미지</summary>
        public static string WritePieceDebug(string path)
        {
            var (owner, r) = PieceOwner();
            var tex = new Texture2D(r.Width, r.Height, TextureFormat.RGBA32, false);
            var output = new Color[owner.Length];
            for (int i = 0; i < owner.Length; i++)
            {
                output[i] = owner[i] < 0 ? Color.clear : Color.Lerp(r.Image[i], Color.HSVToRGB((owner[i] * 0.618f) % 1f, 0.9f, 1f), 0.6f);
            }

            tex.SetPixels(output);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            var mesh = GolemStones.BuildMesh(owner, r.Width, r.Height, Pieces.Length, i => Pieces[i].Name, i => Pieces[i].Rank);
            return $"pieces={owner.Where(o => o >= 0).Distinct().Count()}/{Pieces.Length} verts={mesh.vertices.Length}";
        }
    }
}
