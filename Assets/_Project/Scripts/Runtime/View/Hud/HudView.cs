using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    public abstract class HudView : MonoBehaviour, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _initialized;
        private bool _disposed;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            UiInitialVisibility.InitializeTree(gameObject);
            InitializeView();
        }

        protected virtual void Awake() => Initialize();
        protected virtual void InitializeView() { }

        protected void Track(IDisposable subscription)
        {
            _subscriptions.Add(subscription);
        }
        protected virtual void DisposeView() { }
        protected virtual void OnDestroy() => Dispose();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeView();
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
