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
            builder.RegisterEntryPoint<GameAnalyticsService>().As<IGameplayAnalytics>();

            // 서버 통신. 서버 인증을 쓰는 빌드(Android 개발 빌드)에서만 로그인이 서버로 간다
            builder.RegisterInstance(new HttpApiConfig(ServerConfig.BaseUrl));
            builder.Register<IHttpTransport, UnityWebRequestTransport>(Lifetime.Singleton);
            builder.Register<AuthSession>(Lifetime.Singleton).AsSelf().As<IAuthHeaderProvider>().As<IAuthTokenStore>();
            builder.Register(c => new HttpApiClient(c.Resolve<HttpApiConfig>(), c.Resolve<IHttpTransport>(), c.Resolve<IAuthHeaderProvider>(), tokens: c.Resolve<IAuthTokenStore>()),
                Lifetime.Singleton);
            // 서버에는 아직 인증 외의 API가 없어서 나머지 API는 Local* 구현을 그대로 쓴다. 서버 인증이면 서버 로그인 뒤에 로컬 로그인을 이어서 한다
            builder.Register<LocalAuthApi>(Lifetime.Singleton);
            builder.Register<ServerBackedAuthApi>(Lifetime.Singleton);
            builder.Register<IAuthApi>(c => ServerConfig.UseServerAuth ? c.Resolve<ServerBackedAuthApi>() : c.Resolve<LocalAuthApi>(),
                Lifetime.Singleton);
            // 로그인 정보는 백엔드(Local/서버)와 상관없이 기기에 저장한다
            builder.Register<PlayerPrefsAuthCredentialStore>(Lifetime.Singleton);
            builder.Register<IAuthCredentialStore>(c => new SessionAuthCredentialStore(
                c.Resolve<PlayerPrefsAuthCredentialStore>(), c.Resolve<AuthSession>()), Lifetime.Singleton);
            // 외부 계정 로그인. 구글은 Android 빌드에서 진짜 로그인을 하고, 에디터에서는 가짜 계정으로 바로 성공한다.
            // 애플은 SDK가 붙기 전까지 자리표시 구현이다
#if UNITY_EDITOR
            builder.Register<ISocialSignIn, EditorGoogleSignIn>(Lifetime.Singleton);
#else
            builder.Register<ISocialSignIn, AndroidGoogleSignIn>(Lifetime.Singleton);
#endif
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
            builder.Register<IQuestApi, LocalQuestApi>(Lifetime.Singleton);
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
