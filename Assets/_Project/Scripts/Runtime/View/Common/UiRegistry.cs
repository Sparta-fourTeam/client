using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    public interface IUiRegistry
    {
        T Get<T>() where T : Component;
        /// <summary>구성에 없을 수도 있는 선택 화면용. 없거나 둘 이상이면 null</summary>
        T TryGet<T>() where T : Component;
    }

    public interface IUiBindable
    {
        void BindUi(IUiRegistry registry);
    }

    public sealed class UiRegistry : IUiRegistry
    {
        private readonly Dictionary<Type, List<Component>> _components = new();

        public void Add(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null)
                {
                    throw new InvalidOperationException($"Missing script in {root.name}.");
                }

                var type = component.GetType();
                if (!_components.TryGetValue(type, out var list))
                {
                    _components.Add(type, list = new List<Component>());
                }

                if (!list.Contains(component))
                {
                    list.Add(component);
                }
            }
        }

        public T Get<T>() where T : Component
        {
            if (!_components.TryGetValue(typeof(T), out var list) || list.Count != 1 || list[0] == null)
            {
                throw new InvalidOperationException($"UI composition requires exactly one {typeof(T).Name}.");
            }

            return (T)list[0];
        }

        public T TryGet<T>() where T : Component =>
            _components.TryGetValue(typeof(T), out var list) && list.Count == 1 ? list[0] as T : null;

        public void Clear() => _components.Clear();
    }
}
