using Game.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class LobbyLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // RegisterComponentInHierarchy<T>()는 씬에서 찾은 첫 번째 인스턴스만 주입하므로,
            // 버튼이 여러 개일 수 있는 SceneNavButton은 전부 수동으로 주입한다.
            builder.RegisterBuildCallback(container =>
            {
                foreach (var button in FindObjectsByType<SceneNavButton>(FindObjectsSortMode.None))
                {
                    container.Inject(button);
                }
            });
        }
    }
}
