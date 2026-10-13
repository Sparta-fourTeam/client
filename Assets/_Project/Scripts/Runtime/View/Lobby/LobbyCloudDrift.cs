using System;
using UnityEngine;

namespace Game.View
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbyCloudDrift : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _speed = 8f;
        [SerializeField] private Vector2 _origin;
        private double _elapsed;

        private void OnEnable()
        {
            _elapsed = 0;
            SampleTime(0);
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            SampleTime(_elapsed);
        }

        // Runtime and isolated editor previews share the same deterministic motion.
        public void SampleTime(double seconds)
        {
            var cloud = (RectTransform)transform;
            if (cloud.parent is not RectTransform viewport || viewport.rect.width <= 0f) { return; }
            float width = cloud.rect.width * Mathf.Abs(cloud.localScale.x);
            float left = -cloud.anchorMin.x * viewport.rect.width - width * (1f - cloud.pivot.x) - 2f;
            float right = (1f - cloud.anchorMin.x) * viewport.rect.width + width * cloud.pivot.x + 2f;
            double span = right - left;
            double phase = (_origin.x - left + Math.Max(0, seconds) * _speed) % span;
            cloud.anchoredPosition = new Vector2(left + (float)phase, _origin.y);
        }
    }
}
