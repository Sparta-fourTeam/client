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
            // 로그인 정보는 백엔드(Local/서버)와 상관없이 기기에 저장한다
            builder.Register<IAuthCredentialStore, PlayerPrefsAuthCredentialStore>(Lifetime.Singleton);
            // 외부 계정 로그인. SDK가 붙기 전에는 자리표시 구현이라 로그인창에서 준비 중으로 막힌다
            builder.Register<ISocialSignIn, PlaceholderGoogleSignIn>(Lifetime.Singleton);
            builder.Register<ISocialSignIn, PlaceholderAppleSignIn>(Lifetime.Singleton);
            // TODO(server): 호출부 없음 — 예: BootFlow에서 GetVersions() 비교 후 GetTable()로 갱신
            builder.Register<IDataApi, LocalDataApi>(Lifetime.Singleton);
            builder.Register<IAccountApi, LocalAccountApi>(Lifetime.Singleton);
            builder.Register<AccountService>(Lifetime.Singleton);
            builder.Register<IPlayerApi, LocalPlayerApi>(Lifetime.Singleton);
            builder.Register<IBattleApi, LocalBattleApi>(Lifetime.Singleton).AsSelf().As<ISubmitFaultSwitch>();
            builder.Register<IUpgradeApi, LocalUpgradeApi>(Lifetime.Singleton);
            // TODO(server): Recover(Ad) 항상 거절 중 — 예: 광고 SDK 콜백에서 Recover(EnergySource.Ad, adTxId) 호출
            builder.Register<IEnergyApi, LocalEnergyApi>(Lifetime.Singleton);
            builder.Register<IStageApi, LocalStageApi>(Lifetime.Singleton);
            builder.Register<ICodexApi, LocalCodexApi>(Lifetime.Singleton);
            builder.Register<ITutorialApi, LocalTutorialApi>(Lifetime.Singleton);

            // 음악·효과음·음성 볼륨은 기기 설정이라 기기에 저장하고, 시작할 때와 바뀔 때 오디오 믹서에 적용한다
            builder.Register<IVolumeStore, PlayerPrefsVolumeStore>(Lifetime.Singleton);
            builder.Register<SoundSettings>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SoundVolumeApplier>();
            // 데미지 표시·프레임레이트 같은 나머지 기기 설정도 기기에 저장하고 시작할 때와 바뀔 때 적용한다
            builder.Register<ISettingsStore, PlayerPrefsSettingsStore>(Lifetime.Singleton);
            builder.Register<GameSettings>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameSettingsApplier>();

            builder.Register<StageContext>(Lifetime.Singleton);
            builder.RegisterInstance<ITransitionCurtain>(curtain);
            builder.Register<ISceneNavigator, SceneLoader>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BootFlow>();

            // 랜덤 재료는 열린 재료 중에서 뽑는다. 장비와 스킬은 플레이어 레벨이 해금 레벨 이상이면 열린다(EquipmentUnlockRule, SkillUnlockRule).
            builder.RegisterBuildCallback(c =>
            {
                var battleApi = c.Resolve<LocalBattleApi>();
                var data = c.Resolve<GameDataStore>();
                var store = c.Resolve<LocalSaveStore>();
                battleApi.UnlockedSkillIds = () => SkillUnlockRule.UnlockedSkillIds(data, store.Load().exp);
                battleApi.UnlockedEquipmentIds = () => EquipmentUnlockRule.UnlockedTargetIds(data, store.Load().exp);
            });

            // 진단 창(Diagnostics window) 및 전역 기능을 활성화하기 위해 GlobalMessagePipe를 설정합니다.
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));
        }
    }
}
