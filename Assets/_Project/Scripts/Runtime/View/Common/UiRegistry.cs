using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.View
{
    public interface IUiRegistry
    {
        T Get<T>() where T : Component;

        /// <summary>같은 종류가 여러 개인 구성 요소(버튼마다 붙는 잠금 등)를 모두 돌려준다. 없으면 빈 목록</summary>
        IReadOnlyList<T> GetAll<T>() where T : Component;
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

        public IReadOnlyList<T> GetAll<T>() where T : Component =>
            _components.TryGetValue(typeof(T), out var list) ? list.Where(c => c != null).Cast<T>().ToList() : new List<T>();

        public void Clear() => _components.Clear();
    }
}
