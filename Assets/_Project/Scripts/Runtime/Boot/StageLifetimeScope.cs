using Game.Core;
using Game.Core.Defense;
using Game.Core.Stage;
using VContainer;
using VContainer.Unity;


namespace Game.Boot
{
    public sealed class StageLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 어떤 스테이지인지는 로비에서 정해 StageContext에 남긴다. Stage 씬을 바로 열었으면 첫 스테이지 값을 쓴다
            builder.Register(resolver =>
                resolver.Resolve<GameDataStore>().StageOrFirst(resolver.Resolve<StageContext>().StageId), Lifetime.Scoped);
            builder.Register(resolver => EnemySpawnConfig.From(resolver.Resolve<StageDefinition>().Spawn), Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<Wall>();
            builder.RegisterComponentInHierarchy<WeaponController>();
            builder.Register<IWeaponDataProvider, DefaultWeaponDataProvider>(Lifetime.Scoped);
            builder.Register<IWeaponProgression, ProfileWeaponProgression>(Lifetime.Scoped);
            builder.Register<IStartingSkills, DefaultStartingSkills>(Lifetime.Scoped);
            builder.RegisterEntryPoint<StageClock>().AsSelf();
            builder.RegisterEntryPoint<StageManager>().AsSelf();
            builder.RegisterEntryPoint<StageJudge>();
            // BattleStats는 WaveProgress보다 먼저 EnemyDied를 구독해야 스테이지를 끝내는 마지막 처치까지 센다 (순서 변경 금지)
            builder.RegisterEntryPoint<BattleStats>().AsSelf();
            builder.RegisterEntryPoint<WaveProgress>();
            builder.Register<IRandomProvider, UnityRandomProvider>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<EnemyFactory>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<SpawnArea>();
            builder.Register<EnemyProjectileSystem>(Lifetime.Scoped);
            builder.RegisterEntryPoint<EnemySpawner>(Lifetime.Scoped).AsSelf().As<IEnemyTargetProvider>();
            builder.RegisterEntryPoint<WaveManager>();
        }
    }
}
