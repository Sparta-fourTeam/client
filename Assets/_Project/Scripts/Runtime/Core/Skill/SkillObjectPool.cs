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
        private readonly Dictionary<T, ParticleSystem[]> particles = new();
        private readonly Dictionary<T, TrailRenderer[]> trails = new();
        public ObjectPool<T> Pool { get; }
        public SkillObjectPool(Func<T> create, int capacity, int maxSize)
        {
            Pool = new ObjectPool<T>(() =>
                {
                    var item = create();
                    instances.Add(item);
                    particles[item] = item.GetComponentsInChildren<ParticleSystem>(true);
                    trails[item] = item.GetComponentsInChildren<TrailRenderer>(true);
                    return item;
                },
                item => item.gameObject.SetActive(true), ReleaseItem,
                DestroyItem, true, capacity, maxSize);
        }
        private void ReleaseItem(T item)
        {
            foreach (var particle in particles[item])
            {
                if (particle != null) { particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); }
            }
            foreach (var trail in trails[item]) { if (trail != null) { trail.Clear(); } }
            item.gameObject.SetActive(false);
        }

        private void DestroyItem(T item)
        {
            instances.Remove(item);
            particles.Remove(item);
            trails.Remove(item);
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
