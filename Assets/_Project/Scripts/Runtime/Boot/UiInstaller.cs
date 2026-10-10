using Cysharp.Threading.Tasks;
using Game.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    internal sealed class UiInstaller : IInstaller
    {
        private readonly UiHost _host;

        public UiInstaller(UiHost host)
        {
            _host = host;
        }

        public void Install(IContainerBuilder builder)
        {
            if (_host == null)
            {
                return; // Development scenes without a UI host.
            }

            var registry = new UiRegistry();
            builder.RegisterInstance<IUiRegistry>(registry);
            builder.Register<IUiPrefabProvider, DirectUiPrefabProvider>(Lifetime.Scoped);
            builder.RegisterBuildCallback(resolver => _host.BuildAsync(resolver.Resolve<IUiPrefabProvider>(), registry,
                resolver.InjectGameObject, _host.GetCancellationTokenOnDestroy()).Forget(Debug.LogException));
        }
    }
}
