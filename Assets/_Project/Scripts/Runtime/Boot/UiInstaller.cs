using Cysharp.Threading.Tasks;
using Game.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    internal static class UiInstaller
    {
        public static void Install(IContainerBuilder builder, UiHost host)
        {
            if (host == null)
            {
                return; // Development scenes without a UI host.
            }

            var registry = new UiRegistry();
            builder.RegisterInstance<IUiRegistry>(registry);
            builder.Register<IUiPrefabProvider, DirectUiPrefabProvider>(Lifetime.Scoped);
            builder.RegisterBuildCallback(resolver => host.BuildAsync(resolver.Resolve<IUiPrefabProvider>(), registry,
                resolver.InjectGameObject, host.GetCancellationTokenOnDestroy()).Forget(Debug.LogException));
        }
    }
}
