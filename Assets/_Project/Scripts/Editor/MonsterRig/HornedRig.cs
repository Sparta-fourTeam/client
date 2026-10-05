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
    /// 뿔 달린 녀석: 사용자가 직접 나눈 파츠 그림(horned_parts.png 1024px, 투명 배경)을 오른쪽 아래 완성본 위치로 조립한다.
    /// 레이어(아래 → 위): 오른팔 / 몸통(다리 포함, 본 3개) / 왼팔 / 머리 / 뿔 / 얼굴 십자.
    /// 모든 본은 루트이고, 따라가는 동작은 FK로 직접 계산한다. 좌표는 완성본 기준 원본 픽셀(좌상단 원점)로 적고 V()로 변환한다.
    /// </summary>
    public static class HornedRig
    {
        private const string Png = "Assets/_Project/Art/Characters/Monsters/horned_parts.png";
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;

        // 조립된 캔버스 범위 (완성본 좌표)
        private static readonly RectInt Canvas = new(570, 460, 190, 340);

        private sealed class Piece
        {
            public string Name;
            public Vector2 A, B; // 본은 A(회전 중심) → B
        }

        // 완성본 좌표(좌상단 원점) → 캔버스 픽셀(좌하단 원점)
        private static Vector2 V(float x, float y) => new(x - Canvas.x, Canvas.yMax - 1 - y);

        private static readonly Piece[] Pieces =
        {
            new() { Name = "LegL", A = V(632, 752), B = V(632, 795) },
            new() { Name = "LegR", A = V(705, 752), B = V(705, 785) },
            new() { Name = "ArmR", A = V(712, 624), B = V(725, 727) },
            new() { Name = "Body", A = V(665, 730), B = V(665, 600) },
            new() { Name = "ArmL", A = V(655, 617), B = V(598, 737) },
            new() { Name = "Head", A = V(688, 640), B = V(688, 520) },
            new() { Name = "Horn", A = V(718, 548), B = V(747, 472) },
        };

        private static Piece Get(string n) => Pieces.First(p => p.Name == n);

        private static readonly Vector2 Feet = V(665, 797); // 쓰러질 때 축

        // 파츠 그림의 각 파츠 범위(원본 좌표)와 완성본으로 옮기는 이동량 (완성본에 외곽선을 맞춰 구한 값)
        private static readonly (string name, RectInt box, Vector2Int offset)[] Sources =
        {
            ("head", new RectInt(340, 40, 160, 195), new Vector2Int(271, 420)),
            ("body", new RectInt(315, 260, 145, 235), new Vector2Int(277, 312)),
            ("armL", new RectInt(145, 290, 110, 150), new Vector2Int(423, 312)),
            ("armR", new RectInt(540, 295, 65, 135), new Vector2Int(150, 312)),
        };

        // 머리 원 (파츠 그림 좌표): 원 밖 오른쪽 위로 튀어나온 부분이 뿔
        private static readonly Vector2 HeadCenter = new(417, 158);
        private const float HeadRadius = 63f;

        private sealed class Art
        {
            public Vector2Int Size;
            public Dictionary<string, Color32[]> Layers;
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
                PivotPx = Feet,
                GridStep = 8,
                CustomLayers = () => Layers(art),
                Bones = bones.ToArray(),
                MoveLength = walk,
                MoveKeys = 8,
                Move = Move,
                // 뒤로 젖히며 웅크림 → 몸과 머리를 숙여 뿔을 앞세우고 오른쪽(바라보는 쪽)으로 짧게 돌진 → 복귀
                Attack = new[]
                {
                    (0f, P()),
                    (0.15f, Dash(move: new Vector2(-0.015f, -0.015f), lean: 6f, head: 4f, arms: 20f, legs: 4f)),
                    (0.3f, Dash(move: new Vector2(0.1f, -0.02f), lean: -14f, head: -10f, arms: -25f, legs: -12f)),
                    (0.4f, Dash(move: new Vector2(0.12f, -0.02f), lean: -16f, head: -12f, arms: -28f, legs: -14f)),
                    (0.55f, Dash(move: new Vector2(0.03f, 0f), lean: -3f, head: -2f, arms: -5f, legs: -3f)),
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
            var flight = new Vector2(70f * k, 30f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.4f)) - (hornRest.y - 18f) * k * k);
            Put(p, "Horn", hornRest + flight, -150f * k, hornRest);
            return p;
        }

        // ---------- 그림 준비 ----------

        // PSD 레이어 순서(아래 → 위): 오른팔(몸 뒤) → 몸통 → 왼팔 → 머리 → 뿔 → 십자
        private static List<LayerSpec> Layers(Art art)
        {
            LayerSpec L(string key, params string[] bones) =>
                new() { Name = "horned_" + key.ToLowerInvariant(), Pixels = art.Layers[key], PixelsSize = art.Size, Bones = bones };
            return new List<LayerSpec>
            {
                L("armR", "ArmR"),
                L("body", "Body", "LegL", "LegR"),
                L("armL", "ArmL"),
                L("head", "Head"),
                L("horn", "Horn"),
                L("cross", "Cross"),
            };
        }

        private static Art Prepare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var raw = tex.GetPixels32();
            int fw = tex.width, fh = tex.height;
            Object.DestroyImmediate(tex);
            float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            Color32 At(int x, int y) => raw[(fh - 1 - y) * fw + x]; // 좌상단 원점

            int w = Canvas.width, h = Canvas.height;
            var layers = new Dictionary<string, Color32[]>();
            foreach (var key in new[] { "head", "horn", "body", "armL", "armR", "cross" })
            {
                layers[key] = new Color32[w * h];
            }

            // 1) 파츠를 완성본 위치로 옮겨 담는다. 머리에서 원 밖 오른쪽 위 = 뿔.
            int Index(int x, int y) => (Canvas.yMax - 1 - y) * w + (x - Canvas.x);
            foreach (var (name, box, offset) in Sources)
            {
                for (int y = box.yMin; y < box.yMax; y++)
                {
                    for (int x = box.xMin; x < box.xMax; x++)
                    {
                        var c = At(x, y);
                        if (c.a == 0)
                        {
                            continue;
                        }

                        string key = name;
                        if (name == "head" && x > HeadCenter.x && y < HeadCenter.y - 15f &&
                            Vector2.Distance(new Vector2(x, y), HeadCenter) > HeadRadius - 1.5f)
                        {
                            key = "horn";
                        }

                        layers[key][Index(x + offset.x, y + offset.y)] = c;
                    }
                }
            }

            // 파츠 범위에 섞여 들어온 잡티 제거
            foreach (var px in layers.Values)
            {
                foreach (var comp in Components(px.Length, w, h, i => px[i].a > 0))
                {
                    if (comp.Count < 40)
                    {
                        foreach (int i in comp)
                        {
                            px[i] = default;
                        }
                    }
                }
            }

            // 2) 얼굴 십자:머리 원 안쪽(외곽선 제외)의 어두운 획. 떼어낸 자리는 얼굴 바탕색으로 메운다.
            var head = layers["head"];
            var hc = V(HeadCenter.x + 271, HeadCenter.y + 420);
            bool Inner(int i, float r) => Vector2.Distance(new Vector2(i % w, i / w), hc) < r;
            var cross = new bool[head.Length];
            foreach (var comp in Components(head.Length, w, h, i => head[i].a > 128 && Lum(head[i]) < 0.5f))
            {
                if (comp.Count >= 10 && comp.All(i => Inner(i, HeadRadius - 8f)))
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
                for (int i = 0; i < head.Length; i++)
                {
                    if (!cross[i])
                    {
                        continue;
                    }

                    foreach (int j in N4(i, w, h))
                    {
                        if (head[j].a > 128 && Lum(head[j]) < 0.92f && Inner(j, HeadRadius - 5f))
                        {
                            next[j] = true;
                        }
                    }
                }

                cross = next;
            }

            var cols = new int[w];
            var rows = new int[h];
            for (int i = 0; i < head.Length; i++)
            {
                if (cross[i]) { cols[i % w]++; rows[i / w]++; }
            }

            var center = new Vector2(System.Array.IndexOf(cols, cols.Max()), System.Array.IndexOf(rows, rows.Max()));

            float pr = 0, pg = 0, pb = 0;
            int pn = 0;
            for (int i = 0; i < head.Length; i++)
            {
                if (cross[i] || head[i].a < 250 || Lum(head[i]) < 0.88f || !Inner(i, HeadRadius * 0.7f))
                {
                    continue;
                }

                pr += head[i].r; pg += head[i].g; pb += head[i].b; pn++;
            }

            var paper = new Color32((byte)(pr / pn), (byte)(pg / pn), (byte)(pb / pn), 255);
            for (int i = 0; i < head.Length; i++)
            {
                if (cross[i]) { layers["cross"][i] = head[i]; head[i] = paper; }
            }

            return new Art { Size = new Vector2Int(w, h), Layers = layers, CrossCenter = center };
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

        /// <summary>파츠 조립 확인: 왼쪽 합친 그림, 오른쪽 레이어별 색 + 본 시작점</summary>
        public static string WritePreview(string path)
        {
            s_art = null;
            var art = s_art = Prepare();
            int w = art.Size.x, h = art.Size.y;
            var tex = new Texture2D(w * 2, h, TextureFormat.RGBA32, false);
            string[] order = { "armR", "body", "armL", "head", "horn", "cross" };
            for (int i = 0; i < w * h; i++)
            {
                Color merged = Color.gray, tint = Color.gray;
                for (int k = 0; k < order.Length; k++)
                {
                    var c = art.Layers[order[k]][i];
                    if (c.a > 0)
                    {
                        merged = Color.Lerp(merged, c, c.a / 255f);
                        tint = Color.Lerp(c, Color.HSVToRGB(k / 6f, 1f, 1f), 0.6f);
                    }
                }

                tex.SetPixel(i % w, i / w, merged);
                tex.SetPixel(w + i % w, i / w, tint);
            }

            foreach (var p in Pieces)
            {
                for (int d = -2; d <= 2; d++)
                {
                    tex.SetPixel(w + (int)p.A.x + d, (int)p.A.y, Color.black);
                    tex.SetPixel(w + (int)p.A.x, (int)p.A.y + d, Color.black);
                }
            }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return $"size={art.Size} crossCenter={art.CrossCenter}";
        }
    }
}
