using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.View
{
    [DisallowMultipleComponent]
    public sealed class UiHost : MonoBehaviour
    {
        [SerializeField] private UiComposition _composition;
        private readonly List<GameObject> _instances = new();
        private readonly List<UiPrefabLease> _leases = new();
        private readonly UniTaskCompletionSource _ready = new();
        private bool _started;
        private UiRegistry _registry;
        public UiComposition Composition => _composition;
        public UniTask WhenReady => _ready.Task;

        public async UniTask BuildAsync(IUiPrefabProvider provider, UiRegistry registry, Action<GameObject> inject,
            CancellationToken cancellationToken)
        {
            if (_started)
            {
                throw new InvalidOperationException("UI host is already building or built.");
            }

            _started = true;
            _registry = registry;
            GameObject staging = null;
            try
            {
                if (_composition == null)
                {
                    throw new InvalidOperationException($"{name}: missing UI composition.");
                }

                _composition.Validate();
                foreach (var entry in _composition.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.ParentPath) && transform.Find(entry.ParentPath) == null)
                    {
                        throw new InvalidOperationException($"{entry.Id}: missing layer {entry.ParentPath}.");
                    }
                }

                var camera = GetSceneCamera();
                foreach (var canvas in GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.worldCamera = camera;
                    }
                }

                staging = new GameObject("UI staging");
                staging.SetActive(false);
                staging.transform.SetParent(transform, false);
                foreach (var entry in _composition.Entries)
                {
                    var lease = await provider.LoadAsync(entry, cancellationToken);
                    _leases.Add(lease);
                    cancellationToken.ThrowIfCancellationRequested();
                    var instance = Instantiate(lease.Prefab, staging.transform, false);
                    instance.name = entry.Id;
                    instance.SetActive(false);
                    _instances.Add(instance);
                    registry.Add(instance);
                }

                foreach (var instance in _instances)
                {
                    UiInitialVisibility.InitializeTree(instance);
                }

                // Bind and initialize every screen before any buffered message can be replayed.
                foreach (var instance in _instances)
                {
                    foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (behaviour is IUiBindable bindable)
                        {
                            bindable.BindUi(registry);
                        }
                    }
                }

                foreach (var instance in _instances)
                {
                    foreach (var view in instance.GetComponentsInChildren<HudView>(true))
                    {
                        view.Initialize();
                    }
                }

                foreach (var instance in _instances)
                {
                    inject(instance);
                }

                cancellationToken.ThrowIfCancellationRequested();

                for (int i = 0; i < _instances.Count; i++)
                {
                    var entry = _composition.Entries[i];
                    var parent = string.IsNullOrEmpty(entry.ParentPath) ? transform : transform.Find(entry.ParentPath);
                    if (parent == null)
                    {
                        throw new InvalidOperationException($"{entry.Id}: missing layer {entry.ParentPath}.");
                    }

                    _instances[i].transform.SetParent(parent, false);
                    _instances[i].transform.SetSiblingIndex(entry.SiblingIndex);
                    foreach (var canvas in _instances[i].GetComponentsInChildren<Canvas>(true))
                    {
                        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        {
                            canvas.worldCamera = camera;
                        }
                    }

                    _instances[i].SetActive(true);
                }
                _ready.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                registry.Clear();
                Release();
                _ready.TrySetCanceled(cancellationToken);
                throw;
            }
            catch (Exception error)
            {
                registry.Clear();
                Release();
                _ready.TrySetException(error);
                throw;
            }
            finally
            {
                if (staging != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(staging);
                    }
                    else
                    {
                        DestroyImmediate(staging);
                    }
                }
            }
        }

        private void OnDestroy() => Release();

        private Camera GetSceneCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.CompareTag("MainCamera"))
                    {
                        return camera;
                    }
                }
            }

            return null;
        }

        private void Release()
        {
            _registry?.Clear();
            // Unity destroys children at frame end; release backing assets after that destruction.
            foreach (var instance in _instances)
            {
                if (instance != null)
                {
                    foreach (var view in instance.GetComponentsInChildren<HudView>(true))
                    {
                        view.Dispose();
                    }

                    instance.SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(instance);
                    }
                    else
                    {
                        DestroyImmediate(instance);
                    }
                }
            }

            _instances.Clear();
            var leases = _leases.ToArray();
            _leases.Clear();
            ReleaseAfterDestroy(leases).Forget(Debug.LogException);
        }

        private static async UniTask ReleaseAfterDestroy(UiPrefabLease[] leases)
        {
            if (leases.Length == 0)
            {
                return;
            }

            if (Application.isPlaying)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            }

            foreach (var lease in leases)
            {
                lease.Dispose();
            }
        }
    }
}
