using System.IO;
using Game.Core;
using Game.Network;
using Game.View;
using MessagePipe;
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
            builder.Register<IChatClient, HttpChatClient>(Lifetime.Scoped);
            builder.RegisterMessageBroker<ChatChanged>(new MessagePipeOptions { InstanceLifetime = InstanceLifetime.Scoped });
            builder.RegisterEntryPoint<ChatService>().AsSelf();
            builder.Register<IUtcClock, SystemUtcClock>(Lifetime.Singleton);
            builder.RegisterEntryPoint<LobbyProfileRefresher>().AsSelf();
            builder.RegisterEntryPoint<LobbyModel>();
            builder.RegisterEntryPoint<EnergyClock>();
            builder.Register<BattleLauncher>(Lifetime.Scoped);
            builder.Register<LobbyArrival>(Lifetime.Scoped);
            // 레벨업 팝업의 해금 줄 출처. 새 해금 종류(장비·기능 등)는 출처를 만들어 여기에 추가한다
            builder.Register<ILevelUnlockSource, WeaponUnlockSource>(Lifetime.Scoped);
            builder.Register<ILevelUnlockSource, EquipmentUnlockSource>(Lifetime.Scoped);
            builder.Register<EnergyRecovery>(Lifetime.Scoped);
            // TODO(data): 실제 데이터가 생기면 구현만 교체
            builder.Register<IGrowthCatalog, DummyGrowthCatalog>(Lifetime.Scoped);
            if (ServerConfig.UseServerProgress && _uiHost != null)
            {
                builder.RegisterInstance(_uiHost);
                builder.Register(c => new HttpShopApi(c.Resolve<HttpApiConfig>(), c.Resolve<IHttpTransport>(),
                    c.Resolve<IAuthTokenStore>()), Lifetime.Scoped);
                builder.Register(c => new ShopPendingRequestStore(Path.Combine(Application.persistentDataPath, "ServerPending"),
                    c.Resolve<IAuthTokenStore>()), Lifetime.Scoped);
                builder.Register<ServerShopViewSession>(Lifetime.Scoped).AsSelf().As<IServerShopViewSession>();
                builder.RegisterEntryPoint<ServerShopBinder>();
            }
        }
    }
}
