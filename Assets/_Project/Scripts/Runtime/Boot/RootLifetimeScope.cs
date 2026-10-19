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

            bool serverProgress = ServerConfig.UseServerProgress;

            builder.Register<ILoginConsentStore>(_ => new PlayerPrefsLoginConsentStore(), Lifetime.Singleton);
            builder.Register<GameDataStore>(Lifetime.Singleton);
            builder.Register(c => serverProgress
                ? new LocalSaveStore(LocalSavePath.Current, c.Resolve<GameDataStore>())
                : new LocalSaveStore(c.Resolve<GameDataStore>()), Lifetime.Singleton);
            builder.Register<LocalBattleCheckpointStore>(Lifetime.Singleton);
            builder.Register<ServerBattleCheckpointStore>(Lifetime.Singleton);
            builder.Register<IBattleCheckpointStore>(c => serverProgress
                ? c.Resolve<ServerBattleCheckpointStore>() : c.Resolve<LocalBattleCheckpointStore>(), Lifetime.Singleton);
            builder.Register<PlayerProfile>(Lifetime.Singleton);
            // SDK는 Root에서 시작하지 않고 로그인 흐름의 약관·선택 동의 확인 후 시작한다.
            builder.Register<GameAnalyticsService>(Lifetime.Singleton)
                .As<IGameplayAnalytics>().As<IAnalyticsCollectionControl>();

            // Android는 기존 서버 인증을 유지한다. 진행 연결은 별도 개발 opt-in에서만 사용한다.
            builder.RegisterInstance(new HttpApiConfig(ServerConfig.BaseUrl));
            builder.Register<IHttpTransport, UnityWebRequestTransport>(Lifetime.Singleton);
            builder.Register<IAppUpdateChecker, StoreAppUpdateChecker>(Lifetime.Singleton);
            builder.Register<AuthSession>(Lifetime.Singleton).AsSelf().As<IAuthHeaderProvider>().As<IAuthTokenStore>();
            builder.Register(c => new HttpApiClient(c.Resolve<HttpApiConfig>(), c.Resolve<IHttpTransport>(), c.Resolve<IAuthHeaderProvider>(), tokens: c.Resolve<IAuthTokenStore>()),
                Lifetime.Singleton);
            builder.Register<HttpBattleSourceApi>(Lifetime.Singleton);
            builder.Register<HttpDataVersionApi>(Lifetime.Singleton);
            builder.Register<LocalAuthApi>(Lifetime.Singleton);
            builder.Register(c => serverProgress
                ? new ServerBackedAuthApi(c.Resolve<HttpApiClient>(), c.Resolve<AuthSession>())
                : new ServerBackedAuthApi(c.Resolve<HttpApiClient>(), c.Resolve<AuthSession>(), c.Resolve<LocalAuthApi>()), Lifetime.Singleton);
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
            // 계정 연동은 서버 인증을 쓰는 빌드에서만 서버로 보낸다 (서버 로그인으로 받은 토큰이 있어야 하기 때문)
            builder.Register<LocalAccountApi>(Lifetime.Singleton);
            builder.Register(c => serverProgress
                ? new ServerBackedAccountApi(c.Resolve<HttpApiClient>(), null, c.Resolve<IAuthTokenStore>())
                : new ServerBackedAccountApi(c.Resolve<HttpApiClient>(), c.Resolve<LocalAccountApi>(), c.Resolve<IAuthTokenStore>()), Lifetime.Singleton);
            builder.Register<IAccountApi>(c => ServerConfig.UseServerAuth ? c.Resolve<ServerBackedAccountApi>() : c.Resolve<LocalAccountApi>(),
                Lifetime.Singleton);
            builder.Register<IProfileApi>(c => ServerConfig.UseServerAuth
                ? (IProfileApi)c.Resolve<ServerBackedAccountApi>() : c.Resolve<LocalAccountApi>(), Lifetime.Singleton);
            builder.Register<AccountService>(Lifetime.Singleton);
            // 서버 인증 계정은 원본·레벨 테이블과 /me가 모두 검증돼야 게임에 진입한다.
            builder.Register<LocalPlayerApi>(Lifetime.Singleton);
            builder.Register(c => new HttpPlayerApi(c.Resolve<HttpApiClient>(), c.Resolve<IAuthTokenStore>(),
                c.Resolve<GameDataStore>(), c.Resolve<HttpBattleSourceApi>(), c.Resolve<HttpDataVersionApi>(), c.Resolve<IProfileApi>()), Lifetime.Singleton);
            builder.Register<IPlayerApi>(c => serverProgress ? c.Resolve<HttpPlayerApi>() : c.Resolve<LocalPlayerApi>(),
                Lifetime.Singleton);
            builder.Register<LocalBattleApi>(Lifetime.Singleton).AsSelf().As<ISubmitFaultSwitch>();
            builder.Register<ServerBattleApi>(Lifetime.Singleton);
            builder.Register<IBattleApi>(c => serverProgress ? c.Resolve<ServerBattleApi>() : c.Resolve<LocalBattleApi>(), Lifetime.Singleton);
            builder.Register<ServerUnavailableApi>(Lifetime.Singleton);
            builder.Register<ServerClaimApi>(Lifetime.Singleton);
            builder.Register<HttpUpgradeApi>(Lifetime.Singleton);
            if (serverProgress)
            { builder.Register<ServerAccountPendingRecovery>(Lifetime.Singleton).As<IAccountPendingRecovery>(); }
            else
            { builder.Register<NoAccountPendingRecovery>(Lifetime.Singleton).As<IAccountPendingRecovery>(); }
            builder.Register(c => new HttpEnergyApi(c.Resolve<HttpApiConfig>(), c.Resolve<IHttpTransport>(),
                c.Resolve<IAuthTokenStore>(), c.Resolve<HttpPlayerApi>()), Lifetime.Singleton);
            builder.Register<IUpgradeApi>(c => serverProgress ? c.Resolve<HttpUpgradeApi>() : c.Resolve<LocalUpgradeApi>(), Lifetime.Singleton);
            builder.Register<IEnergyApi>(c => serverProgress ? c.Resolve<HttpEnergyApi>() : c.Resolve<LocalEnergyApi>(), Lifetime.Singleton);
            builder.Register<IStageApi>(c => serverProgress ? c.Resolve<ServerClaimApi>() : c.Resolve<LocalStageApi>(), Lifetime.Singleton);
            builder.Register<ICodexApi>(c => serverProgress ? c.Resolve<ServerClaimApi>() : c.Resolve<LocalCodexApi>(), Lifetime.Singleton);
            builder.Register<IQuestApi>(c => serverProgress ? c.Resolve<ServerClaimApi>() : c.Resolve<LocalQuestApi>(), Lifetime.Singleton);
            builder.Register<IShopApi>(c => serverProgress ? c.Resolve<ServerUnavailableApi>() : c.Resolve<LocalShopApi>(), Lifetime.Singleton);
            builder.Register<ITutorialApi>(c => serverProgress ? c.Resolve<ServerClaimApi>() : c.Resolve<LocalTutorialApi>(), Lifetime.Singleton);
            builder.Register<LocalUpgradeApi>(Lifetime.Singleton);
            builder.Register<LocalEnergyApi>(Lifetime.Singleton);
            builder.Register<LocalStageApi>(Lifetime.Singleton);
            builder.Register<LocalCodexApi>(Lifetime.Singleton);
            builder.Register<LocalQuestApi>(Lifetime.Singleton);
            builder.Register<LocalShopApi>(Lifetime.Singleton);
            builder.Register<LocalTutorialApi>(Lifetime.Singleton);

            // 음악·효과음·음성 볼륨은 기기 설정이라 기기에 저장하고, 시작할 때와 바뀔 때 오디오 믹서에 적용한다
            builder.Register<IVolumeStore, PlayerPrefsVolumeStore>(Lifetime.Singleton);
            builder.Register<SoundSettings>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SoundVolumeApplier>();
            // 소리는 ID로 재생한다. 효과음은 풀링, 배경음은 크로스페이드하고 위 믹서 그룹으로 출력한다
            builder.RegisterEntryPoint<AudioService>().As<IAudioService>();
            builder.RegisterBuildCallback(c => c.Inject(ButtonPressFeedback.EnsureInstance()));
            // 데미지 표시·프레임레이트 같은 나머지 기기 설정도 기기에 저장하고 시작할 때와 바뀔 때 적용한다
            builder.Register<ISettingsStore, PlayerPrefsSettingsStore>(Lifetime.Singleton);
            builder.Register<GameSettings>(Lifetime.Singleton);
            builder.RegisterEntryPoint<GameSettingsApplier>();

            builder.Register<StageContext>(Lifetime.Singleton);
            builder.RegisterInstance<ITransitionCurtain>(curtain);
            builder.Register<ISceneNavigator, SceneLoader>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BootFlow>();

            // 랜덤 재료는 열린 재료 중에서 뽑는다. 장비와 스킬은 플레이어 레벨이 해금 레벨 이상이면 열린다(EquipmentUnlockRule, SkillUnlockRule).
            if (!serverProgress)
            {
                builder.RegisterBuildCallback(c =>
            {
                var battleApi = c.Resolve<LocalBattleApi>();
                var data = c.Resolve<GameDataStore>();
                var store = c.Resolve<LocalSaveStore>();
                battleApi.UnlockedSkillIds = () => SkillUnlockRule.UnlockedSkillIds(data, store.Load().exp);
                battleApi.UnlockedEquipmentIds = () => EquipmentUnlockRule.UnlockedTargetIds(data, store.Load().exp);
            });
            }

            // 진단 창(Diagnostics window) 및 전역 기능을 활성화하기 위해 GlobalMessagePipe를 설정합니다.
            builder.RegisterBuildCallback(c => GlobalMessagePipe.SetProvider(c.AsServiceProvider()));
        }
    }
}
