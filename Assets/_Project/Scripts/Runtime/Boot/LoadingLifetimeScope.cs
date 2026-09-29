using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class LoadingLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // RegisterEntryPoint(IStartable)는 씬 로드로 생성되는 자식 스코프에서는
            // VContainer의 커스텀 PlayerLoop 디스패치가 걸리지 않아 Start()가 호출되지 않는다.
            // 빌드 콜백에서 직접 호출해 우회한다.
            builder.Register<LoadingFlow>(Lifetime.Singleton);
            builder.RegisterBuildCallback(container => container.Resolve<LoadingFlow>().Start());
        }
    }
}
