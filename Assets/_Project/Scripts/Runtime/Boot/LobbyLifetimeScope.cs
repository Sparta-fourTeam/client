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
            builder.RegisterEntryPoint<LobbyModel>();
            builder.RegisterEntryPoint<EnergyClock>();
            builder.Register<BattleLauncher>(Lifetime.Scoped);
            builder.Register<EnergyRecovery>(Lifetime.Scoped);
        }
    }
}
