using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    /// <summary>Owns inactive and in-flight effects so battle teardown releases both.</summary>
    internal sealed class SkillObjectPool<T> : IDisposable where T : Component
    {
        private readonly HashSet<T> instances = new();
        public ObjectPool<T> Pool { get; }
        public SkillObjectPool(Func<T> create, int capacity, int maxSize)
        {
            Pool = new ObjectPool<T>(() => { var item = create(); instances.Add(item); return item; },
                item => item.gameObject.SetActive(true), item => item.gameObject.SetActive(false),
                DestroyItem, true, capacity, maxSize);
        }
        private void DestroyItem(T item)
        {
            instances.Remove(item);
            if (item == null) { return; }
            item.gameObject.SetActive(false);
            if (Application.isPlaying) { UnityEngine.Object.Destroy(item.gameObject); }
            else { UnityEngine.Object.DestroyImmediate(item.gameObject); }
        }
        public void Dispose()
        {
            Pool.Clear();
            foreach (var item in new List<T>(instances)) { DestroyItem(item); }
        }
    }
}
