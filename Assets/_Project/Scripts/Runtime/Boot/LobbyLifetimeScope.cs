using Game.View;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class LobbyLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<SceneNavButton>();
        }
    }
}
