using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    public abstract class HudView : MonoBehaviour
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        protected void Track(IDisposable subscription)
        {
            _subscriptions.Add(subscription);
        }
        protected virtual void OnDestroy()
        {
            foreach (IDisposable subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
