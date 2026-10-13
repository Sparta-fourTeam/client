using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.View
{
    // A lease owns the loaded asset, never an instance. Destroy instances before releasing leases.
    public interface IUiPrefabProvider
    {
        UniTask<UiPrefabLease> LoadAsync(UiComposition.Entry entry, CancellationToken cancellationToken);
    }

    public sealed class UiPrefabLease : IDisposable
    {
        public GameObject Prefab { get; }
        private Action _release;

        public UiPrefabLease(GameObject prefab, Action release = null)
        {
            Prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _release = release;
        }

        public void Dispose()
        {
            var release = _release;
            _release = null;
            release?.Invoke();
        }
    }

    public sealed class DirectUiPrefabProvider : IUiPrefabProvider
    {
        public UniTask<UiPrefabLease> LoadAsync(UiComposition.Entry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.FromResult(new UiPrefabLease(entry.Prefab));
        }
    }
}
