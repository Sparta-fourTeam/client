using Game.Core;
using Game.Network;
using Game.View;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class RootLifetimeScope : LifetimeScope
    {
        [SerializeField] private TransitionCurtain curtain;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe();

            builder.Register<GameDataStore>(Lifetime.Singleton);
            builder.Register<LocalSaveStore>(Lifetime.Singleton);
            builder.Register<PlayerProfile>(Lifetime.Singleton);

            // 서버가 붙으면 이 API 등록을 Http*Api로 교체한다
            builder.Register<IAuthApi, LocalAuthApi>(Lifetime.Singleton);
            // TODO(server): 호출부 없음 — 예: BootFlow에서 GetVersions() 비교 후 GetTable()로 갱신
            builder.Register<IDataApi, LocalDataApi>(Lifetime.Singleton);
            builder.Register<IPlayerApi, LocalPlayerApi>(Lifetime.Singleton);
            builder.Register<IBattleApi, LocalBattleApi>(Lifetime.Singleton).As<ISubmitFaultSwitch>();
            builder.Register<IUpgradeApi, LocalUpgradeApi>(Lifetime.Singleton);
            // TODO(server): Recover(Ad) 항상 거절 중 — 예: 광고 SDK 콜백에서 Recover(EnergySource.Ad, adTxId) 호출
            builder.Register<IEnergyApi, LocalEnergyApi>(Lifetime.Singleton);
            builder.Register<IStageApi, LocalStageApi>(Lifetime.Singleton);

            builder.Register<StageContext>(Lifetime.Singleton);
            builder.RegisterInstance<ITransitionCurtain>(curtain);
            builder.Register<ISceneNavigator, SceneLoader>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BootFlow>();

            // 진단 창(Diagnostics window) 및 전역 기능을 활성화하기 위해 GlobalMessagePipe를 설정합니다.
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));
        }
    }
}
