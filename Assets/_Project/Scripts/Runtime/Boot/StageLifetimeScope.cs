using Game.Core.Defense;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class StageLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<Wall>();
        }
    }
}
