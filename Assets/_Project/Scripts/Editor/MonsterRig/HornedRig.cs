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
    /// 뿔 달린 녀석(horn.png 174x322): 뿔 / 머리 / 몸통 / 팔 2 / 다리 2 (그림 한 장 + 파츠별 메시) + 얼굴 십자(별도 레이어).
    /// 모든 본은 루트이고, 따라가는 동작은 FK로 직접 계산한다. 좌표는 PNG 픽셀, 좌하단 원점.
    /// </summary>
    public static class HornedRig
    {
        private const string Png = "Assets/_Project/Art/Monsters/horn.png";
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;

        private sealed class Piece
        {
            public string Name;
            public Vector2 A, B;     // 배정용 선분, 본은 A(회전 중심) → B
            public float Radius;
            public int Rank;
        }

        private static Vector2 V(float x, float y) => new(x, y);

        private static readonly Piece[] Pieces =
        {
            new() { Name = "LegL", A = V(55, 51), B = V(50, 3), Radius = 0, Rank = 0 },
            new() { Name = "LegR", A = V(120, 45), B = V(120, 11), Radius = 0, Rank = 0 },
            new() { Name = "ArmR", A = V(145, 151), B = V(155, 63), Radius = 0, Rank = 0 },
            new() { Name = "Body", A = V(88, 60), B = V(90, 160), Radius = 38, Rank = 1 },
            new() { Name = "ArmL", A = V(45, 171), B = V(25, 56), Radius = 0, Rank = 2 },
            new() { Name = "Head", A = V(97, 165), B = V(105, 235), Radius = 42, Rank = 3 },
            new() { Name = "Horn", A = V(128, 221), B = V(163, 319), Radius = 4, Rank = 3 },
        };

        private static Piece Get(string n) => Pieces.First(p => p.Name == n);

        // 팔다리 영역: 원본에서 팔과 몸통 사이 안쪽 외곽선, 몸통 아랫선을 따라 그린 다각형 (위 기준 y)
        private static readonly Dictionary<string, Vector2[]> LimbShapes = new()
        {
            ["ArmL"] = new[] { V(45, 138), V(62, 172), V(58, 190), V(56, 262), V(48, 282), V(10, 282), V(-2, 240), V(-2, 190), V(18, 150) },
            ["ArmR"] = new[] { V(138, 163), V(152, 166), V(164, 212), V(172, 250), V(168, 268), V(144, 270), V(137, 262), V(135, 200) },
            ["LegL"] = new[] { V(28, 268), V(82, 266), V(80, 322), V(28, 322) },
            ["LegR"] = new[] { V(98, 274), V(144, 268), V(144, 314), V(98, 314) },
        };

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
        private static readonly Vector2 Feet = V(92, 4); // 쓰러질 때 축

        private sealed class Art
        {
            public Vector2Int Size;
            public Color32[] Main, Cross;
            public int[] Owner;
            public Dictionary<string, Color32[]> Limbs; // 팔다리는 별도 레이어 (몸 쪽 띠는 Main에서 몸 색으로 메움)
            public Vector2 CrossCenter;
        }

        private static Art s_art;

        internal static MonsterRigs.Def Def()
        {
            var art = s_art ??= Prepare();
            var cc = art.CrossCenter;
            const float walk = 1f;

            var bones = Pieces.Select(p => Bone(p.Name, null, p.A.x, p.A.y, p.B.x, p.B.y)).ToList();
            bones.Add(Bone("Cross", null, cc.x, cc.y, cc.x, cc.y + 12));

            Pose Move(float t)
            {
                float ph = t * Tau / walk, s = Mathf.Sin(ph);
                var p = P();
                var body = new Vector2(0f, 0.012f * s * s);
                Upper(p, body, bodyRot: 2f * s, headRot: -1.5f * Mathf.Sin(ph - 0.5f), armL: 9f * s, armR: 9f * s, cross: 0f);
                // 다리: 반대로 흔들며 앞으로 내딛는 다리가 살짝 들림
                p.B("LegL", dy: 0.012f * Mathf.Max(0f, s), rot: -8f * s);
                p.B("LegR", dy: 0.012f * Mathf.Max(0f, -s), rot: -8f * s);
                return p;
            }

            return new MonsterRigs.Def
            {
                Name = "Horned",
                Size = art.Size,
                PivotPx = V(87, 0),
                CustomLayers = () => Layers(art),
                Bones = bones.ToArray(),
                MoveLength = walk,
                MoveKeys = 8,
                Move = Move,
                // 뒤로 젖히며 웅크림 → 몸과 머리를 숙여 뿔을 앞세우고 오른쪽(바라보는 쪽)으로 짧게 돌진 → 복귀
                Attack = new[]
                {
                    (0f, P()),
                    (0.15f, Dash(move: V(-0.015f, -0.015f), lean: 6f, head: 4f, arms: 20f, legs: 4f)),
                    (0.3f, Dash(move: V(0.1f, -0.02f), lean: -14f, head: -10f, arms: -25f, legs: -12f)),
                    (0.4f, Dash(move: V(0.12f, -0.02f), lean: -16f, head: -12f, arms: -28f, legs: -14f)),
                    (0.55f, Dash(move: V(0.03f, 0f), lean: -3f, head: -2f, arms: -5f, legs: -3f)),
                    (0.7f, P()),
                },
                // 움찔 → 뿔이 부러져 튕겨 나가 떨어짐 → 십자가 45° 돌아 X → 발끝 축으로 뒤로 쓰러짐 → 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.06f, Fall(topple: 0f, cross: 0f, horn: 0f).Tint(1f)),
                    (0.15f, Fall(topple: -4f, cross: 10f, horn: 0.1f)),
                    (0.35f, Fall(topple: 8f, cross: 45f, horn: 0.45f)),
                    (0.6f, Fall(topple: 45f, cross: 45f, horn: 0.85f)),
                    (0.75f, Fall(topple: 82f, cross: 45f, horn: 1f)),
                    (0.85f, Fall(topple: 76f, cross: 45f, horn: 1f)),
                    (0.95f, Fall(topple: 80f, cross: 45f, horn: 1f)),
                    (1.3f, Fall(topple: 80f, cross: 45f, horn: 1f).Tint(0f, 0f)),
                },
            };
        }

        // ---------- 자세 (FK) ----------

        private static Vector2 Turn(Vector2 pt, Vector2 about, float deg) =>
            about + (Vector2)(Quaternion.Euler(0f, 0f, deg) * (pt - about));

        private static void Put(Pose p, string bone, Vector2 world, float rot, Vector2 rest) =>
            p.B(bone, dx: (world.x - rest.x) / Ppu, dy: (world.y - rest.y) / Ppu, rot: rot);

        // 몸통(엉덩이 축 회전) → 머리(목 축) → 뿔·십자(머리를 따라감), 팔(어깨 축)
        private static void Upper(Pose p, Vector2 body, float bodyRot, float headRot, float armL, float armR, float cross,
            bool hornFollows = true)
        {
            var hip = Get("Body").A;
            var neck = Get("Head").A;
            Vector2 OnBody(Vector2 pt) => Turn(pt, hip, bodyRot) + body * Ppu;
            Vector2 OnHead(Vector2 pt) => OnBody(Turn(pt, neck, headRot));

            Put(p, "Body", OnBody(hip), bodyRot, hip);
            Put(p, "Head", OnHead(neck), bodyRot + headRot, neck);
            if (hornFollows)
            {
                Put(p, "Horn", OnHead(Get("Horn").A), bodyRot + headRot, Get("Horn").A);
            }

            var cc = s_art.CrossCenter;
            Put(p, "Cross", OnHead(cc), bodyRot + headRot + cross, cc);
            Put(p, "ArmL", OnBody(Get("ArmL").A), bodyRot + armL, Get("ArmL").A);
            Put(p, "ArmR", OnBody(Get("ArmR").A), bodyRot + armR, Get("ArmR").A);
        }

        // 돌진: move = 전체 이동(유닛), lean = 몸 기울기(음수 = 앞/오른쪽), head = 머리 숙임, arms = 팔 젖힘, legs = 다리 벌림
        private static Pose Dash(Vector2 move, float lean, float head, float arms, float legs)
        {
            var p = P();
            Upper(p, move, lean, head, arms, arms, 0f);
            p.B("LegL", dx: move.x, dy: move.y, rot: legs);
            p.B("LegR", dx: move.x, dy: move.y, rot: -legs * 0.7f);
            return p;
        }

        // 죽음: topple = 발끝 축으로 뒤로 쓰러지는 각도, cross = 십자 회전, horn 0..1 = 부러진 뿔이 떨어진 정도
        private static Pose Fall(float topple, float cross, float horn)
        {
            var p = P();
            Upper(p, Vector2.zero, 0f, 0f, 10f * topple / 80f, -10f * topple / 80f, cross, hornFollows: false);
            p.B("LegL").B("LegR");

            // 뿔 제외 전체를 발끝 기준으로 회전
            foreach (var name in new[] { "Body", "Head", "Cross", "ArmL", "ArmR", "LegL", "LegR" })
            {
                var rest = name == "Cross" ? s_art.CrossCenter : Get(name).A;
                var d = p.Bones.TryGetValue(name, out var cur) ? cur : (Vector2.zero, 0f);
                var world = Turn(rest + d.Item1 * Ppu, Feet, topple);
                Put(p, name, world, d.Item2 + topple, rest);
            }

            // 뿔: 위로 튕겨 오른쪽으로 날아가며 회전하다 바닥에 떨어짐 (horn 0..1 진행도에 포물선)
            var hornRest = Get("Horn").A;
            float k = horn;
            var flight = V(70f * k, 30f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.4f)) - (hornRest.y - 18f) * k * k);
            Put(p, "Horn", hornRest + flight, -150f * k, hornRest);
            return p;
        }

        // ---------- 그림 준비 ----------

        private static readonly string[] LimbNames = { "LegL", "LegR", "ArmR", "ArmL" };

        // PSD 레이어 순서(아래 → 위): 다리, 오른팔(몸 뒤) → 몸통·머리·뿔 → 왼팔 → 십자
        private static List<LayerSpec> Layers(Art art)
        {
            var mesh = GolemStones.BuildMesh(art.Owner, art.Size.x, art.Size.y, Pieces.Length, i => Pieces[i].Name, i => Pieces[i].Rank, minCell: 2);
            LayerSpec Limb(string name) => new() { Name = "horned_" + name.ToLowerInvariant(), Pixels = art.Limbs[name], PixelsSize = art.Size, Bones = new[] { name } };
            return new List<LayerSpec>
            {
                Limb("LegL"), Limb("LegR"), Limb("ArmR"),
                new()
                {
                    Name = "horned", Pixels = art.Main, PixelsSize = art.Size,
                    Bones = Pieces.Where(p => !LimbNames.Contains(p.Name)).Select(p => p.Name).ToArray(),
                    MeshVertices = mesh.vertices, MeshIndices = mesh.indices, MeshVertexBone = mesh.vertexBone,
                },
                Limb("ArmL"),
                new() { Name = "horned_cross", Pixels = art.Cross, PixelsSize = art.Size, Bones = new[] { "Cross" } },
            };
        }

        private static Art Prepare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var src = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);
            float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

            // 1) 얼굴 안의 어두운 획 = 십자 (얼굴 범위: top-down x 105~170, y 95~165)
            var face = new RectInt(90, h - 1 - 162, 75, 62);
            bool Dark(int i) => src[i].a > 128 && Lum(src[i]) < 0.5f;
            var cross = new bool[src.Length];
            foreach (var comp in Components(src.Length, w, h, Dark))
            {
                if (comp.Count >= 10 && comp.All(i => face.Contains(new Vector2Int(i % w, i / w))))
                {
                    foreach (int i in comp)
                    {
                        cross[i] = true;
                    }
                }
            }

            for (int pass = 0; pass < 2; pass++)
            {
                var next = (bool[])cross.Clone();
                for (int i = 0; i < src.Length; i++)
                {
                    if (!cross[i])
                    {
                        continue;
                    }

                    foreach (int j in N4(i, w, h))
                    {
                        if (src[j].a > 128 && Lum(src[j]) < 0.92f)
                        {
                            next[j] = true;
                        }
                    }
                }

                cross = next;
            }

            var cols = new int[w];
            var rows = new int[h];
            for (int i = 0; i < src.Length; i++)
            {
                if (cross[i]) { cols[i % w]++; rows[i / w]++; }
            }

            var center = V(System.Array.IndexOf(cols, cols.Max()), System.Array.IndexOf(rows, rows.Max()));

            // 2) 본 그림에서 십자 자리는 얼굴 바탕색으로 메운다
            float pr = 0, pg = 0, pb = 0;
            int pn = 0;
            for (int i = 0; i < src.Length; i++)
            {
                if (cross[i] || src[i].a < 250 || Lum(src[i]) < 0.88f || !face.Contains(new Vector2Int(i % w, i / w)))
                {
                    continue;
                }

                pr += src[i].r; pg += src[i].g; pb += src[i].b; pn++;
            }

            var paper = new Color32((byte)(pr / pn), (byte)(pg / pn), (byte)(pb / pn), 255);
            var main = (Color32[])src.Clone();
            var crossPx = new Color32[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (cross[i]) { crossPx[i] = src[i]; main[i] = paper; }
            }

            // 3) 외곽선 조각 → 파츠: 조각의 80% 이상이 한 파츠면 통째로, 아니면 픽셀마다 가까운 파츠
            var seg = GolemStones.Segment(main, w, h, new int[main.Length], 1, -1, c => Lum(c) < 0.3f, 20);
            var nearest = new int[main.Length];
            for (int i = 0; i < main.Length; i++)
            {
                nearest[i] = -1;
                if (seg.Owner[i] < 0)
                {
                    continue;
                }

                var pt = V(i % w, i / w);
                float best = float.MaxValue;
                for (int k = 0; k < Pieces.Length; k++)
                {
                    if (LimbShapes.ContainsKey(Pieces[k].Name))
                    {
                        continue; // 팔다리는 아래에서 다각형으로 정한다
                    }

                    float d = GolemRig.Distance(pt, Pieces[k].A, Pieces[k].B) - Pieces[k].Radius;
                    if (d < best) { best = d; nearest[i] = k; }
                }
            }

            var owner = Enumerable.Repeat(-1, main.Length).ToArray();
            // 팔다리 다각형 안의 픽셀은 해당 팔다리 (위 기준 y로 판정)
            var limbOf = new int[main.Length];
            for (int i = 0; i < main.Length; i++)
            {
                limbOf[i] = -1;
                if (seg.Owner[i] < 0)
                {
                    continue;
                }

                var td = V(i % w, h - 1 - i / w);
                foreach (var (name, poly) in LimbShapes)
                {
                    if (Inside(poly, td)) { limbOf[i] = System.Array.FindIndex(Pieces, p => p.Name == name); break; }
                }
            }

            foreach (var st in seg.Stones)
            {
                var votes = new int[Pieces.Length];
                int total = 0;
                for (int i = 0; i < main.Length; i++)
                {
                    if (seg.Owner[i] == st.Id) { votes[nearest[i]]++; total++; }
                }

                int top = System.Array.IndexOf(votes, votes.Max());
                bool whole = votes[top] >= total * 0.8f;
                for (int i = 0; i < main.Length; i++)
                {
                    if (seg.Owner[i] == st.Id)
                    {
                        owner[i] = limbOf[i] >= 0 ? limbOf[i] : whole ? top : nearest[i];
                    }
                }
            }

            // 4) 팔다리를 떼어 별도 레이어로. 몸 쪽으로 붙어 있던 띠(몸에서 6px 이내)는 몸 색으로 메워 틈을 막는다.
            int bodyIndex = System.Array.FindIndex(Pieces, p => p.Name == "Body");
            var limbs = new Dictionary<string, Color32[]>();
            var limbIndex = LimbNames.Select(n => System.Array.FindIndex(Pieces, p => p.Name == n)).ToHashSet();
            var dist = Enumerable.Repeat(int.MaxValue, main.Length).ToArray();
            var bfs = new Queue<int>();
            for (int i = 0; i < main.Length; i++)
            {
                if (owner[i] >= 0 && !limbIndex.Contains(owner[i])) { dist[i] = 0; bfs.Enqueue(i); }
            }

            while (bfs.Count > 0)
            {
                int i = bfs.Dequeue();
                if (dist[i] >= 6)
                {
                    continue;
                }

                foreach (int j in N4(i, w, h))
                {
                    if (owner[j] >= 0 && limbIndex.Contains(owner[j]) && dist[j] > dist[i] + 1) { dist[j] = dist[i] + 1; bfs.Enqueue(j); }
                }
            }

            foreach (var name in LimbNames)
            {
                int k = System.Array.FindIndex(Pieces, p => p.Name == name);
                var px = new Color32[main.Length];
                for (int i = 0; i < main.Length; i++)
                {
                    if (owner[i] == k)
                    {
                        px[i] = main[i];
                    }
                }

                limbs[name] = px;
            }

            for (int i = 0; i < main.Length; i++)
            {
                if (owner[i] < 0 || !limbIndex.Contains(owner[i]))
                {
                    continue;
                }

                if (dist[i] <= 6) { main[i] = paper; owner[i] = bodyIndex; }
                else { main[i] = default; owner[i] = -1; }
            }

            return new Art { Size = new Vector2Int(w, h), Main = main, Cross = crossPx, Owner = owner, CrossCenter = center, Limbs = limbs };
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

        /// <summary>파츠 분리 디버그: 파츠별 색 + 십자(검정)</summary>
        public static string WritePreview(string path)
        {
            s_art = null;
            var art = s_art = Prepare();
            var tex = new Texture2D(art.Size.x, art.Size.y, TextureFormat.RGBA32, false);
            for (int i = 0; i < art.Main.Length; i++)
            {
                Color c = art.Owner[i] < 0 ? Color.clear
                    : Color.Lerp(art.Main[i], Color.HSVToRGB((art.Owner[i] * 0.618f) % 1f, 0.9f, 1f), 0.55f);
                int limb = 0;
                foreach (var name in LimbNames)
                {
                    limb++;
                    if (art.Limbs[name][i].a > 0)
                    {
                        c = Color.Lerp(art.Limbs[name][i], Color.HSVToRGB(limb * 0.23f % 1f, 1f, 0.9f), 0.6f);
                    }
                }

                if (art.Cross[i].a > 0)
                {
                    c = Color.black;
                }

                tex.SetPixel(i % art.Size.x, i / art.Size.x, c);
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"crossCenter={art.CrossCenter} pieces={art.Owner.Where(o => o >= 0).Distinct().Count()}/{Pieces.Length}";
        }
    }
}
