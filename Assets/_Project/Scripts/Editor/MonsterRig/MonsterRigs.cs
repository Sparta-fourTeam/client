using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static Game.Editor.MonsterRig.MonsterRigBuilder;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 슬라임 외 몬스터들의 PSD 리그 + Move / Attack / Die 본 애니메이션을 데이터로 정의하고 한 번에 만든다.
    /// 좌표는 모두 원본 PNG 픽셀(좌하단 원점) 기준. 움직임은 타입별 템플릿(걷기 / 부유 / 박쥐 / 스켈레톤)을 공유한다.
    /// </summary>
    public static class MonsterRigs
    {
        private const string ArtFolder = "Assets/_Project/Art/Characters/Monsters";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies/Monsters";
        private const int Pad = 8;
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color HitTint = new(1f, 0.35f, 0.35f, 1f);

        public static string[] Names => Defs().Select(d => d.Name).ToArray();

        public static string BuildAll()
        {
            var log = new List<string>();
            foreach (var def in Defs())
            {
                log.Add(Build(def));
            }

            AssetDatabase.SaveAssets();
            return string.Join("\n", log);
        }

        public static string Build(string name) => Build(Defs().First(d => d.Name == name));

        // ---------- 정의 ----------

        internal sealed class Def
        {
            public string Name;
            public Vector2Int Size;           // PNG 크기 (모든 레이어 공통 캔버스)
            public Vector2 PivotPx;           // 기존 스프라이트 피벗 (PNG 픽셀)
            public (string png, string[] bones)[] Layers;
            public Func<List<LayerSpec>> CustomLayers; // PNG를 파츠별로 잘라 쓰는 경우
            public int? GridStep;
            public BoneSpec[] Bones;          // PNG 픽셀 좌표
            public float MoveLength;
            public int MoveKeys;
            public Func<float, Pose> Move;
            public (float t, Pose pose)[] Attack;
            public (float t, Pose pose)[] Die;
            public Func<float, Pose> DieFn;               // 키 대신 시간 함수로 (돌마다 타이밍이 다른 경우)
            // 본마다 따로 찍는 키: (시간, 변화량(dx, dy, 회전), 기울기). 루트 본 전용.
            public Dictionary<string, (float t, Vector3 v, Vector3 slope)[]> DieTracks;
            public (float t, float red, float alpha)[] DieTint;
            public float DieLength;
            public int DieKeys;
            public (float time, string function)[] AttackEvents;
            public Action<AnimationClip> AttackExtra;     // Attack 클립에 추가 커브 (이펙트 오브젝트 등)
            public Action<AnimationClip> DieExtra;        // Die 클립에 추가 커브 (렌더러별 투명도 등)
            public Action<GameObject> OnAttach;           // Animator가 붙는 Body에 추가 설정
        }

        /// <summary>본별 변화량(부모 기준 이동은 화면 축, 회전은 도) + 색.</summary>
        internal sealed class Pose
        {
            public readonly Dictionary<string, (Vector2 move, float rot)> Bones = new();
            public float Red;     // 피격 틴트 0..1
            public float Alpha = 1f;
            public Color? Paint;  // 지정하면 틴트 대신 이 색(알파 포함)

            public Pose B(string bone, float dx = 0f, float dy = 0f, float rot = 0f)
            {
                Bones[bone] = (new Vector2(dx, dy), rot);
                return this;
            }

            // 다른 포즈의 본 변화량을 덮어쓴다 (다른 본끼리 합치기)
            public Pose Merge(Pose other)
            {
                foreach (var kv in other.Bones)
                {
                    Bones[kv.Key] = kv.Value;
                }

                return this;
            }

            public Pose Colored(Color color)
            {
                Paint = color;
                return this;
            }

            public Pose Tint(float red, float alpha = 1f)
            {
                Red = red;
                Alpha = alpha;
                return this;
            }
        }

        internal static BoneSpec Bone(string name, string parent, float x0, float y0, float x1, float y1) =>
            new() { Name = name, Parent = parent, Start = new Vector2(x0, y0), End = new Vector2(x1, y1) };

        internal static Pose P() => new();

        private static IEnumerable<Def> Defs()
        {
            // --- 걷기 ---
            yield return HornedRig.Def();
            yield return SpiderRig.Def();
            yield return CrabRig.Def();
            if (false)
            {
                yield return Walker("ArmoredCrab", 237, 234, 119, 0, heavy: false,
                new[]
                {
                    Bone("Root", null, 119, 10, 119, 95), Bone("Body", "Root", 119, 95, 124, 225),
                    Bone("LegL", null, 80, 40, 55, 8), Bone("LegR", null, 165, 40, 185, 8),
                    Bone("ArmL", "Body", 75, 110, 4, 100), Bone("ArmR", "Body", 165, 100, 233, 82),
                });
            }

            yield return GolemRig.Def();

            // --- 부유 --- (appendage: 이름, 좌우 부호)
            yield return GhostRig.Def();
            if (false)
            {
                yield return Floater("Ghost", "ghost", 213, 239, 107, 120,
                new[]
                {
                    Bone("Root", null, 107, 40, 107, 110), Bone("Upper", "Root", 107, 110, 96, 228),
                    Bone("ArmL", "Root", 75, 112, 4, 108), Bone("ArmR", "Root", 140, 112, 206, 118), Bone("Tail", "Root", 112, 40, 138, 10),
                }, ("ArmL", 1f), ("ArmR", -1f), ("Tail", 1f));
            }

            yield return EyeJellyRig.Def();
            if (false)
            {
                yield return Floater("EyeJelly", "eyeball", 206, 221, 103, 111,
                new[]
                {
                    Bone("Root", null, 103, 60, 103, 120), Bone("Upper", "Root", 103, 120, 115, 205),
                    Bone("TentL", "Root", 75, 62, 45, 25), Bone("TentM", "Root", 103, 58, 100, 12), Bone("TentR", "Root", 128, 60, 132, 6),
                }, ("TentL", 1f), ("TentM", -1f), ("TentR", 1f));
            }

            yield return BrainSpiderRig.Def();
            if (false)
            {
                yield return Floater("BrainSpider", "brain", 329, 300, 165, 150,
                new[]
                {
                    Bone("Root", null, 175, 90, 175, 165), Bone("Upper", "Root", 175, 165, 200, 275),
                    Bone("LegL", "Root", 125, 85, 95, 24), Bone("LegR", "Root", 250, 85, 282, 24),
                    Bone("LegLU", "Root", 105, 175, 8, 222), Bone("LegRU", "Root", 260, 170, 322, 195),
                }, ("LegL", 1f), ("LegR", -1f), ("LegLU", 1f), ("LegRU", -1f));
            }

            yield return Bat();
            yield return Skeleton();
        }

        // 걷기: 다리는 루트 밖(독립 루트 본)에 두어 바닥에 붙어 있고, 몸은 한 걸음마다 살짝 튀며 좌우로 기운다.
        private static Def Walker(string name, int w, int h, float pivotX, float pivotY, bool heavy, BoneSpec[] bones)
        {
            bool hasHead = bones.Any(b => b.Name == "Head");
            float length = heavy ? 2.8f : 1.4f;
            float bounce = heavy ? 0.012f : 0.016f;
            float lean = heavy ? 2.5f : 1.8f;

            Pose Move(float t)
            {
                float ph = t * Tau / length;
                float s = Mathf.Sin(ph);
                var p = P()
                    .B("Root", dy: bounce * s * s, rot: lean * s)
                    .B("Body", rot: -0.6f * lean * Mathf.Sin(ph - 0.5f))
                    .B("LegL", dy: 0.012f * Mathf.Pow(Mathf.Max(0f, s), 2f))
                    .B("LegR", dy: 0.012f * Mathf.Pow(Mathf.Max(0f, -s), 2f))
                    .B("ArmL", rot: 4f * Mathf.Cos(ph))
                    .B("ArmR", rot: 4f * Mathf.Cos(ph));
                if (hasHead)
                {
                    p.B("Head", rot: 1.5f * Mathf.Sin(ph - 1f));
                }

                return p;
            }

            Pose Lean(float root, float body, float arms, float dy = 0f)
            {
                var p = P().B("Root", dy: dy, rot: root).B("Body", rot: body).B("ArmL", rot: arms).B("ArmR", rot: -arms);
                if (hasHead)
                {
                    p.B("Head", rot: body * 1.2f);
                }

                return p;
            }

            return new Def
            {
                Name = name,
                Size = new Vector2Int(w, h),
                PivotPx = new Vector2(pivotX, pivotY),
                Layers = new[] { (PngFor(name), (string[])null) },
                Bones = bones,
                MoveLength = length,
                MoveKeys = 12,
                Move = Move,
                // 뒤로 젖혔다가 → 앞으로 내려치고 → 작게 되돌아옴
                Attack = new[]
                {
                    (0f, P()),
                    (0.18f, Lean(-6f, -3f, 18f, 0.01f)),
                    (0.32f, Lean(8f, 4f, -22f, -0.01f)),
                    (0.45f, Lean(-1.5f, -1f, 4f)),
                    (0.6f, P()),
                },
                // 움찔 → 옆으로 쓰러지며 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.06f, Lean(-5f, 0f, 0f).Tint(1f)),
                    (0.2f, Lean(-8f, 0f, 5f)),
                    (0.45f, Lean(30f, 4f, 15f)),
                    (0.9f, Lean(75f, 10f, 30f, -0.02f).B("LegL", rot: 40f).B("LegR", rot: 40f).Tint(0f, 0f)),
                },
            };
        }

        // 부유: 천천히 위아래로 떠다니며 기울고, 팔/촉수는 반 박자 늦게 흔들린다.
        private static Def Floater(string name, string png, int w, int h, float pivotX, float pivotY, BoneSpec[] bones,
            params (string bone, float sign)[] limbs)
        {
            const float length = 3f;

            Pose Move(float t)
            {
                float ph = t * Tau / length;
                var p = P()
                    .B("Root", dy: 0.04f * Mathf.Sin(ph), rot: 2f * Mathf.Sin(ph + 1.2f))
                    .B("Upper", rot: -1.5f * Mathf.Sin(ph - 0.6f));
                for (int i = 0; i < limbs.Length; i++)
                {
                    p.B(limbs[i].bone, rot: limbs[i].sign * 7f * Mathf.Sin(2f * ph - 0.8f - 0.7f * i));
                }

                return p;
            }

            Pose Body(float dy, float rot, float upper, float limb)
            {
                var p = P().B("Root", dy: dy, rot: rot).B("Upper", rot: upper);
                foreach (var (bone, sign) in limbs)
                {
                    p.B(bone, rot: sign * limb);
                }

                return p;
            }

            return new Def
            {
                Name = name,
                Size = new Vector2Int(w, h),
                PivotPx = new Vector2(pivotX, pivotY),
                Layers = new[] { (png, (string[])null) },
                Bones = bones,
                MoveLength = length,
                MoveKeys = 12,
                Move = Move,
                // 위로 움츠렸다가 → 아래로 덮치고 → 복귀
                Attack = new[]
                {
                    (0f, P()),
                    (0.18f, Body(0.03f, -5f, -2f, 15f)),
                    (0.32f, Body(-0.05f, 6f, 4f, -20f)),
                    (0.45f, Body(-0.01f, -1f, 0f, 3f)),
                    (0.6f, P()),
                },
                // 움찔 → 위로 떠오르며 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.06f, Body(0f, -4f, 0f, 0f).Tint(1f)),
                    (0.2f, Body(-0.02f, -4f, 0f, 5f)),
                    (0.4f, Body(0.03f, 6f, -3f, 15f)),
                    (0.9f, Body(0.15f, 18f, -8f, 25f).Tint(0f, 0f)),
                },
            };
        }

        // 박쥐: 몸 + 날개 2장(레이어). 날개는 본 2개씩이라 끝이 한 박자 늦게 접혔다 펴진다.
        private static Def Bat()
        {
            const float length = 1.25f; // 날갯짓 4회 + 천천히 위아래 1회

            Pose Wings(float inner, float outer) =>
                P().B("WingL1", rot: inner).B("WingL2", rot: outer).B("WingR1", rot: -inner).B("WingR2", rot: -outer);

            // 파츠는 bat_parts.png(날개 2 + 몸통)를 조립해서 쓴다.
            var art = BatRig.Assemble();
            return new Def
            {
                Name = "Bat",
                Size = art.Size,
                PivotPx = art.Pivot,
                CustomLayers = () => art.Layers,
                GridStep = 10,
                Bones = art.Bones,
                // 내리꽂으며 초음파: 클립 안의 링 스프라이트로 그려서 Animation 창 미리보기에서도 보인다
                AttackExtra = BatRig.AddSonicCurves,
                OnAttach = body => BatRig.CreateSonicObjects(body, art),
                MoveLength = length,
                MoveKeys = 32,
                Move = t =>
                {
                    float flap = t * Tau * 4f / length;
                    float bob = t * Tau / length;
                    return Wings(16f * Mathf.Sin(flap), 9f * Mathf.Sin(flap - 0.8f))
                        .B("Body", dy: 0.015f * Mathf.Sin(flap + 0.6f) + 0.02f * Mathf.Sin(bob));
                },
                // 날개를 들어 올리며 떠올랐다가 → 날개를 젖히며 내리꽂음
                Attack = new[]
                {
                    (0f, P()),
                    (0.15f, Wings(-30f, -15f).B("Body", dy: 0.04f)),
                    (0.3f, Wings(25f, 12f).B("Body", dy: -0.06f)),
                    (0.5f, P()),
                },
                // 날개를 접고 떨어지며 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.06f, Wings(-10f, 0f).Tint(1f)),
                    (0.25f, Wings(50f, 30f).B("Body", rot: 10f)),
                    (0.9f, Wings(55f, 35f).B("Body", dy: -0.25f, rot: 35f).Tint(0f, 0f)),
                },
            };
        }

        // 스켈레톤: 뼈 조각 11개(레이어)가 각자 본 하나씩. 떠 있는 뼈들이 제각각 살짝 흔들린다.
        private static Def Skeleton()
        {
            // 원래 MonsterAnimator 파츠 값: 피벗(본 공간 유닛), 회전 진폭/주파수, 이동 진폭, 위상
            var parts = new (float px, float py, float rot, float rf, float mx, float my, float mf, float phase)[]
            {
                (0.05f, 0.2f, 4, 0.35f, 0.01f, 0.02f, 0.3f, 0f),
                (0.335f, 0.15f, 6, 0.4f, 0.02f, 0.05f, 0.34f, 0.9f),
                (-0.335f, 0.11f, 6, 0.45f, 0.02f, 0.05f, 0.38f, 1.8f),
                (0.19f, 0.07f, 6, 0.5f, 0.02f, 0.05f, 0.42f, 2.7f),
                (-0.18f, 0.04f, 6, 0.55f, 0.02f, 0.05f, 0.46f, 3.6f),
                (0.015f, 0.01f, 6, 0.6f, 0.02f, 0.05f, 0.5f, 4.5f),
                (0.015f, -0.17f, 6, 0.65f, 0.02f, 0.05f, 0.54f, 5.4f),
                (0.12f, -0.275f, 6, 0.7f, 0.02f, 0.05f, 0.58f, 6.3f),
                (-0.12f, -0.33f, 6, 0.75f, 0.02f, 0.05f, 0.62f, 7.2f),
                (0.195f, -0.42f, 6, 0.8f, 0.02f, 0.05f, 0.66f, 8.1f),
                (-0.145f, -0.49f, 6, 0.85f, 0.02f, 0.05f, 0.7f, 9f),
            };
            const float walk = 1.2f;
            const float ppu = 200f;
            Vector2 Px(int i) => new(90f + parts[i].px * ppu, 120f + parts[i].py * ppu);
            string B(int i) => "Bone" + i;

            var bones = new List<BoneSpec> { Bone("Root", null, 90, 4, 90, 24) };
            for (int i = 0; i < parts.Length; i++)
            {
                var c = Px(i);
                bones.Add(Bone(B(i), "Root", c.x, c.y, c.x, c.y + 16f));
            }

            // 조각 번호: 0 해골, 1/2 오른·왼팔, 3/4 오른·왼어깨, 5 갈비뼈, 6 골반, 7/8 오른·왼무릎, 9/10 오른·왼정강이
            // 관절(PNG 픽셀): 허리, 목, 어깨, 골반, 다리가 매달린 무릎 구슬 윗부분
            var waist = new Vector2(94, 100);
            var neck = new Vector2(100, 150);
            var shoulderR = new Vector2(128, 132);
            var shoulderL = new Vector2(55, 127);
            var hipJoint = new Vector2(94, 85);
            var legR = new Vector2(115, 70);
            var legL = new Vector2(67, 60);
            Vector2 Turn(Vector2 pt, Vector2 about, float deg) => about + (Vector2)(Quaternion.Euler(0f, 0f, deg) * (pt - about));

            // 상체는 허리를 중심으로 ribs만큼 돌고, 각 조각은 자기 관절을 중심으로 더 돈다. 하체는 골반 기준.
            // floaty: 조각마다 살짝 다르게 떠 있는 양(유닛) / 회전(도)
            Pose Body(Vector2 move, float ribs, float skull, float armR, float armL, float pelvis, float swingR, float swingL,
                float liftR = 0f, float liftL = 0f, System.Func<int, (float dy, float rot)> floaty = null)
            {
                var p = P();
                void Put(int i, Vector2 joint, float own, Vector2 groupJoint, float group, Vector2 offset)
                {
                    var f = floaty?.Invoke(i) ?? (0f, 0f);
                    var world = Turn(Turn(Px(i), joint, own), groupJoint, group) + offset * ppu + new Vector2(0f, f.dy * ppu);
                    var d = (world - Px(i)) / ppu;
                    p.B(B(i), dx: d.x, dy: d.y, rot: own + group + f.rot);
                }

                Put(5, waist, 0f, waist, ribs, move);
                Put(0, neck, skull, waist, ribs, move);
                Put(3, shoulderR, 0f, waist, ribs, move);
                Put(1, shoulderR, armR, waist, ribs, move);
                Put(4, shoulderL, 0f, waist, ribs, move);
                Put(2, shoulderL, armL, waist, ribs, move);
                var hips = move * 0.4f; // 다리는 몸을 반쯤만 따라감 (발이 바닥 쪽)
                Put(6, hipJoint, 0f, hipJoint, pelvis, hips);
                Put(7, legR, 0f, hipJoint, pelvis, hips + new Vector2(0f, liftR));
                Put(9, legR, swingR, hipJoint, pelvis, hips + new Vector2(0f, liftR));
                Put(8, legL, 0f, hipJoint, pelvis, hips + new Vector2(0f, liftL));
                Put(10, legL, swingL, hipJoint, pelvis, hips + new Vector2(0f, liftL));
                return p;
            }

            Pose Fall(float amount, float alpha)
            {
                var p = P().Tint(0f, alpha);
                for (int i = 0; i < parts.Length; i++)
                {
                    float sign = i % 2 == 0 ? 1f : -1f;
                    float floor = (Px(i).y - 10f) / ppu;
                    p.B(B(i), dx: sign * 0.01f * (i % 3) * amount, dy: -floor * 0.9f * amount, rot: sign * (25f + 7f * i) * amount);
                }

                return p;
            }

            return new Def
            {
                Name = "Skeleton",
                Size = new Vector2Int(180, 240),
                PivotPx = new Vector2(90, 120),
                Layers = Enumerable.Range(0, parts.Length).Select(i => ("skeleton_" + i, new[] { B(i) })).ToArray(),
                Bones = bones.ToArray(),
                MoveLength = walk,
                MoveKeys = 8,
                // 조각들은 각자 살짝 떠 있되, 팔과 다리를 엇갈려 흔들며 걷는다 (왼팔 ↔ 오른다리)
                Move = t =>
                {
                    float ph = t * Tau / walk;
                    float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
                    return Body(new Vector2(0f, 0.012f * (1f - Mathf.Cos(2f * ph)) * 0.5f),
                        ribs: 2.5f * s, skull: -2f * s, armR: 12f * s, armL: 12f * s, pelvis: -2f * s,
                        swingR: -16f * s, swingL: 16f * s,
                        liftR: 0.012f * Mathf.Max(0f, -c), liftL: 0.012f * Mathf.Max(0f, c),
                        floaty: i => (0.008f * Mathf.Sin(2f * ph + i * 0.7f), 2f * Mathf.Sin(ph + i)));
                },
                // 오른팔 들어 올림 → 오른팔 휘두름 → 왼팔 들어 올림 → 왼팔 휘두름 → 복귀. 몸통과 다리가 휘두르는 쪽으로 따라 비튼다.
                Attack = new[]
                {
                    (0f, P()),
                    (0.12f, Body(new Vector2(0.01f, 0.01f), ribs: 8f, skull: 4f, armR: 55f, armL: -10f, pelvis: -3f, swingR: -6f, swingL: 8f)),
                    (0.24f, Body(new Vector2(-0.02f, -0.015f), ribs: -10f, skull: -6f, armR: -75f, armL: 10f, pelvis: 4f, swingR: 10f, swingL: -8f, liftL: 0.01f)),
                    (0.38f, Body(new Vector2(-0.01f, 0.01f), ribs: -8f, skull: -4f, armR: -25f, armL: -55f, pelvis: 3f, swingR: 8f, swingL: -6f)),
                    (0.5f, Body(new Vector2(0.02f, -0.015f), ribs: 10f, skull: 6f, armR: 5f, armL: 75f, pelvis: -4f, swingR: -10f, swingL: 10f, liftR: 0.01f)),
                    (0.64f, Body(Vector2.zero, ribs: 2f, skull: 1f, armR: 0f, armL: 10f, pelvis: -1f, swingR: -2f, swingL: 2f)),
                    (0.85f, P()),
                },
                // 뼈가 우르르 바닥으로 무너지며 사라짐
                Die = new[]
                {
                    (0f, P()),
                    (0.06f, P().Tint(1f)),
                    (0.25f, Fall(-0.05f, 1f)),
                    (0.6f, Fall(0.85f, 1f)),
                    (1f, Fall(1f, 0f)),
                },
            };
        }

        private static string PngFor(string name) => name switch
        {
            "Horned" => "horn",
            "ArmoredCrab" => "crab",
            _ => name.ToLowerInvariant(),
        };

        // ---------- 빌드 ----------

        private static string Build(Def def)
        {
            string psdPath = $"{ArtFolder}/Rig/{def.Name}/{def.Name}.psd";
            MonsterAnimationBaker.EnsureFolder($"{ArtFolder}/Rig/{def.Name}");
            int maxSide = Mathf.Max(def.Size.x, def.Size.y);

            var spec = new RigSpec
            {
                PsdPath = psdPath,
                CanvasSize = def.Size + Vector2Int.one * (Pad * 2),
                PixelsPerUnit = 200f,
                Pivot = new Vector2((def.PivotPx.x + Pad) / (def.Size.x + Pad * 2f), (def.PivotPx.y + Pad) / (def.Size.y + Pad * 2f)),
                GridStep = def.GridStep ?? Mathf.Clamp(maxSide / 14, 8, 32),
                WeightFalloff = 2.5f,
                MaxInfluences = 3,
                Layers = def.CustomLayers != null
                    ? def.CustomLayers().Select(l => { l.CanvasOffset = new Vector2Int(Pad, Pad); return l; }).ToList()
                    : def.Layers.Select(l => new LayerSpec
                    {
                        Name = l.png,
                        PngPath = $"{ArtFolder}/Source/{l.png}.png",
                        CanvasOffset = new Vector2Int(Pad, Pad),
                        Bones = l.bones,
                    }).ToList(),
                Bones = def.Bones.Select(b => new BoneSpec
                {
                    Name = b.Name,
                    Parent = b.Parent,
                    Start = b.Start + Vector2.one * Pad,
                    End = b.End + Vector2.one * Pad,
                }).ToList(),
            };

            var rig = MonsterRigBuilder.Build(spec);
            var channels = Channels(rig.transform, def.Bones.Select(b => b.Name));

            string folder = $"{ArtFolder}/Animations/{def.Name}";
            MonsterAnimationBaker.EnsureFolder(folder);
            foreach (var stale in new[] { "Idle", "Hit" })
            {
                AssetDatabase.DeleteAsset($"{folder}/{def.Name}_{stale}.anim");
            }

            var move = new ClipBuilder(MonsterAnimationBaker.LoadOrCreateClip($"{folder}/{def.Name}_Move.anim"), loop: true);
            var samples = Enumerable.Range(0, 48).Select(i => def.Move(def.MoveLength * i / 48f)).ToArray();
            foreach (var c in Animated(channels, samples))
            {
                move.Sparse(c.Path, c.Type, c.Property, def.MoveLength, t => c.Value(def.Move(t)), def.MoveKeys);
            }

            move.Finish();

            var attack = Keyed($"{folder}/{def.Name}_Attack.anim", channels, def.Attack);
            def.AttackExtra?.Invoke(attack);
            if (def.AttackEvents != null)
            {
                AnimationUtility.SetAnimationEvents(attack, def.AttackEvents
                    .Select(e => new AnimationEvent { time = e.time, functionName = e.function }).ToArray());
            }

            AnimationClip die;
            if (def.DieTracks != null)
            {
                var b = new ClipBuilder(MonsterAnimationBaker.LoadOrCreateClip($"{folder}/{def.Name}_Die.anim"), loop: false);
                foreach (var (bone, keys) in def.DieTracks)
                {
                    Pose At(Vector3 v) => P().B(bone, dx: v.x, dy: v.y, rot: v.z);
                    var rest = P();
                    foreach (var c in channels.Where(c => c.Type == typeof(Transform)))
                    {
                        if (Mathf.Abs(c.Value(At(Vector3.one)) - c.Value(rest)) < 1e-6f)
                        {
                            continue; // 이 본의 변하는 성분만
                        }

                        b.Explicit(c.Path, c.Type, c.Property, keys.Select(k =>
                            (k.t, c.Value(At(k.v)), c.Value(At(k.v + k.slope)) - c.Value(At(k.v)))).ToArray());
                    }
                }

                var tintPoses = def.DieTint.Select(k => P().Tint(k.red, k.alpha)).ToArray();
                foreach (var c in Animated(channels.Where(c => c.Type == typeof(SpriteRenderer)).ToList(), tintPoses))
                {
                    b.Keyed(c.Path, c.Type, c.Property, def.DieTint.Select((k, i) => (k.t, c.Value(tintPoses[i]))).ToArray());
                }

                b.Finish();
                die = b.Clip;
            }
            else if (def.DieFn != null)
            {
                var b = new ClipBuilder(MonsterAnimationBaker.LoadOrCreateClip($"{folder}/{def.Name}_Die.anim"), loop: false);
                var dieSamples = Enumerable.Range(0, 64).Select(i => def.DieFn(def.DieLength * i / 63f)).ToArray();
                foreach (var c in Animated(channels, dieSamples))
                {
                    b.Sparse(c.Path, c.Type, c.Property, def.DieLength, t => c.Value(def.DieFn(t)), def.DieKeys, loop: false);
                }

                b.Finish();
                die = b.Clip;
            }
            else
            {
                die = Keyed($"{folder}/{def.Name}_Die.anim", channels, def.Die);
            }

            def.DieExtra?.Invoke(die);
            var controller = CreateController($"{folder}/{def.Name}.controller", move.Clip, attack, die);
            AttachToPrefab($"{PrefabFolder}/{def.Name}_Animated.prefab", rig, controller, def.OnAttach);

            var sprites = rig.GetComponentsInChildren<SpriteRenderer>();
            int verts = sprites.Sum(r => UnityEngine.U2D.SpriteDataAccessExtensions.GetVertexCount(r.sprite));
            return $"{def.Name}: layers={sprites.Length} bones={def.Bones.Length} verts={verts} " +
                   $"curves move/attack/die={Curves(move.Clip)}/{Curves(attack)}/{Curves(die)}";
        }

        private static string Curves(AnimationClip c)
        {
            var b = AnimationUtility.GetCurveBindings(c);
            return $"{b.Length}c{b.Sum(x => AnimationUtility.GetEditorCurve(c, x).length)}k";
        }

        internal sealed class Channel
        {
            public string Path;
            public Type Type;
            public string Property;
            public Func<Pose, float> Value;
        }

        // 본: 휴식값 + 변화량(이동은 부모의 휴식 회전으로 로컬 변환). 렌더러: 피격 틴트 / 알파.
        internal static List<Channel> Channels(Transform rig, IEnumerable<string> boneNames)
        {
            var list = new List<Channel>();
            var all = rig.GetComponentsInChildren<Transform>(true);
            foreach (var name in boneNames)
            {
                var t = all.First(x => x != rig && x.name == name && x.GetComponent<SpriteRenderer>() == null);
                string path = AnimationUtility.CalculateTransformPath(t, rig);
                float parentAngle = (Quaternion.Inverse(rig.rotation) * t.parent.rotation).eulerAngles.z;
                var toLocal = Quaternion.Euler(0f, 0f, -parentAngle);
                Vector3 rest = t.localPosition;
                float restRot = ClipBuilder.SignedAngle(t.localEulerAngles.z);
                string bone = name;

                Vector3 Pos(Pose p) =>
                    rest + (p.Bones.TryGetValue(bone, out var d) ? toLocal * (Vector3)d.move : Vector3.zero);

                float Rot(Pose p) => restRot + (p.Bones.TryGetValue(bone, out var d) ? d.rot : 0f);

                list.Add(new Channel { Path = path, Type = typeof(Transform), Property = "m_LocalPosition.x", Value = p => Pos(p).x });
                list.Add(new Channel { Path = path, Type = typeof(Transform), Property = "m_LocalPosition.y", Value = p => Pos(p).y });
                list.Add(new Channel { Path = path, Type = typeof(Transform), Property = "m_LocalPosition.z", Value = p => Pos(p).z });
                list.Add(new Channel { Path = path, Type = typeof(Transform), Property = "localEulerAnglesRaw.x", Value = _ => 0f });
                list.Add(new Channel { Path = path, Type = typeof(Transform), Property = "localEulerAnglesRaw.y", Value = _ => 0f });
                list.Add(new Channel { Path = path, Type = typeof(Transform), Property = "localEulerAnglesRaw.z", Value = Rot });
            }

            foreach (var r in rig.GetComponentsInChildren<SpriteRenderer>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(r.transform, rig);
                string[] props = { "m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a" };
                for (int ch = 0; ch < 4; ch++)
                {
                    int index = ch;
                    list.Add(new Channel
                    {
                        Path = path,
                        Type = typeof(SpriteRenderer),
                        Property = props[index],
                        Value = p =>
                        {
                            if (p.Paint.HasValue)
                            {
                                return p.Paint.Value[index];
                            }

                            var c = UnityEngine.Color.Lerp(UnityEngine.Color.white, HitTint, p.Red);
                            c.a = p.Alpha;
                            return c[index];
                        },
                    });
                }
            }

            return list;
        }

        // 클립 안에서 변하는 묶음(위치 xyz / 회전 xyz / 색 rgba)만 남긴다. 일부 성분만 넣으면 나머지가 0으로 샘플링된다.
        internal static IEnumerable<Channel> Animated(List<Channel> channels, Pose[] poses)
        {
            bool Varies(Channel c)
            {
                float first = c.Value(poses[0]);
                return poses.Any(p => Mathf.Abs(c.Value(p) - first) > 1e-5f);
            }

            string Group(Channel c) => c.Path + "|" + c.Property.Substring(0, c.Property.LastIndexOf('.'));
            var animated = channels.GroupBy(Group).Where(g => g.Any(Varies)).Select(g => g.Key).ToHashSet();
            // 묶음 안에서도 늘 0인 성분(위치 z, 회전 x/y 등)은 빼도 0으로 샘플링되므로 생략한다.
            return channels.Where(c => animated.Contains(Group(c)) &&
                                       (Varies(c) || Mathf.Abs(c.Value(poses[0])) > 1e-5f));
        }

        internal static AnimationClip Keyed(string path, List<Channel> channels, (float t, Pose pose)[] keys)
        {
            var b = new ClipBuilder(MonsterAnimationBaker.LoadOrCreateClip(path), loop: false);
            foreach (var c in Animated(channels, keys.Select(k => k.pose).ToArray()))
            {
                b.Keyed(c.Path, c.Type, c.Property, keys.Select(k => (k.t, c.Value(k.pose))).ToArray());
            }

            b.Finish();
            return b.Clip;
        }

        // Move(기본) ⇄ Attack(트리거), 어디서든 Die(트리거)
        private static AnimatorController CreateController(string path, AnimationClip move, AnimationClip attack, AnimationClip die)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(MonsterAnimationBaker.AttackParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(MonsterAnimationBaker.DieParam, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var moveState = sm.AddState("Move", new Vector3(300f, 0f));
            var attackState = sm.AddState("Attack", new Vector3(560f, 0f));
            var dieState = sm.AddState("Die", new Vector3(300f, 120f));
            moveState.motion = move;
            attackState.motion = attack;
            dieState.motion = die;
            sm.defaultState = moveState;

            var toAttack = moveState.AddTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, MonsterAnimationBaker.AttackParam);

            var toMove = attackState.AddTransition(moveState);
            toMove.hasExitTime = true;
            toMove.exitTime = 1f;
            toMove.duration = 0.1f;

            var toDie = sm.AddAnyStateTransition(dieState);
            toDie.hasExitTime = false;
            toDie.duration = 0f;
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0f, MonsterAnimationBaker.DieParam);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // 프리팹의 Body를 PSD 리그(중첩 프리팹)로 교체하고 Animator를 붙인다.
        private static void AttachToPrefab(string prefabPath, GameObject rig, RuntimeAnimatorController controller,
            Action<GameObject> onAttach)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var oldBody = root.transform.Find("Body");
                if (oldBody != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldBody.gameObject);
                }

                var body = (GameObject)PrefabUtility.InstantiatePrefab(rig, root.transform);
                body.name = "Body";
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
                body.transform.localScale = Vector3.one;

                var animator = body.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                onAttach?.Invoke(body);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
