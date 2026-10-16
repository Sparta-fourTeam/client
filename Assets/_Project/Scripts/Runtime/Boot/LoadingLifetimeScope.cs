using Game.Core;
using Game.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class LoadingLifetimeScope : LifetimeScope
    {
        [SerializeField] private UiHost _uiHost;

        protected override void Configure(IContainerBuilder builder)
        {
            new UiInstaller(_uiHost).Install(builder);
            // 화면은 UiHost가 조립한 것을 레지스트리에서 꺼내 쓴다 (조립은 진입점보다 먼저 끝난다)
            builder.Register<ILoginPrompt>(resolver => resolver.Resolve<IUiRegistry>().Get<LoginView>(), Lifetime.Scoped);
            builder.Register(resolver => resolver.Resolve<IUiRegistry>().Get<TapToStartView>(), Lifetime.Scoped);
            builder.Register(resolver => resolver.Resolve<IUiRegistry>().Get<ConfirmPopupView>(), Lifetime.Scoped);
            builder.Register<LoginFlow>(Lifetime.Scoped);
            // 전투 튜토리얼 중이면 로그인 뒤 로비 대신 그 스테이지로 바로 들어간다
            builder.Register<BattleLauncher>(Lifetime.Scoped);
            builder.RegisterEntryPoint<LoadingFlow>();
        }
    }
}
