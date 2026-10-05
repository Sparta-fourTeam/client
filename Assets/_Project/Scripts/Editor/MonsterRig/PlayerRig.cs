using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using static Game.Editor.MonsterRig.MonsterRigBuilder;
using static Game.Editor.MonsterRig.MonsterRigs;
using Pose = Game.Editor.MonsterRig.MonsterRigs.Pose;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 플레이어 사신(reaper.png 1024px, 검은 배경 → 투명, 1/4 축소):
    /// 로브(본 6개) + 지팡이(손 포함, 본 1개) + 양옆 유령 2마리(본 2개씩) + 떠다니는 점(점마다 본 1개).
    /// Idle: 로브가 숨 쉬듯 떠 있고 유령·점이 제각각 둥실 / Attack: 지팡이를 들어 올렸다가 앞으로 내밀며 구슬이 번쩍.
    /// 발사체는 따로 구현하므로 클립에는 넣지 않는다. 좌표는 원본 픽셀(좌상단 원점)로 적고 V()로 변환한다.
    /// </summary>
    public static class PlayerRig
    {
        private const string Png = "Assets/_Project/Art/Characters/Player/reaper.png";
        private const string RigFolder = "Assets/_Project/Art/Characters/Player/Rig";
        private const string AnimFolder = "Assets/_Project/Art/Characters/Player/Animations";
        private const string PrefabPath = "Assets/_Project/Prefabs/Player/Player_Animated.prefab";
        private const int Scale = 4;
        private const float Ppu = 200f;
        private const float Tau = Mathf.PI * 2f;
        private const float IdleLength = 2.4f;
        private const float AttackLength = 0.55f;

        // 원본에서 잘라 쓰는 범위 (워터마크 제외)
        private static readonly RectInt Crop = new(144, 192, 768, 640);

        private sealed class Art
        {
            public Vector2Int Size;
            public Color32[] Robe, Staff, GhostL, GhostR;
            public List<(Color32[] px, Vector2 center)> Dots;
        }

        private static Art s_art;

        // 원본 좌표(좌상단 원점) → 축소된 레이어 픽셀(좌하단 원점)
        private static Vector2 V(float x, float y) => new((x - Crop.x) / Scale, (Crop.yMax - y) / Scale);

        private static BoneSpec B(string name, string parent, float x0, float y0, float x1, float y1)
        {
            var a = V(x0, y0);
            var b = V(x1, y1);
            return Bone(name, parent, a.x, a.y, b.x, b.y);
        }

        // 관절 (원본 좌표)
        private static readonly Vector2 Waist = new(540, 600);
        private static readonly Vector2 Shoulder = new(430, 545);
        private static readonly Vector2 Hand = new(375, 560);
        private static readonly Vector2 Orb = new(305, 345);
        private static readonly Vector2 Pivot = new(540, 560);

        public static string Build()
        {
            s_art = null;
            var art = s_art = Prepare();

            var bones = new List<BoneSpec>
            {
                B("Root", null, 540, 770, Waist.x, Waist.y),
                B("Body", "Root", Waist.x, Waist.y, 540, 500),
                B("Hood", "Body", 535, 510, 525, 280),
                B("Sleeve", "Body", Shoulder.x, Shoulder.y, 405, 640),
                B("Staff", "Sleeve", Hand.x, Hand.y, 300, 290),
                B("HemL", "Root", 470, 690, 395, 775),
                B("HemR", "Root", 620, 690, 715, 770),
                B("GhostL", null, 215, 610, 230, 480),
                B("GhostLTail", "GhostL", 225, 640, 270, 715),
                B("GhostR", null, 815, 440, 775, 215),
                B("GhostRTail", "GhostR", 790, 480, 725, 610),
            };
            for (int i = 0; i < art.Dots.Count; i++)
            {
                var c = art.Dots[i].center;
                bones.Add(Bone("Dot" + i, null, c.x, c.y, c.x, c.y + 6f));
            }

            var layers = new List<LayerSpec>
            {
                Layer("reaper_ghost_l", art.GhostL, art.Size, "GhostL", "GhostLTail"),
                Layer("reaper_ghost_r", art.GhostR, art.Size, "GhostR", "GhostRTail"),
            };
            for (int i = 0; i < art.Dots.Count; i++)
            {
                layers.Add(Layer("reaper_dot" + i, art.Dots[i].px, art.Size, "Dot" + i));
            }

            layers.Add(Layer("reaper_robe", art.Robe, art.Size, "Root", "Body", "Hood", "Sleeve", "HemL", "HemR"));
            layers.Add(Layer("reaper_staff", art.Staff, art.Size, "Staff"));

            const int pad = 8;
            string psdPath = $"{RigFolder}/Player.psd";
            MonsterAnimationBaker.EnsureFolder(RigFolder);
            var pivot = V(Pivot.x, Pivot.y);
            var rig = MonsterRigBuilder.Build(new RigSpec
            {
                PsdPath = psdPath,
                CanvasSize = art.Size + Vector2Int.one * (pad * 2),
                PixelsPerUnit = Ppu,
                Pivot = new Vector2((pivot.x + pad) / (art.Size.x + pad * 2f), (pivot.y + pad) / (art.Size.y + pad * 2f)),
                GridStep = 8,
                WeightFalloff = 2.5f,
                MaxInfluences = 3,
                Layers = layers.Select(l => { l.CanvasOffset = new Vector2Int(pad, pad); return l; }).ToList(),
                Bones = bones.Select(b => new BoneSpec { Name = b.Name, Parent = b.Parent, Start = b.Start + Vector2.one * pad, End = b.End + Vector2.one * pad }).ToList(),
            });

            var channels = Channels(rig.transform, bones.Select(b => b.Name));
            MonsterAnimationBaker.EnsureFolder(AnimFolder);

            var idle = new ClipBuilder(MonsterAnimationBaker.LoadOrCreateClip($"{AnimFolder}/Player_Idle.anim"), loop: true);
            int dots = art.Dots.Count;
            var samples = Enumerable.Range(0, 48).Select(i => Idle(IdleLength * i / 48f, dots)).ToArray();
            foreach (var c in Animated(channels, samples))
            {
                idle.Sparse(c.Path, c.Type, c.Property, IdleLength, t => c.Value(Idle(t, dots)), 8);
            }

            idle.Finish();

            var keys = AttackKeys();
            var attack = Keyed($"{AnimFolder}/Player_Attack.anim", channels, keys.Select(k => (k.t, k.pose)).ToArray());
            var glow = GlowTrack(keys);
            ClipFx.AddCurves(attack, GlowRoot, new[] { glow });

            var controller = CreateController($"{AnimFolder}/Player.controller", idle.Clip, attack);
            AttachToPrefab(rig, controller, body => ClipFx.CreateObjects(body, GlowRoot, Vector2.zero, new[] { glow }));
            AssetDatabase.SaveAssets();

            var sprites = rig.GetComponentsInChildren<SpriteRenderer>();
            int verts = sprites.Sum(r => UnityEngine.U2D.SpriteDataAccessExtensions.GetVertexCount(r.sprite));
            return $"Player: size={art.Size} layers={sprites.Length} bones={bones.Count} verts={verts} dots={dots}";
        }

        private static LayerSpec Layer(string name, Color32[] px, Vector2Int size, params string[] bones) =>
            new() { Name = name, Pixels = px, PixelsSize = size, Bones = bones };

        // ---------- 움직임 ----------

        // 로브는 숨 쉬듯 살짝 떠오르고(자락은 반 박자 늦게), 유령·점은 각자 다른 박자로 둥실.
        private static Pose Idle(float t, int dots)
        {
            float ph = t * Tau / IdleLength;
            var p = P()
                .B("Root", dy: 0.01f * Mathf.Sin(ph))
                .B("Body", rot: 1f * Mathf.Sin(ph - 0.5f))
                .B("Hood", rot: 1.5f * Mathf.Sin(ph - 1f))
                .B("Sleeve", rot: -1f * Mathf.Sin(ph - 0.7f))
                .B("Staff", rot: 1.5f * Mathf.Sin(ph - 0.9f))
                .B("HemL", rot: 3f * Mathf.Sin(ph - 1.4f))
                .B("HemR", rot: -3f * Mathf.Sin(ph - 1.7f))
                .B("GhostL", dx: 0.006f * Mathf.Sin(2f * ph), dy: 0.025f * Mathf.Sin(ph + 1.5f), rot: 4f * Mathf.Sin(ph + 0.8f))
                .B("GhostLTail", rot: 10f * Mathf.Sin(ph + 0.2f))
                .B("GhostR", dx: -0.006f * Mathf.Sin(2f * ph + 1f), dy: 0.025f * Mathf.Sin(ph + 3.6f), rot: -4f * Mathf.Sin(ph + 2.9f))
                .B("GhostRTail", rot: -10f * Mathf.Sin(ph + 2.2f));
            for (int i = 0; i < dots; i++)
            {
                float k = i % 2 == 0 ? 1f : 2f;
                p.B("Dot" + i, dy: 0.015f * Mathf.Sin(k * ph + i * 1.7f));
            }

            return p;
        }

        // raise: 팔 들어 올림(도, 음수 = 손이 위로) / staff: 지팡이 자체 회전(양수 = 끝이 바깥쪽) / lean: 몸 기울기 / spread: 유령이 벌어지는 양(유닛)
        private static Pose Cast(float raise, float staff, float lean, float lift, float spread, float tail)
        {
            return P()
                .B("Root", dy: lift)
                .B("Body", rot: lean)
                .B("Hood", rot: lean * 1.2f)
                .B("Sleeve", rot: raise)
                .B("Staff", rot: staff)
                .B("HemL", rot: -lean * 1.5f)
                .B("HemR", rot: -lean * 1.5f)
                .B("GhostL", dx: -spread, dy: Mathf.Abs(spread) * 0.3f, rot: spread * 150f)
                .B("GhostLTail", rot: tail)
                .B("GhostR", dx: spread, dy: Mathf.Abs(spread) * 0.3f, rot: -spread * 150f)
                .B("GhostRTail", rot: -tail);
        }

        // 지팡이를 들어 올림(유령이 몸 쪽으로 모임) → 앞으로 내밀며 구슬 번쩍(유령이 확 벌어짐) → 잠깐 버팀 → 복귀
        private static (float t, Pose pose, float glow)[] AttackKeys() => new[]
        {
            (0f, P(), 0f),
            (0.14f, Cast(raise: -25f, staff: 18f, lean: -2f, lift: 0.01f, spread: -0.02f, tail: -8f), 0.35f),
            (0.25f, Cast(raise: 4f, staff: 22f, lean: 3f, lift: -0.006f, spread: 0.035f, tail: 14f), 1f),
            (0.36f, Cast(raise: 3f, staff: 14f, lean: 2f, lift: -0.003f, spread: 0.02f, tail: 6f), 0.3f),
            (AttackLength, P(), 0f),
        };

        // ---------- 구슬 번쩍임 (Attack 클립 안 스프라이트) ----------

        private const string GlowRoot = "OrbGlowFx";

        private static ClipFx.Track GlowTrack((float t, Pose pose, float glow)[] keys)
        {
            var teal = new Color(0.55f, 1f, 0.85f, 1f);
            var pivot = V(Pivot.x, Pivot.y);
            return new ClipFx.Track
            {
                Name = "OrbGlow",
                Sprite = ClipFx.Soft,
                Order = 40,
                Keys = keys.Select(k =>
                {
                    var pos = (OrbAt(k.pose) - pivot) / Ppu;
                    float size = 0.08f + 0.22f * k.glow;
                    return (k.t, pos, Vector2.one * size, ClipFx.Alpha(teal, k.glow));
                }).ToArray(),
            };
        }

        // 포즈에서 구슬 중심 위치 (축소 픽셀): Root 이동 → Body(허리) → Sleeve(어깨) → Staff(손) 순으로 돈다
        private static Vector2 OrbAt(Pose pose)
        {
            float Rot(string bone) => pose.Bones.TryGetValue(bone, out var d) ? d.rot : 0f;
            Vector2 Move(string bone) => pose.Bones.TryGetValue(bone, out var d) ? d.move * Ppu : Vector2.zero;
            Vector2 Turn(Vector2 pt, Vector2 about, float deg) => about + (Vector2)(Quaternion.Euler(0f, 0f, deg) * (pt - about));

            var waist = V(Waist.x, Waist.y);
            var shoulder = V(Shoulder.x, Shoulder.y);
            var hand = V(Hand.x, Hand.y);
            var orb = V(Orb.x, Orb.y);

            // 안쪽 관절부터: 손 기준 지팡이 → 어깨 기준 팔 → 허리 기준 몸 (뒤 관절 위치도 함께 돈다)
            orb = Turn(orb, hand, Rot("Staff"));
            orb = Turn(orb, shoulder, Rot("Sleeve"));
            orb = Turn(orb, waist, Rot("Body"));
            return orb + Move("Root");
        }

        // ---------- 컨트롤러 / 프리팹 ----------

        // Idle(기본) ⇄ Attack(트리거)
        private static AnimatorController CreateController(string path, AnimationClip idle, AnimationClip attack)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(MonsterAnimationBaker.AttackParam, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle", new Vector3(300f, 0f));
            var attackState = sm.AddState("Attack", new Vector3(560f, 0f));
            idleState.motion = idle;
            attackState.motion = attack;
            sm.defaultState = idleState;

            var toAttack = idleState.AddTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, MonsterAnimationBaker.AttackParam);

            // 연사 중에도 다시 처음부터
            var again = attackState.AddTransition(attackState);
            again.hasExitTime = false;
            again.duration = 0.03f;
            again.AddCondition(AnimatorConditionMode.If, 0f, MonsterAnimationBaker.AttackParam);

            var toIdle = attackState.AddTransition(idleState);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 1f;
            toIdle.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // 플레이어 비주얼 프리팹(루트 + Body = PSD 리그 + Animator). 공용 Player 프리팹은 건드리지 않는다.
        private static void AttachToPrefab(GameObject rig, RuntimeAnimatorController controller, System.Action<GameObject> onAttach)
        {
            MonsterAnimationBaker.EnsureFolder(Path.GetDirectoryName(PrefabPath)!.Replace('\\', '/'));
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var empty = new GameObject(Path.GetFileNameWithoutExtension(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(empty, PrefabPath);
                Object.DestroyImmediate(empty);
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var oldBody = root.transform.Find("Body");
                if (oldBody != null)
                {
                    Object.DestroyImmediate(oldBody.gameObject);
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

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- 그림 준비 ----------

        // 지팡이(손 포함) 영역: 로브 왼쪽 경계를 높이별로 자른 선 (원본 좌표)
        private static bool InStaff(int x, int y)
        {
            if (y < 255 || y >= 706)
            {
                return false;
            }

            if (y < 530)
            {
                return x < 382;
            }

            if (y < 566)
            {
                return x < 400;
            }

            if (y < 590)
            {
                return x < 388;
            }

            return x < Mathf.Min(404f, 388f + (y - 590) * 0.25f);
        }

        private static Art Prepare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(Path.GetFullPath(Png)));
            var raw = tex.GetPixels32();
            int fw = tex.width, fh = tex.height;
            Object.DestroyImmediate(tex);

            // 잘라내고 좌상단 원점으로 (Texture2D는 아래 행부터)
            int w = Crop.width, h = Crop.height;
            var src = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    src[y * w + x] = raw[(fh - 1 - (Crop.y + y)) * fw + Crop.x + x];
                }
            }

            float Lum(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            bool Teal(Color32 c) => c.g > c.r + 40 && c.g > 90;

            // 1) 가장자리에서 이어진 검정 = 배경 → 투명. 로브 안쪽의 어두운 빗금은 가장자리와 안 이어지므로 남는다.
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

            // 지팡이 갈래 사이에 갇힌 검정도 배경 (로브 안쪽 빗금은 지팡이 영역 밖이라 남는다)
            foreach (var comp in Components(src.Length, w, h, i => !bg[i] && Lum(src[i]) < 0.06f))
            {
                if (comp.Count >= 30 && comp.All(i => InStaff(i % w + Crop.x, i / w + Crop.y)))
                {
                    foreach (int i in comp)
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

            // 2) 덩어리별로 나눈다: 청록 덩어리 = 유령(큰 것) / 점(작은 것), 구슬은 지팡이, 나머지는 로브·지팡이를 경계선으로
            var robe = new Color32[src.Length];
            var staff = new Color32[src.Length];
            var ghostL = new Color32[src.Length];
            var ghostR = new Color32[src.Length];
            var dots = new List<(Color32[] px, Vector2 center)>();
            var orbRect = new RectInt(250 - Crop.x, 290 - Crop.y, 110, 120);
            foreach (var comp in Components(px.Length, w, h, i => px[i].a > 0))
            {
                if (comp.Count < 40)
                {
                    continue; // 티끌
                }

                var center = new Vector2((float)comp.Average(i => i % w), (float)comp.Average(i => i / w));
                float teal = comp.Count(i => Teal(src[i])) / (float)comp.Count;
                Color32[] target = null;
                if (orbRect.Contains(Vector2Int.RoundToInt(center)))
                {
                    target = staff;
                }
                else if (teal > 0.5f && comp.Count > 4000)
                {
                    target = center.x + Crop.x < 500 ? ghostL : ghostR;
                }
                else if (teal > 0.2f && comp.Count < 1500)
                {
                    // 떠다니는 점 (가운데가 밝아 청록 비율이 낮은 점도 있다)
                    var dot = new Color32[src.Length];
                    foreach (int i in comp)
                    {
                        dot[i] = px[i];
                    }

                    dots.Add((dot, V(center.x + Crop.x, center.y + Crop.y)));
                    continue;
                }

                foreach (int i in comp)
                {
                    var dst = target ?? (InStaff(i % w + Crop.x, i / w + Crop.y) ? staff : robe);
                    dst[i] = px[i];
                }
            }

            dots.Sort((a, b) => a.center.x.CompareTo(b.center.x));
            return new Art
            {
                Size = new Vector2Int(w / Scale, h / Scale),
                Robe = Downscale(robe, w, h),
                Staff = Downscale(staff, w, h),
                GhostL = Downscale(ghostL, w, h),
                GhostR = Downscale(ghostR, w, h),
                Dots = dots.Select(d => (Downscale(d.px, w, h), d.center)).ToList(),
            };
        }

        // 좌상단 원점 원본 → 4x4 알파 가중 평균, 좌하단 원점 결과
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

                    result[(oh - 1 - y) * ow + x] = a > 0f
                        ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / (Scale * Scale) * 255f))
                        : new Color32(0, 0, 0, 0);
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

        /// <summary>준비 결과 확인: 왼쪽 원본(합친 것), 오른쪽 파츠별 색 (로브 회색, 지팡이 주황, 유령 초록/파랑, 점 노랑)</summary>
        public static string WritePreview(string path)
        {
            var art = s_art = Prepare();
            int w = art.Size.x, h = art.Size.y;
            var tex = new Texture2D(w * 2, h, TextureFormat.RGBA32, false);
            var parts = new List<(Color32[] px, Color tint)>
            {
                (art.Robe, Color.gray), (art.Staff, new Color(1f, 0.5f, 0f)), (art.GhostL, Color.green), (art.GhostR, Color.cyan),
            };
            parts.AddRange(art.Dots.Select(d => (d.px, Color.yellow)));
            for (int i = 0; i < w * h; i++)
            {
                Color merged = Color.black, tint = Color.black;
                foreach (var (p, c) in parts)
                {
                    if (p[i].a > 0)
                    {
                        merged = Color.Lerp(merged, p[i], p[i].a / 255f);
                        tint = c;
                    }
                }

                tex.SetPixel(i % w, i / w, merged);
                tex.SetPixel(w + i % w, i / w, tint);
            }

            // 3배 확대 (보기 쉽게)
            var big = new Texture2D(w * 6, h * 3, TextureFormat.RGBA32, false);
            for (int y = 0; y < h * 3; y++)
            {
                for (int x = 0; x < w * 6; x++)
                {
                    big.SetPixel(x, y, tex.GetPixel(x / 3, y / 3));
                }
            }

            File.WriteAllBytes(path, big.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(big);
            return $"size={art.Size} dots={art.Dots.Count} " + string.Join(",", art.Dots.Select(d => d.center.ToString("0")));
        }
    }
}
