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
        }
    }
}
