using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// golem.png를 검은 외곽선 기준으로 돌 단위로 나눈다.
    /// 1) 부위(팔/다리/몸통/머리) 배정 → 2) 외곽선이 아닌 픽셀의 연결 덩어리 = 돌 (부위 경계에서 나눔)
    /// 3) 작은 조각은 이웃 돌에 합침, 머리는 한 덩어리 → 4) 외곽선 픽셀은 가장 가까운 돌로 번져 채움
    /// 메시는 돌마다 따로 (쿼드트리) 만들어 버텍스를 그 돌의 본에 100% 붙인다.
    /// </summary>
    public static class GolemStones
    {
        private const int MinStonePixels = 90;
        private const int HeadPart = 3;

        internal sealed class Stone
        {
            public int Id;
            public int Part;       // GolemRig.Parts 인덱스
            public string Parent;  // 부모 부위 본
            public Vector2 Center; // PNG 픽셀
            public int Pixels;
            public string Bone => "S" + Id;
        }

        internal sealed class Result
        {
            public int Width, Height;
            public Color32[] Image;
            public int[] Owner;  // 픽셀 → 돌 id (-1 투명)
            public List<Stone> Stones;
        }

        private static Result s_cache;

        internal static Result Compute()
        {
            if (s_cache != null)
            {
                return s_cache;
            }

            var (pixels, w, h) = GolemRig.LoadPng();
            var part = GolemRig.Assign(pixels, w, h);
            // 외곽선: 어둡거나(밝기 < 0.3) 채도 없는 회흑색(밝기 < 0.5). 갈색 면은 채도가 있어 제외된다.
            var r = Segment(pixels, w, h, part, GolemRig.Parts.Length, HeadPart, c =>
            {
                Color.RGBToHSV(c, out _, out float sat, out _);
                float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                return lum < 0.3f || (lum < 0.5f && sat < 0.15f);
            }, MinStonePixels);

            // 부위 본이 2개면 돌 중심에 가까운 쪽을 부모로
            foreach (var st in r.Stones)
            {
                st.Parent = GolemRig.Parts[st.Part].bones
                    .OrderBy(b =>
                    {
                        var bone = GolemRig.Bones.First(x => x.Name == b);
                        return GolemRig.Distance(st.Center, bone.Start, bone.End);
                    }).First();
            }

            return s_cache = r;
        }

        /// <summary>
        /// 외곽선(isLine)을 경계로 이어진 면을 조각으로 나눈다. part가 다르면 같은 면이어도 나눈다.
        /// mergePart 영역의 조각은 하나로 합치고(-1이면 안 함), minPixels보다 작은 조각은 이웃에 흡수.
        /// </summary>
        internal static Result Segment(Color32[] pixels, int w, int h, int[] part, int regionCount, int mergePart,
            System.Func<Color32, bool> isLine, int minPixels)
        {
            int n = pixels.Length;

            bool Opaque(int i) => pixels[i].a > 8 && part[i] >= 0;

            // 스케치 선이 끊긴 곳을 막기 위해 1px 두껍게 만든다.
            var rawLine = new bool[n];
            for (int i = 0; i < n; i++)
            {
                rawLine[i] = pixels[i].a < 128 || isLine(pixels[i]);
            }

            var line = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (!rawLine[i])
                {
                    continue;
                }

                line[i] = true;
                foreach (int j in Neighbors(i, w, h))
                {
                    line[j] = true;
                }
            }

            bool Line(int i) => line[i];

            // 2) 외곽선이 아닌 픽셀을 같은 부위끼리만 이어 덩어리로
            var owner = Enumerable.Repeat(-1, n).ToArray();
            var sizes = new List<int>();
            var partOf = new List<int>();
            var queue = new Queue<int>();
            for (int s = 0; s < n; s++)
            {
                if (!Opaque(s) || Line(s) || owner[s] >= 0)
                {
                    continue;
                }

                int id = sizes.Count, count = 0;
                owner[s] = id;
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    count++;
                    foreach (int j in Neighbors(i, w, h))
                    {
                        if (owner[j] < 0 && Opaque(j) && !Line(j) && part[j] == part[s])
                        {
                            owner[j] = id;
                            queue.Enqueue(j);
                        }
                    }
                }

                sizes.Add(count);
                partOf.Add(part[s]);
            }

            // 3) 머리는 하나로, 작은 조각은 비워 두었다가 이웃 돌로 채운다.
            int head = -1;
            var remap = new int[sizes.Count];
            for (int id = 0; id < sizes.Count; id++)
            {
                if (partOf[id] == mergePart)
                {
                    if (head < 0)
                    {
                        head = id;
                    }

                    remap[id] = head;
                }
                else
                {
                    remap[id] = sizes[id] < minPixels ? -1 : id;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (owner[i] >= 0)
                {
                    owner[i] = remap[owner[i]];
                }
            }

            // 4) 외곽선 / 작은 조각 / 반투명 픽셀은 가장 가까운 돌에서 번져 채운다. (같은 부위 우선)
            var frontier = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (owner[i] >= 0)
                {
                    frontier.Enqueue(i);
                }
            }

            while (frontier.Count > 0)
            {
                int i = frontier.Dequeue();
                foreach (int j in Neighbors(i, w, h))
                {
                    if (owner[j] < 0 && Opaque(j))
                    {
                        owner[j] = owner[i];
                        frontier.Enqueue(j);
                    }
                }
            }

            // id 정리 + 돌 정보
            var ids = owner.Where(o => o >= 0).Distinct().OrderBy(o => o).ToList();
            var dense = ids.Select((o, k) => (o, k)).ToDictionary(x => x.o, x => x.k);
            var sum = new Vector2[ids.Count];
            var count2 = new int[ids.Count];
            var partVotes = new int[ids.Count, regionCount];
            for (int i = 0; i < n; i++)
            {
                if (owner[i] < 0)
                {
                    continue;
                }

                int k = dense[owner[i]];
                owner[i] = k;
                sum[k] += new Vector2(i % w, i / w);
                count2[k]++;
                if (part[i] >= 0)
                {
                    partVotes[k, part[i]]++;
                }
            }

            var stones = new List<Stone>();
            for (int k = 0; k < ids.Count; k++)
            {
                int bestPart = 0;
                for (int p = 1; p < regionCount; p++)
                {
                    if (partVotes[k, p] > partVotes[k, bestPart])
                    {
                        bestPart = p;
                    }
                }

                stones.Add(new Stone { Id = k, Part = bestPart, Center = sum[k] / count2[k], Pixels = count2[k] });
            }

            return new Result { Width = w, Height = h, Image = pixels, Owner = owner, Stones = stones };
        }

        internal static void ClearCache() => s_cache = null;

        // ---------- 메시 ----------

        // 그리는 순서: 다리 → 몸통 → 머리 → 팔 (팔을 들면 몸 앞에 보이도록)
        private static readonly int[] DrawRank = { 0, 0, 1, 2, 3, 3 };

        /// <summary>돌마다 따로 쿼드트리 메시. 반환 좌표는 PNG 픽셀, 버텍스별 본 이름.</summary>
        internal static (Vector2[] vertices, int[] indices, string[] vertexBone) BuildMesh(Result r) =>
            BuildMesh(r.Owner, r.Width, r.Height, r.Stones.Count, id => r.Stones[id].Bone, id => DrawRank[r.Stones[id].Part]);

        /// <summary>owner(픽셀 → 조각 id)대로 조각마다 따로 쿼드트리 메시. 그리는 순서는 rank 오름차순.</summary>
        internal static (Vector2[] vertices, int[] indices, string[] vertexBone) BuildMesh(int[] ownerMap, int width, int height,
            int count, System.Func<int, string> boneOf, System.Func<int, int> rankOf, int minCell = 4)
        {
            var r = new { Owner = ownerMap, Width = width, Height = height };
            var verts = new List<Vector2>();
            var bones = new List<string>();
            var index = new Dictionary<(int stone, int x, int y), int>();
            var tris = Enumerable.Range(0, count).ToDictionary(i => i, _ => new List<int>());

            int V(int stone, int x, int y)
            {
                x = Mathf.Clamp(x, 0, r.Width);
                y = Mathf.Clamp(y, 0, r.Height);
                if (index.TryGetValue((stone, x, y), out int i))
                {
                    return i;
                }

                i = verts.Count;
                verts.Add(new Vector2(x, y));
                bones.Add(boneOf(stone));
                index[(stone, x, y)] = i;
                return i;
            }

            void Quad(int stone, int x, int y, int size)
            {
                int a = V(stone, x, y), b = V(stone, x + size, y), c = V(stone, x + size, y + size), d = V(stone, x, y + size);
                tris[stone].AddRange(new[] { a, b, c, a, c, d });
            }

            void Block(int x, int y, int size)
            {
                var present = new HashSet<int>();
                for (int yy = y; yy < Mathf.Min(y + size, r.Height); yy++)
                {
                    for (int xx = x; xx < Mathf.Min(x + size, r.Width); xx++)
                    {
                        int o = r.Owner[yy * r.Width + xx];
                        if (o >= 0)
                        {
                            present.Add(o);
                        }
                    }
                }

                if (present.Count == 0)
                {
                    return;
                }

                if (present.Count == 1 && size <= 32)
                {
                    Quad(present.First(), x, y, size);
                    return;
                }

                if (size > minCell)
                {
                    int half = size / 2;
                    Block(x, y, half);
                    Block(x + half, y, size - half);
                    Block(x, y + half, half);
                    Block(x + half, y + half, size - half);
                    return;
                }

                // 경계의 작은 칸은 걸친 돌 모두에 넣는다 (겹쳐서 틈이 안 생기게)
                foreach (int stone in present)
                {
                    Quad(stone, x, y, size);
                }
            }

            for (int y = 0; y < r.Height; y += 32)
            {
                for (int x = 0; x < r.Width; x += 32)
                {
                    Block(x, y, 32);
                }
            }

            var indices = Enumerable.Range(0, count)
                .OrderBy(rankOf)
                .SelectMany(i => tris[i])
                .ToArray();
            return (verts.ToArray(), indices, bones.ToArray());
        }

        private static IEnumerable<int> Neighbors(int i, int w, int h)
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

        /// <summary>돌 분리 결과를 돌마다 다른 색으로 칠한 디버그 이미지</summary>
        public static string WriteDebug(string path)
        {
            ClearCache();
            var r = Compute();
            var tex = new Texture2D(r.Width, r.Height, TextureFormat.RGBA32, false);
            var output = new Color[r.Image.Length];
            for (int i = 0; i < output.Length; i++)
            {
                if (r.Owner[i] < 0) { output[i] = Color.clear; continue; }
                var tint = Color.HSVToRGB((r.Owner[i] * 0.618f) % 1f, 0.8f, 1f);
                output[i] = Color.Lerp(r.Image[i], tint, 0.55f);
            }

            tex.SetPixels(output);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            var mesh = BuildMesh(r);
            return $"stones={r.Stones.Count} verts={mesh.vertices.Length} tris={mesh.indices.Length / 3} " +
                   "perPart=" + string.Join(",", r.Stones.GroupBy(s => s.Part).OrderBy(g => g.Key).Select(g => g.Count()));
        }
    }
}
