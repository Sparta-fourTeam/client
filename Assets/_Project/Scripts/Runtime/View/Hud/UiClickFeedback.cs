using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.View
{
    /// <summary>화면을 누른 곳에 짧은 반짝임과 파동을 재생한다. UI 입력을 받지 않는 별도 캔버스를 쓴다.</summary>
    public sealed class UiClickFeedback : MonoBehaviour
    {
        [SerializeField] private RectTransform _burstPrefab;
        [SerializeField, Min(1)] private int _poolSize = 8;
        [SerializeField, Min(0.1f)] private float _burstSeconds = 0.7f;

        private RectTransform _canvasRect;
        private Burst[] _bursts;
        private int _nextBurst;

        private sealed class Burst
        {
            public RectTransform Rect;
            public ParticleSystem[] Particles;
            public float EndsAt;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<UiClickFeedback>() != null) { return; }
            var prefab = Resources.Load<GameObject>("UI/UiClickFeedback");
            if (prefab == null) { return; }
            DontDestroyOnLoad(Instantiate(prefab));
        }

        private void Awake()
        {
            _canvasRect = (RectTransform)transform;
            if (_burstPrefab == null) { return; }
            _bursts = new Burst[Mathf.Max(1, _poolSize)];
            for (int i = 0; i < _bursts.Length; i++)
            {
                var rect = Instantiate(_burstPrefab, transform, false);
                rect.gameObject.SetActive(false);
                _bursts[i] = new Burst
                {
                    Rect = rect,
                    Particles = rect.GetComponentsInChildren<ParticleSystem>(true)
                };
            }
        }

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                Play(pointer.position.ReadValue());
            }

            if (_bursts == null) { return; }
            foreach (var burst in _bursts)
            {
                if (burst.Rect.gameObject.activeSelf && Time.unscaledTime >= burst.EndsAt)
                {
                    Clear(burst);
                }
            }
        }

        /// <summary>화면 좌표에 효과를 재생한다. 빠르게 연속으로 눌러도 미리 만든 오브젝트를 재사용한다.</summary>
        public void Play(Vector2 screenPosition)
        {
            if (_bursts == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPosition, null, out var point)) { return; }

            var burst = _bursts[_nextBurst];
            _nextBurst = (_nextBurst + 1) % _bursts.Length;
            Clear(burst);
            burst.Rect.anchoredPosition = point;
            burst.Rect.gameObject.SetActive(true);
            burst.EndsAt = Time.unscaledTime + _burstSeconds;
            foreach (var particles in burst.Particles) { particles.Play(false); }
        }

        private void OnDisable()
        {
            if (_bursts == null) { return; }
            foreach (var burst in _bursts) { Clear(burst); }
        }

        private static void Clear(Burst burst)
        {
            foreach (var particles in burst.Particles)
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            burst.Rect.gameObject.SetActive(false);
        }
    }
}
