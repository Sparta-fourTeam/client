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
    /// 골렘: 돌을 묶은 파츠 15개(머리, 팔 위/아래/손, 다리 위/아래, 골반, 배, 가슴 2)에 본 하나씩.
    /// 모든 본이 루트이고 부모를 따라가는 동작은 FK/IK로 직접 계산한다. 그림은 한 장, 메시는 파츠 경계로 나뉜다.
    /// 좌표는 golem.png(322x317) 픽셀, 좌하단 원점.
    /// </summary>
    public static class GolemRig
    {
        private const string Png = "Assets/_Project/Sprites/golem.png";
        private const float Tau = Mathf.PI * 2f;

        // 파츠: 레이어 이름, 본들, 픽셀 배정용 두께(px)
        internal static readonly (string layer, string[] bones, float radius)[] Parts =
        {
            ("golem_leg_l", new[] { "LegL_U", "LegL_L" }, 25f),
            ("golem_leg_r", new[] { "LegR_U", "LegR_L" }, 25f),
            ("golem_torso", new[] { "Hip", "Spine" }, 50f),
            ("golem_head", new[] { "Head" }, 45f),
            ("golem_arm_l", new[] { "ArmL_U", "ArmL_L" }, 38f),
            ("golem_arm_r", new[] { "ArmR_U", "ArmR_L" }, 38f),
        };

        // 파츠마다 루트 본을 따로 둔다. (무너질 때 각자 떨어지도록)
        internal static readonly BoneSpec[] Bones =
        {
            Bone("Hip", null, 165, 105, 165, 160), Bone("Spine", "Hip", 165, 160, 160, 205),
            Bone("Head", null, 128, 262, 130, 305),
            Bone("ArmL_U", null, 62, 222, 42, 170), Bone("ArmL_L", "ArmL_U", 42, 170, 35, 95),
            Bone("ArmR_U", null, 240, 228, 275, 175), Bone("ArmR_L", "ArmR_U", 275, 175, 290, 80),
            Bone("LegL_U", null, 115, 100, 108, 55), Bone("LegL_L", "LegL_U", 108, 55, 100, 10),
            Bone("LegR_U", null, 205, 100, 210, 55), Bone("LegR_L", "LegR_U", 210, 55, 218, 10),
        };

        // ---------- 파츠 15개 (돌을 묶어서 만든다) ----------
        // 이름, 거친 영역(Parts 인덱스), 돌 묶기 기준 선분, 본(시작 = 회전 중심), 그리는 순서
        private static readonly (string name, int region, Vector2 g0, Vector2 g1, Vector2 b0, Vector2 b1, int rank)[] Pieces =
        {
            ("LegL_U", 0, V(115, 100), V(108, 60), V(115, 100), V(108, 55), 0),
            ("LegL_L", 0, V(108, 55), V(100, 10), V(108, 55), V(100, 10), 0),
            ("LegR_U", 1, V(205, 100), V(210, 60), V(205, 100), V(210, 55), 0),
            ("LegR_L", 1, V(210, 55), V(218, 10), V(210, 55), V(218, 10), 0),
            ("Pelvis", 2, V(140, 125), V(190, 125), V(165, 110), V(165, 140), 1),
            ("Belly", 2, V(165, 140), V(165, 170), V(165, 140), V(165, 170), 1),
            ("ChestL", 2, V(120, 200), V(145, 215), V(140, 175), V(120, 215), 1),
            ("ChestR", 2, V(185, 200), V(210, 215), V(190, 175), V(210, 215), 1),
            ("Head", 3, V(128, 262), V(130, 305), V(128, 262), V(130, 305), 2),
            ("ArmL_U", 4, V(62, 222), V(42, 170), V(62, 222), V(42, 170), 3),
            ("ArmL_L", 4, V(42, 170), V(38, 120), V(42, 170), V(38, 120), 3),
            ("HandL", 4, V(38, 120), V(35, 80), V(38, 120), V(35, 80), 3),
            ("ArmR_U", 5, V(240, 228), V(275, 175), V(240, 228), V(275, 175), 3),
            ("ArmR_L", 5, V(275, 175), V(285, 120), V(275, 175), V(285, 120), 3),
            ("HandR", 5, V(285, 120), V(290, 70), V(285, 120), V(290, 70), 3),
        };

        private const float Ppu = 200f;
        private static Vector2 V(float x, float y) => new(x, y);
        private static Vector2 Pivot(string piece) => Pieces.First(p => p.name == piece).b0;

        internal static MonsterRigs.Def Def()
        {
            const float length = 2.4f;
            const float stride = 0.035f, lift = 0.045f, bounce = 0.012f;

            // 모든 본이 루트라서, 부모를 따라가는 동작은 아래 FK 도우미로 직접 계산한다.
            Pose Move(float t)
            {
                float ph = t * Tau / length;
                float s = Mathf.Sin(ph);
                float body = bounce * s * s;
                var p = P();
                Torso(p, new Vector2(0f, body), -2f * s);
                p.B("Head", dx: -0.008f * s, dy: body, rot: -1.5f * s);
                Chain(p, new[] { "ArmL_U", "ArmL_L", "HandL" }, new Vector2(0f, body), 3f * s, 0f, 0f);
                Chain(p, new[] { "ArmR_U", "ArmR_L", "HandR" }, new Vector2(0f, body), 3f * s, 0f, 0f);
                // 두 다리를 반 주기씩 엇갈려: 들어 올리며 앞(화면 아래)으로 내딛고, 디딘 발은 뒤로 밀린다.
                foreach (var (side, phase) in new[] { ("L", ph), ("R", ph + Mathf.PI) })
                {
                    float up = Mathf.Max(0f, Mathf.Sin(phase));
                    Chain(p, new[] { $"Leg{side}_U", $"Leg{side}_L" }, new Vector2(0f, stride * Mathf.Cos(phase) + lift * up),
                        0f, (side == "L" ? -6f : 6f) * up);
                }

                return p;
            }

            return new MonsterRigs.Def
            {
                Name = "Golem",
                Size = new Vector2Int(322, 317),
                PivotPx = new Vector2(161, 0),
                CustomLayers = Layers,
                Bones = Pieces.Select(p => Bone(p.name, null, p.b0.x, p.b0.y, p.b1.x, p.b1.y)).ToArray(),
                MoveLength = length,
                MoveKeys = 8,
                Move = Move,
                // 살짝 웅크림 → 몸을 펴며 양팔을 몸 앞으로 휘둘러 머리 위로 → 무릎을 굽혀 주저앉으며 두 주먹으로 땅을 내려침(이펙트) → 복귀
                Attack = new[]
                {
                    (0f, P()),
                    (0.15f, Strike(crouch: 0.02f, armL: -10f, armR: 10f, bend: 0f)),
                    (0.38f, Strike(crouch: -0.012f, armL: 169f, armR: -187f, bend: 15f)),
                    (0.46f, Strike(crouch: -0.015f, armL: 175f, armR: -193f, bend: 18f)),
                    (0.55f, Slam(0.12f)),
                    (0.63f, Slam(0.11f)),
                    (0.9f, P()),
                },
                // 내려치는 순간의 충격파/먼지/부스러기: 클립 안 스프라이트라 Animation 창 미리보기에서도 보인다
                AttackExtra = clip => ClipFx.AddCurves(clip, SlamRoot, SlamTracks()),
                DieTracks = Collapse(),
                DieTint = new[] { (0f, 0f, 1f), (0.06f, 1f, 1f), (0.14f, 0f, 1f), (1.3f, 0f, 1f), (1.7f, 0f, 0f) },
                OnAttach = body => ClipFx.CreateObjects(body, SlamRoot, new Vector2(0f, 0.04f), SlamTracks()),
            };
        }

        // ---------- FK / IK 도우미 (모든 본은 루트, 변화량은 화면 축) ----------

        // 몸통 파츠는 골반 중심으로 같이 움직인다.
        private static void Torso(Pose p, Vector2 move, float rot)
        {
            var q = Quaternion.Euler(0f, 0f, rot);
            var pelvis = Pivot("Pelvis");
            foreach (var piece in new[] { "Pelvis", "Belly", "ChestL", "ChestR" })
            {
                var offset = Pivot(piece) - pelvis;
                var world = pelvis + move * Ppu + (Vector2)(q * offset);
                p.B(piece, dx: (world.x - Pivot(piece).x) / Ppu, dy: (world.y - Pivot(piece).y) / Ppu, rot: rot);
            }
        }

        // 사슬(위팔 → 아래팔 → 손 등): 각 관절 회전은 부모 기준, baseMove는 첫 관절 이동(유닛)
        private static void Chain(Pose p, string[] bones, Vector2 baseMove, params float[] rotations)
        {
            float angle = 0f;
            Vector2 joint = Pivot(bones[0]) + baseMove * Ppu;
            for (int i = 0; i < bones.Length; i++)
            {
                if (i > 0)
                {
                    joint += (Vector2)(Quaternion.Euler(0f, 0f, angle) * (Pivot(bones[i]) - Pivot(bones[i - 1])));
                }

                angle += i < rotations.Length ? rotations[i] : 0f;
                p.B(bones[i], dx: (joint.x - Pivot(bones[i]).x) / Ppu, dy: (joint.y - Pivot(bones[i]).y) / Ppu, rot: angle);
            }
        }

        // 2본 IK: 엉덩이를 drop만큼 내리고 발은 바닥에 고정, 무릎은 바깥으로 굽힌다.
        private static void Leg(Pose p, string side, float drop)
        {
            var upper = Pieces.First(x => x.name == $"Leg{side}_U");
            var lower = Pieces.First(x => x.name == $"Leg{side}_L");
            Vector2 hip0 = upper.b0, knee0 = lower.b0, foot = lower.b1;
            float a = Vector2.Distance(hip0, knee0), b = Vector2.Distance(knee0, foot);
            var hip = hip0 + new Vector2(0f, -drop * Ppu);
            var toFoot = foot - hip;
            float d = Mathf.Min(toFoot.magnitude, a + b - 0.01f);
            float bend = Mathf.Acos(Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f)) * Mathf.Rad2Deg;
            float baseAngle = Mathf.Atan2(toFoot.y, toFoot.x) * Mathf.Rad2Deg;
            float thigh = baseAngle + (side == "L" ? -bend : bend); // 무릎이 바깥으로
            var knee = hip + (Vector2)(Quaternion.Euler(0f, 0f, thigh) * Vector2.right * a);
            float shin = Angle(knee, foot);
            p.B(upper.name, dx: (hip.x - hip0.x) / Ppu, dy: (hip.y - hip0.y) / Ppu, rot: Mathf.DeltaAngle(Angle(hip0, knee0), thigh));
            p.B(lower.name, dx: (knee.x - knee0.x) / Ppu, dy: (knee.y - knee0.y) / Ppu, rot: Mathf.DeltaAngle(Angle(knee0, foot), shin));
        }

        private static float Angle(Vector2 a, Vector2 b) => Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

        // 팔을 휘두르는 자세: crouch = 골반 내림(유닛, 음수면 몸을 폄), 팔 각도는 휴식 대비, bend = 아래팔·손 굽힘
        private static Pose Strike(float crouch, float armL, float armR, float bend)
        {
            var p = P();
            var body = new Vector2(0f, -crouch);
            Torso(p, body, 0f);
            p.B("Head", dy: -crouch * 1.2f);
            var shoulders = body + new Vector2(0f, crouch < 0f ? 0.02f : 0f);
            Chain(p, new[] { "ArmL_U", "ArmL_L", "HandL" }, shoulders, armL, bend, bend * 0.6f);
            Chain(p, new[] { "ArmR_U", "ArmR_L", "HandR" }, shoulders, armR, -bend, -bend * 0.6f);
            Leg(p, "L", crouch);
            Leg(p, "R", crouch);
            return p;
        }

        // 내려치기: 무릎을 굽혀 깊게 주저앉고, 몸을 숙이며 곧게 편 팔이 앞 땅의 한 점을 향한다.
        private static Pose Slam(float crouch)
        {
            var p = P();
            const float lean = 0.03f;
            Torso(p, new Vector2(0f, -crouch - lean * 0.5f), 0f);
            p.B("Head", dy: -crouch - lean * 1.5f);
            var shoulders = new Vector2(0f, -crouch - lean);
            foreach (var (side, target) in new[] { ("L", V(140, 6)), ("R", V(182, 6)) })
            {
                string u = $"Arm{side}_U", l = $"Arm{side}_L", hand = $"Hand{side}";
                var tip = Pieces.First(x => x.name == hand).b1;
                var s0 = Pivot(u);
                var s1 = s0 + shoulders * Ppu;
                float aim = Mathf.DeltaAngle(Angle(s0, tip), Angle(s1, target));
                Chain(p, new[] { u, l, hand }, shoulders, aim, 0f, 0f);
            }

            Leg(p, "L", crouch);
            Leg(p, "R", crouch);
            return p;
        }

        // ---------- Die: 파츠마다 키 6개 ----------
        // 정지 → 금 가며 벌어짐 → 낙하 시작(속도 0) → 착지(실제 속도를 탄젠트로) → 튕김 → 정착
        private static Dictionary<string, (float t, Vector3 v, Vector3 slope)[]> Collapse()
        {
            const float crackStart = 0.12f, fallStart = 0.22f, gravity = 7f;
            var center = new Vector2(161f, 150f);
            var tracks = new Dictionary<string, (float t, Vector3 v, Vector3 slope)[]>();
            for (int i = 0; i < Pieces.Length; i++)
            {
                var piece = Pieces[i];
                var mid = (piece.b0 + piece.b1) * 0.5f;
                float h1 = Hash(i + 3), h2 = Hash(i + 41);
                float rest = 0.02f + 0.06f * h1;
                float drop = Mathf.Max(0.02f, piece.b0.y / Ppu * 0.92f - rest);
                float fall = Mathf.Sqrt(2f * drop / gravity);
                float vx = (mid.x - center.x) / Ppu * 0.6f + (h2 - 0.5f) * 0.3f;
                float spin = (h1 > 0.5f ? 1f : -1f) * (80f + 200f * h2);
                var crack = (Vector3)((mid - center).normalized * 0.008f);

                float land = fallStart + fall;
                var landed = crack + new Vector3(vx * fall, -drop, spin * fall);
                var landSlope = new Vector3(vx, -gravity * fall, spin);
                var bounce = landed + new Vector3(vx * 0.04f, 0.02f * Mathf.Min(1f, drop), spin * 0.01f);
                var settle = landed + new Vector3(vx * 0.06f, 0f, spin * 0.015f);
                tracks[piece.name] = new[]
                {
                    (0f, Vector3.zero, Vector3.zero),
                    (crackStart, Vector3.zero, Vector3.zero),
                    (fallStart, crack, Vector3.zero),
                    (land, landed, landSlope),
                    (land + 0.08f, bounce, Vector3.zero),
                    (land + 0.16f, settle, Vector3.zero),
                };
            }

            return tracks;
        }

        private static float Hash(int n)
        {
            float v = Mathf.Sin(n * 12.9898f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        // ---------- 땅 내려치기 이펙트 (Attack 클립 안에서 움직이는 스프라이트) ----------

        private const string SlamRoot = "SlamFx";
        private const float Impact = 0.55f;

        private static List<ClipFx.Track> SlamTracks()
        {
            var tracks = new List<ClipFx.Track>();
            const float T = Impact;

            // 충격파 링: 바닥에 납작하게 빠르게 퍼지며 사라짐
            var ringColor = new Color(0.85f, 0.8f, 0.7f, 0.9f);
            var ring0 = new Vector2(0.3f, 0.105f);
            tracks.Add(new ClipFx.Track
            {
                Name = "Ring",
                Sprite = ClipFx.Ring,
                Order = 10,
                Keys = new[]
                {
                    (0f, Vector2.zero, ring0, ClipFx.Alpha(ringColor, 0f)),
                    (T, Vector2.zero, ring0, ClipFx.Alpha(ringColor, 0f)),
                    (T + 0.01f, Vector2.zero, ring0, ringColor),
                    (T + 0.32f, Vector2.zero, new Vector2(2.2f, 0.77f), ClipFx.Alpha(ringColor, 0f)),
                },
            });

            var dust = new Color(0.82f, 0.76f, 0.66f, 0.9f);
            var rock = new Color(0.42f, 0.32f, 0.24f, 1f);
            for (int i = 0; i < 6; i++)
            {
                float side = i < 3 ? -1f : 1f;
                int k = i % 3;

                // 흙먼지: 옆·위로 퍼지며 커지고 옅어짐
                var start = new Vector2(side * 0.1f, -0.02f);
                var end = new Vector2(side * (0.7f + 0.25f * k), 0.04f + 0.12f * k);
                var s0 = Vector2.one * 0.3f;
                var s1 = Vector2.one * (0.7f + 0.15f * k);
                tracks.Add(new ClipFx.Track
                {
                    Name = "Dust" + i,
                    Sprite = ClipFx.Soft,
                    Order = 11,
                    Keys = new[]
                    {
                        (0f, start, s0, ClipFx.Alpha(dust, 0f)),
                        (T, start, s0, ClipFx.Alpha(dust, 0f)),
                        (T + 0.03f, Vector2.Lerp(start, end, 0.15f), Vector2.Lerp(s0, s1, 0.15f), dust),
                        (T + 0.35f, end, s1, ClipFx.Alpha(dust, 0f)),
                    },
                });

                // 돌 부스러기: 포물선으로 튀었다가 떨어짐
                float vx = side * (1.0f + 0.4f * k), vy = 1.5f + 0.4f * k;
                const float g = 6f;
                var size = Vector2.one * (0.11f + 0.025f * k);
                Vector2 At(float t) => new(vx * t, vy * t - 0.5f * g * t * t);
                tracks.Add(new ClipFx.Track
                {
                    Name = "Debris" + i,
                    Sprite = ClipFx.Soft,
                    Order = 12,
                    Keys = new[]
                    {
                        (0f, Vector2.zero, size, ClipFx.Alpha(rock, 0f)),
                        (T, Vector2.zero, size, ClipFx.Alpha(rock, 0f)),
                        (T + 0.01f, At(0.01f), size, rock),
                        (T + 0.1f, At(0.1f), size, rock),
                        (T + 0.2f, At(0.2f), size, rock),
                        (T + 0.3f, At(0.3f), size, ClipFx.Alpha(rock, 0f)),
                    },
                });
            }

            return tracks;
        }

        // ---------- 레이어 ----------

        // 그림 한 장 + 파츠별 메시. 돌(외곽선 기준)을 파츠로 묶으므로 경계가 돌 외곽선을 따른다.
        private static List<LayerSpec> Layers()
        {
            var owner = PieceOwner(out var r);
            var mesh = GolemStones.BuildMesh(owner, r.Width, r.Height, Pieces.Length, i => Pieces[i].name, i => Pieces[i].rank);
            return new List<LayerSpec>
            {
                new()
                {
                    Name = "golem",
                    Pixels = r.Image,
                    PixelsSize = new Vector2Int(r.Width, r.Height),
                    MeshVertices = mesh.vertices,
                    MeshIndices = mesh.indices,
                    MeshVertexBone = mesh.vertexBone,
                },
            };
        }

        // 돌마다 같은 거친 영역 안에서 가장 가까운 파츠 선분에 배정 → 픽셀 → 파츠 인덱스
        private static int[] PieceOwner(out GolemStones.Result r)
        {
            GolemStones.ClearCache();
            r = GolemStones.Compute();
            var stoneToPiece = r.Stones.Select(st =>
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int k = 0; k < Pieces.Length; k++)
                {
                    if (Pieces[k].region != st.Part)
                    {
                        continue;
                    }

                    float d = Distance(st.Center, Pieces[k].g0, Pieces[k].g1);
                    if (d < bestD) { bestD = d; best = k; }
                }

                return best;
            }).ToArray();
            return r.Owner.Select(o => o < 0 ? -1 : stoneToPiece[o]).ToArray();
        }

        /// <summary>파츠 분리 결과 디버그 이미지</summary>
        public static string WritePieceDebug(string path)
        {
            var owner = PieceOwner(out var r);
            var tex = new Texture2D(r.Width, r.Height, TextureFormat.RGBA32, false);
            var output = new Color[owner.Length];
            for (int i = 0; i < owner.Length; i++)
            {
                output[i] = owner[i] < 0 ? Color.clear : Color.Lerp(r.Image[i], Color.HSVToRGB((owner[i] * 0.618f) % 1f, 0.8f, 1f), 0.55f);
            }

            tex.SetPixels(output);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            var mesh = GolemStones.BuildMesh(owner, r.Width, r.Height, Pieces.Length, i => Pieces[i].name, i => Pieces[i].rank);
            return $"pieces={owner.Where(o => o >= 0).Distinct().Count()}/{Pieces.Length} verts={mesh.vertices.Length}";
        }

        // 각 픽셀을 (본 선분까지 거리 - 파츠 두께)가 가장 작은 파츠에 배정한다.
        internal static int[] Assign(Color32[] pixels, int w, int h)
        {
            var segments = Parts.Select(part => Bones.Where(b => part.bones.Contains(b.Name)).ToArray()).ToArray();
            var owner = new int[pixels.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    owner[i] = -1;
                    if (pixels[i].a <= 8)
                    {
                        continue;
                    }

                    var pt = new Vector2(x, y);
                    float best = float.MaxValue;
                    for (int k = 0; k < Parts.Length; k++)
                    {
                        float d = segments[k].Min(b => Distance(pt, b.Start, b.End)) - Parts[k].radius;
                        if (d < best)
                        {
                            best = d;
                            owner[i] = k;
                        }
                    }
                }
            }

            RemoveIslands(owner, w, h);
            return owner;
        }

        // 파츠마다 가장 큰 덩어리만 남기고, 떨어진 조각(외곽선 부스러기 등)은 맞닿은 다른 파츠로 넘긴다.
        private static void RemoveIslands(int[] owner, int w, int h)
        {
            var label = new int[owner.Length];
            var queue = new Queue<int>();
            var orphans = new List<int>();
            for (int k = 0; k < Parts.Length; k++)
            {
                System.Array.Clear(label, 0, label.Length);
                var components = new List<List<int>>();
                for (int start = 0; start < owner.Length; start++)
                {
                    if (owner[start] != k || label[start] != 0)
                    {
                        continue;
                    }

                    var comp = new List<int>();
                    label[start] = 1;
                    queue.Enqueue(start);
                    while (queue.Count > 0)
                    {
                        int i = queue.Dequeue();
                        comp.Add(i);
                        foreach (int n in Neighbors(i, w, h))
                        {
                            if (owner[n] == k && label[n] == 0) { label[n] = 1; queue.Enqueue(n); }
                        }
                    }

                    components.Add(comp);
                }

                foreach (var comp in components.OrderByDescending(c => c.Count).Skip(1))
                {
                    foreach (int i in comp)
                    {
                        owner[i] = -2; // 재배정 대기
                        orphans.Add(i);
                    }
                }
            }

            // 맞닿은 파츠에서 번져 들어가며 채운다.
            bool changed = true;
            while (changed && orphans.Count > 0)
            {
                changed = false;
                foreach (int i in orphans)
                {
                    if (owner[i] != -2)
                    {
                        continue;
                    }

                    foreach (int n in Neighbors(i, w, h))
                    {
                        if (owner[n] >= 0) { owner[i] = owner[n]; changed = true; break; }
                    }
                }
            }
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

        internal static float Distance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        internal static (Color32[] pixels, int w, int h) LoadPng()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var result = (tex.GetPixels32(), tex.width, tex.height);
            Object.DestroyImmediate(tex);
            return result;
        }

    }
}
