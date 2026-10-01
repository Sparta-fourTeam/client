using Game.Core;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class LobbyLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IUtcClock, SystemUtcClock>(Lifetime.Singleton);
            builder.RegisterEntryPoint<LobbyProfileRefresher>().AsSelf();
            builder.RegisterEntryPoint<LobbyModel>();
            builder.RegisterEntryPoint<EnergyClock>();
            builder.Register<BattleLauncher>(Lifetime.Scoped);
            builder.Register<EnergyRecovery>(Lifetime.Scoped);
            // TODO(data): 실제 데이터가 생기면 구현만 교체
            builder.Register<IGrowthCatalog, DummyGrowthCatalog>(Lifetime.Scoped);
        }
    }
}
