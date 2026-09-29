using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class LoadingLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<LoadingFlow>();
        }
    }
}
