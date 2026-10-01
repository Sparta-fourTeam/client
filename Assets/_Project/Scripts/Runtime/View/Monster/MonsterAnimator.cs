using System;
using UnityEngine;

namespace Game.View.Monster
{
    public enum MonsterMotion
    {
        Slime,
        Float,
        Ghost,
        Jelly,
        Walk,
        HeavyWalk,
        Bat,
        Bones,
    }

    /// <summary>
    /// Procedural idle/move animation for hand-drawn monster sprites.
    /// Drives a "Body" transform (whole-body motion) and optional sub-parts (wings, bones).
    /// </summary>
    public sealed class MonsterAnimator : MonoBehaviour
    {
        [Serializable]
        public struct Part
        {
            public Transform Target;
            [Tooltip("Rotation pivot in the Body's local space.")]
            public Vector2 Pivot;
            public float RotationAmplitude;
            public float RotationFrequency;
            public Vector2 MoveAmplitude;
            public float MoveFrequency;
            public float Phase;
        }

        [SerializeField] private MonsterMotion _motion = MonsterMotion.Float;
        [SerializeField] private Transform _body;
        [SerializeField] private Part[] _parts = Array.Empty<Part>();
        [SerializeField, Min(0f)] private float _speed = 1f;

        private Vector3 _bodyPos;
        private Vector3 _bodyScale;
        private Vector3[] _partPos;
        private float _seed;

        private void Awake()
        {
            if (_body == null)
            {
                _body = transform;
            }

            _bodyPos = _body.localPosition;
            _bodyScale = _body.localScale;
            _partPos = new Vector3[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i].Target != null)
                {
                    _partPos[i] = _parts[i].Target.localPosition;
                }
            }

            _seed = UnityEngine.Random.value * 10f;
        }

        private void Update()
        {
            float t = (Time.time + _seed) * _speed;
            AnimateBody(t);
            AnimateParts(t);
        }

        private void AnimateBody(float t)
        {
            const float Tau = Mathf.PI * 2f;
            float y = 0f, rot = 0f, sx = 1f, sy = 1f, x = 0f;

            switch (_motion)
            {
                case MonsterMotion.Slime:
                    {
                        float s = Mathf.Sin(t * Tau * 0.9f);
                        sy = 1f + 0.12f * s;
                        sx = 1f - 0.08f * s;
                        y = Mathf.Max(0f, -s) * 0.04f;
                        break;
                    }
                case MonsterMotion.Float:
                    y = Mathf.Sin(t * Tau * 0.35f) * 0.06f;
                    rot = Mathf.Sin(t * Tau * 0.22f) * 2f;
                    break;
                case MonsterMotion.Ghost:
                    y = Mathf.Sin(t * Tau * 0.3f) * 0.08f;
                    rot = Mathf.Sin(t * Tau * 0.2f) * 4f;
                    x = Mathf.Sin(t * Tau * 0.15f) * 0.04f;
                    sx = 1f + 0.03f * Mathf.Sin(t * Tau * 0.6f);
                    sy = 1f - 0.03f * Mathf.Sin(t * Tau * 0.6f);
                    break;
                case MonsterMotion.Jelly:
                    {
                        float p = Mathf.Sin(t * Tau * 0.7f);
                        sx = 1f + 0.06f * p;
                        sy = 1f - 0.06f * p;
                        y = Mathf.Sin(t * Tau * 0.35f) * 0.05f;
                        rot = Mathf.Sin(t * Tau * 0.25f) * 3f;
                        break;
                    }
                case MonsterMotion.Walk:
                case MonsterMotion.HeavyWalk:
                    {
                        bool heavy = _motion == MonsterMotion.HeavyWalk;
                        float f = heavy ? 0.7f : 1.4f;
                        float step = Mathf.Sin(t * Tau * f * 0.5f);      // left/right foot
                        float bounce = Mathf.Abs(step);
                        y = bounce * (heavy ? 0.03f : 0.04f);
                        rot = step * (heavy ? 3.5f : 2.5f);
                        x = step * (heavy ? 0.03f : 0.015f);
                        float squash = (1f - bounce) * (heavy ? 0.05f : 0.04f);
                        sy = 1f - squash;
                        sx = 1f + squash * 0.6f;
                        break;
                    }
                case MonsterMotion.Bat:
                    y = Mathf.Sin(t * Tau * 3.2f + 0.6f) * 0.05f + Mathf.Sin(t * Tau * 0.4f) * 0.05f;
                    break;
                case MonsterMotion.Bones:
                    y = Mathf.Sin(t * Tau * 0.3f) * 0.04f;
                    break;
            }

            _body.localPosition = _bodyPos + new Vector3(x, y, 0f);
            _body.localRotation = Quaternion.Euler(0f, 0f, rot);
            _body.localScale = new Vector3(_bodyScale.x * sx, _bodyScale.y * sy, _bodyScale.z);
        }

        private void AnimateParts(float t)
        {
            const float Tau = Mathf.PI * 2f;
            for (int i = 0; i < _parts.Length; i++)
            {
                ref readonly Part p = ref _parts[i];
                if (p.Target == null)
                {
                    continue;
                }

                float rf = p.RotationFrequency > 0f ? p.RotationFrequency : 1f;
                float mf = p.MoveFrequency > 0f ? p.MoveFrequency : 1f;
                float wave = Mathf.Sin(t * Tau * rf + p.Phase);
                float angle = p.RotationAmplitude * wave;

                var rotation = Quaternion.Euler(0f, 0f, angle);
                Vector3 pivot = p.Pivot;
                Vector3 pivotShift = pivot - rotation * pivot;
                float mw = Mathf.Sin(t * Tau * mf + p.Phase);
                float mw2 = Mathf.Cos(t * Tau * mf * 0.8f + p.Phase);
                var move = new Vector3(p.MoveAmplitude.x * mw2, p.MoveAmplitude.y * mw, 0f);

                p.Target.localRotation = rotation;
                p.Target.localPosition = _partPos[i] + pivotShift + move;
            }
        }
    }
}
