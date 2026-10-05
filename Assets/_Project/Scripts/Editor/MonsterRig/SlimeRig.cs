using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Game.Editor.MonsterRig.MonsterRigBuilder;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 슬라임: 단일 레이어 + 본 5개 (Base → Mid → Tip 척추, 좌우 SideL / SideR).
    /// 척추를 늘이고 줄여 출렁임, 좌우 본으로 옆으로 퍼짐을 만든다.
    /// </summary>
    public static class SlimeRig
    {
        public const string Name = "Slime";
        public const string PsdPath = "Assets/_Project/Sprites/Slime.psd";
        private const int Pad = 8;

        // 좌표는 slime.png(135x123) 픽셀 기준 + Pad
        internal static RigSpec Spec() => new()
        {
            PsdPath = PsdPath,
            CanvasSize = new Vector2Int(135 + Pad * 2, 123 + Pad * 2),
            PixelsPerUnit = 200f,
            Pivot = new Vector2(0.5f, Pad / (123f + Pad * 2)), // 그림 바닥 중앙 = 기존 스프라이트 피벗
            GridStep = 9,
            WeightFalloff = 2.5f,
            MaxInfluences = 3,
            Layers = new List<LayerSpec>
            {
                new() { Name = "slime", PngPath = "Assets/_Project/Sprites/slime.png", CanvasOffset = new Vector2Int(Pad, Pad) },
            },
            Bones = new List<BoneSpec>
            {
                new() { Name = "Base", Parent = null, Start = P(67, 2), End = P(67, 34) },
                new() { Name = "Mid", Parent = "Base", Start = P(67, 34), End = P(72, 76) },
                new() { Name = "Tip", Parent = "Mid", Start = P(72, 76), End = P(82, 120) },
                new() { Name = "SideL", Parent = "Base", Start = P(50, 40), End = P(4, 42) },
                new() { Name = "SideR", Parent = "Base", Start = P(84, 40), End = P(131, 42) },
            },
        };

        private static Vector2 P(float x, float y) => new(x + Pad, y + Pad);

        public static GameObject BuildRig()
        {
            MonsterAnimationBaker.EnsureFolder(System.IO.Path.GetDirectoryName(PsdPath)!.Replace('\\', '/'));
            return Build(Spec());
        }

        // ---------- animation ----------

        private const string AnimFolder = "Assets/_Project/Animations";
        private const string PrefabPath = "Assets/_Project/Prefabs/Monsters/Slime_Animated.prefab";
        private const string RendererPath = "slime";
        private const float Tau = Mathf.PI * 2f;
        private static readonly Color HitTint = new(1f, 0.35f, 0.35f, 1f);

        /// <summary>본 포즈. 값은 모두 휴식 포즈 대비 변화량.</summary>
        private struct Pose
        {
            public float Stretch;     // 몸통(Mid 관절) 길이 배율 - 1
            public float TipStretch;  // 꼭지(Tip 관절) 길이 배율 - 1
            public float Widen;       // 좌우 본을 바깥으로 (유닛)
            public float MidRot;      // 도
            public float TipRot;      // 도
            public float Hop;         // 전체 위로 (유닛)

            public static Pose Rest => default;

            // 몸통과 꼭지가 같이 움직이는 포즈
            public static Pose Uniform(float stretch, float widen, float tipRot) =>
                new() { Stretch = stretch, TipStretch = stretch, Widen = widen, TipRot = tipRot };
        }

        public static void BuildAnimation()
        {
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(PsdPath);
            if (rig == null)
            {
                throw new InvalidOperationException("먼저 BuildRig()를 실행하세요: " + PsdPath);
            }

            var channels = Channels(rig.transform);
            MonsterAnimationBaker.EnsureFolder(AnimFolder);

            // 통통 튀며 출렁인다: 빨리 눌리고 늘어나며 살짝 떴다가 착지에서 다시 눌리는 반동,
            // 좌우 폭은 부피가 유지될 만큼, 꼭지는 몸통보다 늦고 크게 따라와 채찍 같은 여운을 준다.
            const float moveLength = 1.1f;
            float Wave(float phase) => Mathf.Sin(phase) + 0.2f * Mathf.Sin(2f * phase);
            var move = Looping($"{AnimFolder}/Slime_Move.anim", channels, moveLength, t =>
            {
                float phase = t * Tau / moveLength;
                float body = Wave(phase);
                float up = Mathf.Max(0f, Mathf.Sin(phase));
                return new Pose
                {
                    Stretch = 0.16f * body,
                    TipStretch = 0.18f * Wave(phase - 0.8f),
                    Widen = -0.02f * body,
                    Hop = 0.03f * up * up,
                    MidRot = 2.5f * Mathf.Sin(phase - 0.4f),
                    TipRot = 6f * Mathf.Sin(phase - 1f),
                };
            });

            // 사망: 움찔 → 천천히 주저앉으며 → 옆으로 넓게 퍼져 → 납작한 웅덩이가 되어 사라짐
            var die = Keyed($"{AnimFolder}/Slime_Die.anim", channels, new[]
            {
                (0f, Pose.Rest),
                (0.08f, new Pose { Stretch = -0.12f, TipStretch = -0.1f, Widen = 0.03f, TipRot = -8f }),
                (0.3f, new Pose { Stretch = -0.25f, TipStretch = -0.3f, Widen = 0.05f, MidRot = 4f, TipRot = 10f }),
                (0.7f, new Pose { Stretch = -0.5f, TipStretch = -0.6f, Widen = 0.13f, MidRot = 6f, TipRot = 18f, Hop = -0.005f }),
                (1.1f, new Pose { Stretch = -0.68f, TipStretch = -0.75f, Widen = 0.2f, MidRot = 6f, TipRot = 22f, Hop = -0.01f }),
            }, b => Tint(b, (0f, Color.white), (0.06f, HitTint), (0.2f, Color.white), (0.75f, Color.white),
                (1.2f, new Color(1f, 1f, 1f, 0f))));

            // 공격: 꼭지를 뒤로 젖히며 몸을 늘임 → 꼭지가 채찍처럼 앞쪽 아래로 휘둘러 내려침(몸은 눌림)
            //       → 반동으로 튕겨 돌아갔다가 → 흔들리며 복귀
            var attack = Keyed($"{AnimFolder}/Slime_Attack.anim", channels, new[]
            {
                (0f, Pose.Rest),
                (0.2f, new Pose { Stretch = 0.12f, TipStretch = 0.05f, Widen = -0.01f, MidRot = -8f, TipRot = -25f }),
                (0.32f, new Pose { Stretch = -0.12f, TipStretch = 0.12f, Widen = 0.03f, MidRot = 18f, TipRot = 58f }),
                (0.42f, new Pose { Stretch = -0.06f, TipStretch = 0.05f, Widen = 0.02f, MidRot = 8f, TipRot = -20f }),
                (0.52f, new Pose { Stretch = 0.02f, MidRot = -2f, TipRot = 8f }),
                (0.65f, Pose.Rest),
            }, _ => { });

            var controller = CreateController($"{AnimFolder}/Slime.controller", move, attack, die);
            AttachToPrefab(rig, controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[SlimeRig] 애니메이션 / 프리팹 갱신 완료");
        }

        // Move(기본) ⇄ Attack(트리거), 어디서든 Die(트리거)
        private static UnityEditor.Animations.AnimatorController CreateController(string path, AnimationClip move,
            AnimationClip attack, AnimationClip die)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
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
            toAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, MonsterAnimationBaker.AttackParam);

            var toMove = attackState.AddTransition(moveState);
            toMove.hasExitTime = true;
            toMove.exitTime = 1f;
            toMove.duration = 0.1f;

            var toDie = sm.AddAnyStateTransition(dieState);
            toDie.hasExitTime = false;
            toDie.duration = 0f;
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, MonsterAnimationBaker.DieParam);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private sealed class Channel
        {
            public string Path;
            public string Property;
            public Func<Pose, float> Value;
        }

        private static List<Channel> Channels(Transform rig)
        {
            Transform Find(string path) => rig.Find(path) ?? throw new InvalidOperationException("본 없음: " + path);
            var baseBone = Find("Base");
            var mid = Find("Base/Mid");
            var tip = Find("Base/Mid/Tip");
            var sideL = Find("Base/SideL");
            var sideR = Find("Base/SideR");

            var list = new List<Channel>();

            void Pos(string path, Transform t, Func<Pose, Vector2> offset)
            {
                Vector3 rest = t.localPosition;
                list.Add(new Channel { Path = path, Property = "m_LocalPosition.x", Value = p => rest.x + offset(p).x });
                list.Add(new Channel { Path = path, Property = "m_LocalPosition.y", Value = p => rest.y + offset(p).y });
                list.Add(new Channel { Path = path, Property = "m_LocalPosition.z", Value = _ => rest.z });
            }

            void Rot(string path, Transform t, Func<Pose, float> delta)
            {
                float rest = ClipBuilder.SignedAngle(t.localEulerAngles.z);
                list.Add(new Channel { Path = path, Property = "localEulerAnglesRaw.x", Value = _ => 0f });
                list.Add(new Channel { Path = path, Property = "localEulerAnglesRaw.y", Value = _ => 0f });
                list.Add(new Channel { Path = path, Property = "localEulerAnglesRaw.z", Value = p => rest + delta(p) });
            }

            float midLength = mid.localPosition.x;
            float tipLength = tip.localPosition.x;
            Pos("Base", baseBone, p => new Vector2(0f, p.Hop));
            // Mid/Tip은 부모 본의 +x(본 방향)에 놓여 있으므로 x를 늘이면 척추가 늘어난다.
            Pos("Base/Mid", mid, p => new Vector2(midLength * p.Stretch, 0f));
            Pos("Base/Mid/Tip", tip, p => new Vector2(tipLength * p.TipStretch, 0f));
            // Base는 위(+90°)를 향하므로 Base 로컬 +y가 화면 왼쪽
            Pos("Base/SideL", sideL, p => new Vector2(0f, p.Widen));
            Pos("Base/SideR", sideR, p => new Vector2(0f, -p.Widen));
            Rot("Base/Mid", mid, p => p.MidRot);
            Rot("Base/Mid/Tip", tip, p => p.TipRot);
            return list;
        }

        private static AnimationClip Looping(string path, List<Channel> channels, float length, Func<float, Pose> pose)
        {
            var b = new ClipBuilder(MonsterAnimationBaker.LoadOrCreateClip(path), loop: true);
            var samples = Enumerable.Range(0, 24).Select(i => pose(length * i / 24f)).ToArray();
            foreach (var c in Animated(channels, samples))
            {
                b.Sparse(c.Path, typeof(Transform), c.Property, length, t => c.Value(pose(t)), 8);
            }

            b.Finish();
            return b.Clip;
        }

        private static AnimationClip Keyed(string path, List<Channel> channels, (float t, Pose pose)[] keys,
            Action<ClipBuilder> extra)
        {
            var b = new ClipBuilder(MonsterAnimationBaker.LoadOrCreateClip(path), loop: false);
            foreach (var c in Animated(channels, keys.Select(k => k.pose).ToArray()))
            {
                b.Keyed(c.Path, typeof(Transform), c.Property, keys.Select(k => (k.t, c.Value(k.pose))).ToArray());
            }

            extra(b);
            b.Finish();
            return b.Clip;
        }

        // 이 클립에서 값이 변하는 채널만 남긴다.
        // 위치/회전은 x·y·z 중 하나라도 변하면 셋을 모두 넣는다. (일부만 넣으면 빠진 성분이 0으로 샘플링된다)
        private static IEnumerable<Channel> Animated(List<Channel> channels, Pose[] poses)
        {
            bool Varies(Channel c)
            {
                float first = c.Value(poses[0]);
                return poses.Any(p => Mathf.Abs(c.Value(p) - first) > 1e-5f);
            }

            string Group(Channel c) => c.Path + "|" + c.Property.Substring(0, c.Property.LastIndexOf('.'));

            var animatedGroups = channels.GroupBy(Group)
                .Where(g => g.Any(Varies))
                .Select(g => g.Key)
                .ToHashSet();

            return channels.Where(c => animatedGroups.Contains(Group(c)));
        }

        private static void Tint(ClipBuilder b, params (float t, Color c)[] keys)
        {
            string[] props = { "m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a" };
            for (int ch = 0; ch < 4; ch++)
            {
                int index = ch;
                b.Keyed(RendererPath, typeof(SpriteRenderer), props[index], keys.Select(k => (k.t, k.c[index])).ToArray());
            }
        }

        // Slime_Animated 프리팹의 Body를 PSD 리그(중첩 프리팹)로 교체하고 Animator를 붙인다.
        private static void AttachToPrefab(GameObject rig, RuntimeAnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
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

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
