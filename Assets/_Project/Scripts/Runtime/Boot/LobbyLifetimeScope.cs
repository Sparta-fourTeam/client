using Game.Core;
using Game.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class LobbyLifetimeScope : LifetimeScope
    {
        [SerializeField] private UiHost _uiHost;

        protected override void Configure(IContainerBuilder builder)
        {
            new UiInstaller(_uiHost).Install(builder);
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
