using Game.Core;
using Game.Network;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe();

            // 진단 창(Diagnostics window) 및 전역 기능을 활성화하기 위해 GlobalMessagePipe를 설정합니다.
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));

            builder.Register<GameDataStore>(Lifetime.Singleton);
            builder.Register<LocalSaveStore>(Lifetime.Singleton);
            builder.Register<PlayerProfile>(Lifetime.Singleton);

            // 서버가 붙으면 이 7줄만 Http*Api로 교체하면 된다
            builder.Register<IAuthApi, LocalAuthApi>(Lifetime.Singleton);
            builder.Register<IDataApi, LocalDataApi>(Lifetime.Singleton);
            builder.Register<IPlayerApi, LocalPlayerApi>(Lifetime.Singleton);
            builder.Register<IBattleApi, LocalBattleApi>(Lifetime.Singleton);
            builder.Register<IUpgradeApi, LocalUpgradeApi>(Lifetime.Singleton);
            builder.Register<IEnergyApi, LocalEnergyApi>(Lifetime.Singleton);
            builder.Register<IStageApi, LocalStageApi>(Lifetime.Singleton);
        }
    }
}
