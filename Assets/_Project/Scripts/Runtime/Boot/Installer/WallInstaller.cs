using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using VContainer;

namespace Game.Boot.Installer
{
    public static class WallInstaller
    {
        public static void InstallWall(this IContainerBuilder builder, MessagePipeOptions options, int maxHp)
        {
            builder.RegisterMessageBroker<WallHpChanged>(options);
            builder.RegisterMessageBroker<WallDestroyed>(options);

            builder.Register<IWall, Wall>(Lifetime.Singleton).WithParameter("maxHp", maxHp);
        }
    }
}
