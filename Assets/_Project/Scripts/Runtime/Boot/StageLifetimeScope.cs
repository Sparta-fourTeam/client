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
            builder.RegisterComponentInHierarchy<Wall>();
            builder.RegisterComponentInHierarchy<WeaponController>();
            builder.Register<IWeaponDataProvider, DefaultWeaponDataProvider>(Lifetime.Scoped);
            builder.Register<IWeaponProgression, ProfileWeaponProgression>(Lifetime.Scoped);
            builder.RegisterEntryPoint<StageClock>().AsSelf();
            builder.RegisterEntryPoint<StageManager>().AsSelf();
            builder.RegisterEntryPoint<StageJudge>();
            // BattleStats는 WaveProgress보다 먼저 EnemyDied를 구독해야 스테이지를 끝내는 마지막 처치까지 센다 (순서 변경 금지)
            builder.RegisterEntryPoint<BattleStats>().AsSelf();
            builder.RegisterEntryPoint<WaveProgress>();
            builder.Register<IRandomProvider, UnityRandomProvider>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<EnemyFactory>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<SpawnArea>();
            builder.RegisterInstance(CreateWaveData());
            builder.Register<EnemyProjectileSystem>(Lifetime.Scoped);
            builder.RegisterEntryPoint<EnemySpawner>(Lifetime.Scoped).AsSelf().As<IEnemyTargetProvider>();
            builder.RegisterEntryPoint<WaveManager>();
        }

        // TODO : 현재는 하드코딩, 추후 로컬 데이터 연동시 수정할 수 있음.
        private static EnemySpawnConfig CreateWaveData()
        {
            return new EnemySpawnConfig(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCooldown: 3f
                );
        }
    }
}
