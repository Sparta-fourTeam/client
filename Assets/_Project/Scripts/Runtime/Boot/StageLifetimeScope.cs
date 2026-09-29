using Game.Core;
using Game.Core.Defense;
using Game.Core.Stage;
using Game.View;
using VContainer;
using VContainer.Unity;

namespace Game.Boot
{
    public sealed class StageLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<Wall>();
            builder.RegisterEntryPoint<StageJudge>();
            builder.RegisterEntryPoint<WaveProgress>();
            builder.Register<IRandomProvider, UnityRandomProvider>(Lifetime.Scoped);
            builder.RegisterComponentInHierarchy<EnemyViewFactory>().AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<SpawnArea>();
            builder.RegisterInstance(CreateWaveData());
            builder.RegisterEntryPoint<EnemySpawner>(Lifetime.Scoped);
            builder.RegisterEntryPoint<WaveManager>();
        }

        // TODO : 현재는 하드코딩, 추후 로컬 데이터 연동시 수정할 수 있음.
        private static EnemySpawnConfig CreateWaveData()
        {
            return new EnemySpawnConfig(
                spawnIntervalMin: 0.1f,
                spawnIntervalMax: 0.5f,
                spawnCooldown: 3f,
                enemyType: EnemyType.Normal
                );
        }
    }
}
