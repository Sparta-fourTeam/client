using System;
using UnityEngine;

namespace Game.View
{
    // Prefabs stay fully active for editing. Apply the authored starting state before binding/subscriptions.
    [DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class UiInitialVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject[] _hiddenOnStart = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] _shownOnStart = Array.Empty<GameObject>();
        private bool _initialized;

        public static void InitializeTree(GameObject root)
        {
            var states = root.GetComponentsInChildren<UiInitialVisibility>(true);
            for (int i = states.Length - 1; i >= 0; i--)
            {
                states[i].Initialize();
            }
        }

        private void Awake() => InitializeTree(gameObject);

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            foreach (var target in _hiddenOnStart)
            {
                if (target != null)
                {
                    target.SetActive(false);
                }
            }

            foreach (var target in _shownOnStart)
            {
                if (target != null)
                {
                    target.SetActive(true);
                }
            }
        }
    }
}
