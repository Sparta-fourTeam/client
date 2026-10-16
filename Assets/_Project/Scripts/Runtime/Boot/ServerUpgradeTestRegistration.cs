using Game.Core;
using Game.Network;
using VContainer;

namespace Game.Boot
{
    public static class ServerUpgradeTestRegistration
    {
        public static void Register(IContainerBuilder builder)
        {
            builder.Register<ServerUpgradeTestBackend>(Lifetime.Singleton).AsSelf().As<IPlayerApi>().As<IUpgradeApi>()
                .As<IBattleApi>().As<IEnergyApi>().As<IStageApi>().As<ICodexApi>().As<IQuestApi>().As<IShopApi>()
                .As<ITutorialApi>().As<IAccountApi>().As<IBattleCheckpointStore>().As<ISubmitFaultSwitch>();
        }
    }
}
