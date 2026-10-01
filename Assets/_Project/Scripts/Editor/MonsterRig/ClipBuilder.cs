using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.MonsterRig
{
    /// <summary>
    /// 본 키프레임 AnimationClip 작성 도우미.
    /// Sampled: 시간 함수를 30fps로 샘플링 (루프 클립은 양 끝 탄젠트를 이어 붙임)
    /// Keyed: 몇 개의 키로 직접 지정 (ClampedAuto 탄젠트)
    /// </summary>
    internal sealed class ClipBuilder
    {
        public const float FrameRate = 30f;
        private readonly AnimationClip _clip;

        public ClipBuilder(AnimationClip clip, bool loop)
        {
            _clip = clip;
            _clip.ClearCurves();
            _clip.frameRate = FrameRate;
            MonsterAnimationBaker.SetLoop(_clip, loop);
        }

        public AnimationClip Clip => _clip;

        public void Sampled(string path, Type type, string property, float length, Func<float, float> f, bool loop)
        {
            int frames = Mathf.Max(2, Mathf.RoundToInt(length * FrameRate));
            var keys = new Keyframe[frames + 1];
            for (int i = 0; i <= frames; i++)
            {
                float t = length * i / frames;
                keys[i] = new Keyframe(t, f(t));
            }

            float dt = length / frames;
            for (int i = 0; i <= frames; i++)
            {
                int prev = i > 0 ? i - 1 : (loop ? frames - 1 : 0);
                int next = i < frames ? i + 1 : (loop ? 1 : frames);
                float span = (prev == i || next == i) ? dt : 2f * dt;
                float slope = (keys[next].value - keys[prev].value) / span;
                keys[i].inTangent = slope;
                keys[i].outTangent = slope;
            }

            Set(path, type, property, new AnimationCurve(keys));
        }

        // 키 keyCount개만 균등하게 찍고, 탄젠트는 함수의 실제 기울기로 넣는다. (루프 끝 = 시작)
        public void Sparse(string path, Type type, string property, float length, Func<float, float> f, int keyCount,
            bool loop = true)
        {
            const float eps = 1f / 240f;
            var keys = new Keyframe[keyCount + 1];
            for (int i = 0; i <= keyCount; i++)
            {
                float t = length * i / keyCount;
                float slope = (f(t + eps) - f(t - eps)) / (2f * eps);
                keys[i] = new Keyframe(t, f(loop && i == keyCount ? 0f : t), slope, slope);
            }

            Set(path, type, property, new AnimationCurve(keys));
        }

        // 값과 기울기를 직접 준 키 (예: 낙하는 착지 키에 실제 속도를 넣으면 키 2개로 포물선이 된다)
        public void Explicit(string path, Type type, string property, IReadOnlyList<(float t, float v, float slope)> keys)
        {
            var frames = new Keyframe[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                frames[i] = new Keyframe(keys[i].t, keys[i].v, keys[i].slope, keys[i].slope);
            }

            Set(path, type, property, new AnimationCurve(frames));
        }

        public void Keyed(string path, Type type, string property, IReadOnlyList<(float t, float v)> keys)
        {
            var curve = new AnimationCurve();
            foreach (var (t, v) in keys)
            {
                curve.AddKey(new Keyframe(t, v));
            }

            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }

            Set(path, type, property, curve);
        }

        public void Finish() => EditorUtility.SetDirty(_clip);

        private void Set(string path, Type type, string property, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(_clip, EditorCurveBinding.FloatCurve(path, type, property), curve);

        public static float SignedAngle(float degrees)
        {
            degrees %= 360f;
            return degrees > 180f ? degrees - 360f : degrees;
        }
    }
}
