using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>
    /// Monsters 프리팹의 절차적 움직임(MonsterAnimator)을 AnimationClip으로 굽고
    /// Idle / Move / Hit / Die 상태를 가진 AnimatorController로 교체한다.
    /// 이미 본 리깅된 프리팹(Golem_Rigged 등)은 기존 걷기 클립을 Move로 재사용한다.
    /// </summary>
    public static class MonsterAnimationBaker
    {
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies/Monsters";
        private const string OutputRoot = "Assets/_Project/Art/Characters/Monsters/Animations";
        private const string ProceduralTypeName = "Game.View.Monster.MonsterAnimator";
        private const float FrameRate = 30f;
        private const float Tau = Mathf.PI * 2f;

        public const string MovingParam = "Moving";
        public const string HitParam = "Hit";
        public const string DieParam = "Die";
        public const string AttackParam = "Attack";

        // MonsterAnimator.MonsterMotion과 같은 순서 (Game.Editor는 Game.View를 참조하지 않는다)
        private enum Motion { Slime, Float, Ghost, Jelly, Walk, HeavyWalk, Bat, Bones }

        private struct Part
        {
            public string Path;
            public Vector3 RestPosition;
            public Vector2 Pivot;
            public float RotationAmplitude;
            public float RotationFrequency;
            public Vector2 MoveAmplitude;
            public float MoveFrequency;
            public float Phase;
        }

        private struct BodyPose
        {
            public float X, Y, Rot, Sx, Sy;
        }

        [MenuItem("Tools/Monster/Bake Animations")]
        public static void BakeAll()
        {
            EnsureFolder(OutputRoot);
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            int baked = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (BakePrefab(path))
                {
                    baked++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MonsterAnimationBaker] {baked}/{guids.Length}개 프리팹 베이크 완료");
        }

        // unity run ... -executeMethod Game.Editor.MonsterAnimationBaker.BakeAllFromCommandLine
        public static void BakeAllFromCommandLine()
        {
            try
            {
                BakeAll();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static bool BakePrefab(string prefabPath)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(prefabPath).Replace("_Animated", "");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var procedural = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .FirstOrDefault(c => c != null && c.GetType().FullName == ProceduralTypeName);

                Transform host;
                Transform body;
                AnimationClip idle, move;
                var folder = $"{OutputRoot}/{name}";
                EnsureFolder(folder);

                if (procedural != null)
                {
                    var so = new SerializedObject(procedural);
                    var motion = (Motion)so.FindProperty("_motion").enumValueIndex;
                    body = so.FindProperty("_body").objectReferenceValue as Transform;
                    if (body == null)
                    {
                        body = procedural.transform;
                    }

                    float speed = so.FindProperty("_speed").floatValue;

                    var targets = new List<(Transform target, SerializedProperty prop)>();
                    var partsProp = so.FindProperty("_parts");
                    for (int i = 0; i < partsProp.arraySize; i++)
                    {
                        var p = partsProp.GetArrayElementAtIndex(i);
                        if (p.FindPropertyRelative("Target").objectReferenceValue is Transform t)
                        {
                            targets.Add((t, p));
                        }
                    }

                    // 파츠가 모두 Body 아래에 있으면 Animator를 Body에 붙여서 루트(이동)와 분리한다.
                    host = targets.All(x => x.target.IsChildOf(body)) ? body : procedural.transform;

                    var parts = targets.Select(x => new Part
                    {
                        Path = AnimationUtility.CalculateTransformPath(x.target, host),
                        RestPosition = x.target.localPosition,
                        Pivot = x.prop.FindPropertyRelative("Pivot").vector2Value,
                        RotationAmplitude = x.prop.FindPropertyRelative("RotationAmplitude").floatValue,
                        RotationFrequency = x.prop.FindPropertyRelative("RotationFrequency").floatValue,
                        MoveAmplitude = x.prop.FindPropertyRelative("MoveAmplitude").vector2Value,
                        MoveFrequency = x.prop.FindPropertyRelative("MoveFrequency").floatValue,
                        Phase = x.prop.FindPropertyRelative("Phase").floatValue,
                    }).ToArray();

                    move = BakeProcedural(LoadOrCreateClip($"{folder}/{name}_Move.anim"), motion, parts, body, host, speed);
                    idle = motion is Motion.Walk or Motion.HeavyWalk
                        ? BakeBreathing(LoadOrCreateClip($"{folder}/{name}_Idle.anim"), body, host)
                        : BakeProcedural(LoadOrCreateClip($"{folder}/{name}_Idle.anim"), motion, parts, body, host, speed * 0.6f);

                    Object.DestroyImmediate(procedural);
                }
                else
                {
                    // 이미 리깅된 프리팹: 기존 컨트롤러의 첫 클립(걷기)을 Move로 재사용
                    var existing = root.GetComponentInChildren<Animator>(true);
                    var walk = existing != null && existing.runtimeAnimatorController != null
                        ? existing.runtimeAnimatorController.animationClips
                            .FirstOrDefault(c => !c.name.StartsWith(name + "_"))
                        : null;
                    if (walk == null)
                    {
                        Debug.LogWarning($"[MonsterAnimationBaker] {prefabPath}: MonsterAnimator도 기존 걷기 클립도 없어 건너뜀");
                        return false;
                    }

                    host = existing.transform;
                    body = host;
                    move = walk;
                    idle = BakeBreathing(LoadOrCreateClip($"{folder}/{name}_Idle.anim"), body, host);
                }

                var renderers = host.GetComponentsInChildren<SpriteRenderer>(true);
                var hit = BakeHit(LoadOrCreateClip($"{folder}/{name}_Hit.anim"), body, host, renderers);
                var die = BakeDie(LoadOrCreateClip($"{folder}/{name}_Die.anim"), body, host, renderers);

                var controller = CreateController($"{folder}/{name}.controller", idle, move, hit, die);

                var animator = host.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = host.gameObject.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[MonsterAnimationBaker] {prefabPath} → {controller.name} (Animator: {host.name})");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- Idle / Move ----------

        private static AnimationClip BakeProcedural(AnimationClip clip, Motion motion, Part[] parts,
            Transform body, Transform host, float speed)
        {
            // 모든 사인파가 클립 끝에서 정확히 이어지도록 루프 길이를 고르고 주파수를 그 배수로 맞춘다.
            var freqs = BodyFrequencies(motion).ToList();
            foreach (var p in parts)
            {
                float rf = p.RotationFrequency > 0f ? p.RotationFrequency : 1f;
                float mf = p.MoveFrequency > 0f ? p.MoveFrequency : 1f;
                freqs.Add(rf);
                if (p.MoveAmplitude != Vector2.zero)
                {
                    freqs.Add(mf);
                    freqs.Add(mf * 0.8f);
                }
            }

            float length = ChooseLoopLength(freqs.Select(f => f * speed).ToArray());
            Func<float, float> q = hz => Quantize(hz * speed, length);

            string bodyPath = AnimationUtility.CalculateTransformPath(body, host);
            var rest = new RestPose(body);
            var bodyCurves = new TransformCurves();
            var partCurves = parts.Select(_ => new TransformCurves()).ToArray();

            int frames = Mathf.RoundToInt(length * FrameRate);
            for (int i = 0; i <= frames; i++)
            {
                float t = i / FrameRate;
                bodyCurves.Add(t, rest, EvaluateBody(motion, t, q));

                for (int k = 0; k < parts.Length; k++)
                {
                    var p = parts[k];
                    float rf = p.RotationFrequency > 0f ? p.RotationFrequency : 1f;
                    float mf = p.MoveFrequency > 0f ? p.MoveFrequency : 1f;
                    float angle = p.RotationAmplitude * Mathf.Sin(t * Tau * q(rf) + p.Phase);

                    var rotation = Quaternion.Euler(0f, 0f, angle);
                    Vector3 pivot = p.Pivot;
                    Vector3 pivotShift = pivot - rotation * pivot;
                    float mw = Mathf.Sin(t * Tau * q(mf) + p.Phase);
                    float mw2 = Mathf.Cos(t * Tau * q(mf * 0.8f) + p.Phase);
                    var pos = p.RestPosition + pivotShift + new Vector3(p.MoveAmplitude.x * mw2, p.MoveAmplitude.y * mw, 0f);
                    partCurves[k].AddRaw(t, pos, angle, Vector3.one);
                }
            }

            clip.ClearCurves();
            clip.frameRate = FrameRate;
            bodyCurves.Apply(clip, bodyPath);
            for (int k = 0; k < parts.Length; k++)
            {
                partCurves[k].Apply(clip, parts[k].Path, includeScale: false);
            }

            SetLoop(clip, true);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static IEnumerable<float> BodyFrequencies(Motion motion) => motion switch
        {
            Motion.Slime => new[] { 0.9f },
            Motion.Float => new[] { 0.35f, 0.22f },
            Motion.Ghost => new[] { 0.3f, 0.2f, 0.15f, 0.6f },
            Motion.Jelly => new[] { 0.7f, 0.35f, 0.25f },
            Motion.Walk => new[] { 0.7f },
            Motion.HeavyWalk => new[] { 0.35f },
            Motion.Bat => new[] { 3.2f, 0.4f },
            Motion.Bones => new[] { 0.3f },
            _ => new[] { 1f },
        };

        // MonsterAnimator.AnimateBody와 같은 식. q()는 루프에 맞게 보정된 주파수.
        private static BodyPose EvaluateBody(Motion motion, float t, Func<float, float> q)
        {
            float y = 0f, rot = 0f, sx = 1f, sy = 1f, x = 0f;
            switch (motion)
            {
                case Motion.Slime:
                    {
                        float s = Mathf.Sin(t * Tau * q(0.9f));
                        sy = 1f + 0.12f * s;
                        sx = 1f - 0.08f * s;
                        y = Mathf.Max(0f, -s) * 0.04f;
                        break;
                    }
                case Motion.Float:
                    y = Mathf.Sin(t * Tau * q(0.35f)) * 0.06f;
                    rot = Mathf.Sin(t * Tau * q(0.22f)) * 2f;
                    break;
                case Motion.Ghost:
                    y = Mathf.Sin(t * Tau * q(0.3f)) * 0.08f;
                    rot = Mathf.Sin(t * Tau * q(0.2f)) * 4f;
                    x = Mathf.Sin(t * Tau * q(0.15f)) * 0.04f;
                    sx = 1f + 0.03f * Mathf.Sin(t * Tau * q(0.6f));
                    sy = 1f - 0.03f * Mathf.Sin(t * Tau * q(0.6f));
                    break;
                case Motion.Jelly:
                    {
                        float p = Mathf.Sin(t * Tau * q(0.7f));
                        sx = 1f + 0.06f * p;
                        sy = 1f - 0.06f * p;
                        y = Mathf.Sin(t * Tau * q(0.35f)) * 0.05f;
                        rot = Mathf.Sin(t * Tau * q(0.25f)) * 3f;
                        break;
                    }
                case Motion.Walk:
                case Motion.HeavyWalk:
                    {
                        bool heavy = motion == Motion.HeavyWalk;
                        float step = Mathf.Sin(t * Tau * q(heavy ? 0.35f : 0.7f));
                        float bounce = Mathf.Abs(step);
                        y = bounce * (heavy ? 0.03f : 0.04f);
                        rot = step * (heavy ? 3.5f : 2.5f);
                        x = step * (heavy ? 0.03f : 0.015f);
                        float squash = (1f - bounce) * (heavy ? 0.05f : 0.04f);
                        sy = 1f - squash;
                        sx = 1f + squash * 0.6f;
                        break;
                    }
                case Motion.Bat:
                    y = Mathf.Sin(t * Tau * q(3.2f) + 0.6f) * 0.05f + Mathf.Sin(t * Tau * q(0.4f)) * 0.05f;
                    break;
                case Motion.Bones:
                    y = Mathf.Sin(t * Tau * q(0.3f)) * 0.04f;
                    break;
            }

            return new BodyPose { X = x, Y = y, Rot = rot, Sx = sx, Sy = sy };
        }

        private static AnimationClip BakeBreathing(AnimationClip clip, Transform body, Transform host)
        {
            const float length = 2f;
            var rest = new RestPose(body);
            var curves = new TransformCurves();
            int frames = Mathf.RoundToInt(length * FrameRate);
            for (int i = 0; i <= frames; i++)
            {
                float t = i / FrameRate;
                float s = Mathf.Sin(t * Tau / length);
                curves.Add(t, rest, new BodyPose { Sx = 1f - 0.015f * s, Sy = 1f + 0.025f * s, Y = 0.005f * (s + 1f) });
            }

            clip.ClearCurves();
            clip.frameRate = FrameRate;
            curves.Apply(clip, AnimationUtility.CalculateTransformPath(body, host));
            SetLoop(clip, true);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ---------- Hit / Die ----------

        private static readonly Color HitTint = new(1f, 0.35f, 0.35f, 1f);

        private static AnimationClip BakeHit(AnimationClip clip, Transform body, Transform host, SpriteRenderer[] renderers)
        {
            var rest = new RestPose(body);
            var curves = new TransformCurves();
            curves.Add(0f, rest, Pose(1f, 1f));
            curves.Add(0.05f, rest, Pose(1.15f, 0.85f, y: 0.05f));
            curves.Add(0.15f, rest, Pose(0.95f, 1.05f, y: 0.02f));
            curves.Add(0.3f, rest, Pose(1f, 1f));

            clip.ClearCurves();
            clip.frameRate = FrameRate;
            curves.Apply(clip, AnimationUtility.CalculateTransformPath(body, host), smooth: false);
            foreach (var r in renderers)
            {
                var c = r.color;
                SetColorCurves(clip, AnimationUtility.CalculateTransformPath(r.transform, host),
                    (0f, c), (0.04f, c * HitTint), (0.3f, c));
            }

            SetLoop(clip, false);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimationClip BakeDie(AnimationClip clip, Transform body, Transform host, SpriteRenderer[] renderers)
        {
            var rest = new RestPose(body);
            var curves = new TransformCurves();
            curves.Add(0f, rest, Pose(1f, 1f));
            curves.Add(0.08f, rest, Pose(1.25f, 0.75f));
            curves.Add(0.2f, rest, Pose(0.9f, 1.15f, y: 0.08f, rot: -8f));
            curves.Add(0.6f, rest, Pose(0.05f, 0.05f, y: 0.12f, rot: -40f));

            clip.ClearCurves();
            clip.frameRate = FrameRate;
            curves.Apply(clip, AnimationUtility.CalculateTransformPath(body, host), smooth: false);
            foreach (var r in renderers)
            {
                var c = r.color;
                var faded = new Color(c.r, c.g, c.b, 0f);
                SetColorCurves(clip, AnimationUtility.CalculateTransformPath(r.transform, host),
                    (0f, c), (0.06f, c * HitTint), (0.25f, c), (0.6f, faded));
            }

            SetLoop(clip, false);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static BodyPose Pose(float sx, float sy, float y = 0f, float rot = 0f) =>
            new() { Sx = sx, Sy = sy, Y = y, Rot = rot };

        private static void SetColorCurves(AnimationClip clip, string path, params (float t, Color c)[] keys)
        {
            string[] channels = { "m_Color.r", "m_Color.g", "m_Color.b", "m_Color.a" };
            for (int ch = 0; ch < 4; ch++)
            {
                var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.c[ch])).ToArray());
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }

                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(SpriteRenderer), channels[ch]), curve);
            }
        }

        // ---------- Controller ----------

        internal static AnimatorController CreateController(string path, AnimationClip idle, AnimationClip move,
            AnimationClip hit, AnimationClip die)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(MovingParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(HitParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(DieParam, AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle", new Vector3(300f, 0f));
            var moveState = sm.AddState("Move", new Vector3(300f, 100f));
            var hitState = sm.AddState("Hit", new Vector3(560f, 50f));
            var dieState = sm.AddState("Die", new Vector3(560f, 180f));
            idleState.motion = idle;
            moveState.motion = move;
            hitState.motion = hit;
            dieState.motion = die;
            sm.defaultState = idleState;

            var toMove = idleState.AddTransition(moveState);
            toMove.hasExitTime = false;
            toMove.duration = 0.1f;
            toMove.AddCondition(AnimatorConditionMode.If, 0f, MovingParam);

            var toIdle = moveState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, MovingParam);

            // Die를 먼저 추가해 Hit보다 우선 평가되게 한다.
            var anyToDie = sm.AddAnyStateTransition(dieState);
            anyToDie.hasExitTime = false;
            anyToDie.duration = 0f;
            anyToDie.canTransitionToSelf = false;
            anyToDie.AddCondition(AnimatorConditionMode.If, 0f, DieParam);

            var anyToHit = sm.AddAnyStateTransition(hitState);
            anyToHit.hasExitTime = false;
            anyToHit.duration = 0f;
            anyToHit.canTransitionToSelf = true;
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, HitParam);

            var hitToIdle = hitState.AddTransition(idleState);
            hitToIdle.hasExitTime = true;
            hitToIdle.exitTime = 1f;
            hitToIdle.duration = 0.05f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ---------- helpers ----------

        private static float ChooseLoopLength(float[] freqs)
        {
            float best = 1f, bestErr = float.MaxValue;
            for (float len = 1f; len <= 10.001f; len += 0.05f)
            {
                float err = freqs.Max(f => Mathf.Abs(Quantize(f, len) - f) / f);
                if (err <= 0.06f)
                {
                    return len; // 가장 짧은, 충분히 정확한 루프
                }

                if (err < bestErr)
                {
                    bestErr = err;
                    best = len;
                }
            }

            return best;
        }

        private static float Quantize(float hz, float length) => Mathf.Max(1f, Mathf.Round(hz * length)) / length;

        internal static void SetLoop(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        internal static AnimationClip LoadOrCreateClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                return clip;
            }

            clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private readonly struct RestPose
        {
            public readonly Vector3 Position;
            public readonly Vector3 Scale;

            public RestPose(Transform t)
            {
                Position = t.localPosition;
                Scale = t.localScale;
            }
        }

        private sealed class TransformCurves
        {
            private readonly List<(float t, Vector3 pos, float rot, Vector3 scale)> _keys = new();

            public void Add(float t, RestPose rest, BodyPose pose) =>
                _keys.Add((t, rest.Position + new Vector3(pose.X, pose.Y, 0f), pose.Rot,
                    new Vector3(rest.Scale.x * pose.Sx, rest.Scale.y * pose.Sy, rest.Scale.z)));

            public void AddRaw(float t, Vector3 pos, float rot, Vector3 scale) => _keys.Add((t, pos, rot, scale));

            public void Apply(AnimationClip clip, string path, bool includeScale = true, bool smooth = true)
            {
                Set(clip, path, "m_LocalPosition.x", k => k.pos.x, smooth);
                Set(clip, path, "m_LocalPosition.y", k => k.pos.y, smooth);
                Set(clip, path, "m_LocalPosition.z", k => k.pos.z, smooth);
                Set(clip, path, "localEulerAnglesRaw.x", _ => 0f, smooth);
                Set(clip, path, "localEulerAnglesRaw.y", _ => 0f, smooth);
                Set(clip, path, "localEulerAnglesRaw.z", k => k.rot, smooth);
                if (!includeScale)
                {
                    return;
                }

                Set(clip, path, "m_LocalScale.x", k => k.scale.x, smooth);
                Set(clip, path, "m_LocalScale.y", k => k.scale.y, smooth);
                Set(clip, path, "m_LocalScale.z", k => k.scale.z, smooth);
            }

            private void Set(AnimationClip clip, string path, string property,
                Func<(float t, Vector3 pos, float rot, Vector3 scale), float> value, bool smooth)
            {
                var keys = new Keyframe[_keys.Count];
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i] = new Keyframe(_keys[i].t, value(_keys[i]));
                }

                var curve = new AnimationCurve(keys);
                if (smooth)
                {
                    // 촘촘히 샘플링한 값이므로 중앙차분 기울기를 그대로 탄젠트로 쓴다. 양 끝은 루프가 이어지도록 맞춘다.
                    int n = keys.Length;
                    for (int i = 0; i < n; i++)
                    {
                        int prev = i > 0 ? i - 1 : n - 2;
                        int next = i < n - 1 ? i + 1 : 1;
                        float dt = 2f / FrameRate;
                        float slope = n > 2 ? (keys[next].value - keys[prev].value) / dt : 0f;
                        keys[i].inTangent = slope;
                        keys[i].outTangent = slope;
                    }

                    curve.keys = keys;
                }
                else
                {
                    for (int i = 0; i < curve.length; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    }

                    for (int i = 0; i < curve.length; i++)
                    {
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    }
                }

                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
            }
        }
    }
}
