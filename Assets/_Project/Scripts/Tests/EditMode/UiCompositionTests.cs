using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class UiCompositionTests
    {
        private readonly List<Object> _objects = new();

        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private GameObject Root(string name) => Keep(new GameObject(name));

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void DuplicateIdsAndMissingAssetsAreRejected()
        {
            var composition = Keep(ScriptableObject.CreateInstance<UiComposition>());
            var prefab = Root("screen");
            composition.Entries = new[] { Entry("same", prefab), Entry("same", prefab) };
            Assert.Throws<InvalidOperationException>(composition.Validate);
            composition.Entries = new[] { Entry("missing", null) };
            Assert.Throws<InvalidOperationException>(composition.Validate);
        }

        [Test]
        public void RegistryRejectsAmbiguousScreensAndClearsReferences()
        {
            var registry = new UiRegistry();
            var one = Root("one").AddComponent<UiLifecycleProbe>();
            registry.Add(one.gameObject);
            Assert.That(registry.Get<UiLifecycleProbe>(), Is.SameAs(one));
            registry.Add(one.gameObject); // Registering the same hierarchy is idempotent.
            Assert.That(registry.Get<UiLifecycleProbe>(), Is.SameAs(one));
            registry.Add(Root("two").AddComponent<UiLifecycleProbe>().gameObject);
            Assert.Throws<InvalidOperationException>(() => registry.Get<UiLifecycleProbe>());
            registry.Clear();
            Assert.Throws<InvalidOperationException>(() => registry.Get<UiLifecycleProbe>());
        }

        [Test]
        public void HostInitializesBeforeMessageReplayAndDoesNotResetOnAwake()
        {
            var prefab = Root("screen");
            prefab.SetActive(false);
            prefab.AddComponent<UiLifecycleProbe>();
            var host = CreateHost(new[] { Entry("screen", prefab) });
            var registry = new UiRegistry();
            host.BuildAsync(new DirectUiPrefabProvider(), registry, go =>
            {
                var view = go.GetComponent<UiLifecycleProbe>();
                Assert.That(view.Initializations, Is.EqualTo(1));
                Assert.That(view.Bound, Is.True);
                view.Value = 42; // Simulate a buffered subscription replay during injection.
            }, CancellationToken.None).GetAwaiter().GetResult();
            var instance = registry.Get<UiLifecycleProbe>();
            instance.Initialize();
            Assert.That(instance.Value, Is.EqualTo(42));
            Assert.That(instance.Initializations, Is.EqualTo(1));
            Assert.That(instance.gameObject.activeSelf, Is.True);
            Assert.Throws<InvalidOperationException>(() => host.BuildAsync(new DirectUiPrefabProvider(), registry, _ => { }, CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void PartialLoadFailureReleasesPreviousLeaseAndRemovesInstances()
        {
            var prefab = Root("screen");
            prefab.SetActive(false);
            prefab.AddComponent<UiLifecycleProbe>();
            var host = CreateHost(new[] { Entry("one", prefab), Entry("two", prefab) });
            var registry = new UiRegistry();
            var provider = new FailingProvider(prefab);
            Assert.Throws<InvalidOperationException>(() => host.BuildAsync(provider, registry, _ => { }, CancellationToken.None).GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(() => host.WhenReady.GetAwaiter().GetResult());
            Assert.That(provider.Released, Is.EqualTo(1));
            Assert.That(host.transform.childCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => registry.Get<UiLifecycleProbe>());
        }

        [Test]
        public void CancellationBeforeLoadCreatesNothing()
        {
            var host = CreateHost(new[] { Entry("screen", Root("screen")) });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => host.BuildAsync(new DirectUiPrefabProvider(), new UiRegistry(), _ => { }, cancellation.Token).GetAwaiter().GetResult());
            Assert.Throws<OperationCanceledException>(() => host.WhenReady.GetAwaiter().GetResult());
            Assert.That(host.transform.childCount, Is.Zero);
        }

        [Test]
        public void LeaseAndViewCleanupAreIdempotent()
        {
            int released = 0;
            var root = Root("screen");
            using var lease = new UiPrefabLease(root, () => released++);
            lease.Dispose(); lease.Dispose();
            Assert.That(released, Is.EqualTo(1));
            var view = root.AddComponent<UiLifecycleProbe>();
            view.Dispose(); view.Dispose();
            Assert.That(view.Disposals, Is.EqualTo(1));
        }

        private UiHost CreateHost(UiComposition.Entry[] entries)
        {
            var composition = Keep(ScriptableObject.CreateInstance<UiComposition>());
            composition.Entries = entries;
            var host = Root("host").AddComponent<UiHost>();
            var so = new SerializedObject(host);
            so.FindProperty("_composition").objectReferenceValue = composition;
            so.ApplyModifiedPropertiesWithoutUndo();
            return host;
        }

        private static UiComposition.Entry Entry(string id, GameObject prefab) => new() { Id = id, Prefab = prefab };

        private sealed class FailingProvider : IUiPrefabProvider
        {
            private readonly GameObject _prefab;
            private int _loads;
            public int Released;
            public FailingProvider(GameObject prefab) => _prefab = prefab;
            public UniTask<UiPrefabLease> LoadAsync(UiComposition.Entry entry, CancellationToken token)
            {
                if (++_loads == 2)
                {
                    throw new InvalidOperationException("Simulated load failure.");
                }

                return UniTask.FromResult(new UiPrefabLease(_prefab, () => Released++));
            }
        }
    }

    public sealed class UiLifecycleProbe : HudView, IUiBindable
    {
        public int Initializations;
        public int Disposals;
        public int Value;
        public bool Bound;
        public void BindUi(IUiRegistry registry) => Bound = registry.Get<UiLifecycleProbe>() == this;
        protected override void InitializeView() { Initializations++; Value = 0; }
        protected override void DisposeView() => Disposals++;
    }
}
