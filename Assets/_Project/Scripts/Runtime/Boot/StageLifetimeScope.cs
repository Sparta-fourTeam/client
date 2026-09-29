using Game.Core;
using Game.Core.Defense;
using Game.Core.Stage;
using VContainer;
using VContainer.Unity;


namespace Game.Boot
{
    public sealed class StageLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<Wall>();
            builder.RegisterEntryPoint<StageJudge>();
            builder.RegisterEntryPoint<WaveProgress>();
        }
    }
}
