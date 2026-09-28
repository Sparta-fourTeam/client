using Game.Boot.Installer;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class StageLifetimeScope : LifetimeScope
    {
        [SerializeField] private int _wallMaxHp = 100;

        protected override void Configure(IContainerBuilder builder)
        {
            MessagePipeOptions options = Parent.Container.Resolve<MessagePipeOptions>();
            builder.InstallWall(options, _wallMaxHp);
        }
    }
}
