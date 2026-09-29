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
            builder.RegisterEntryPoint<StageJudge>();
            // BattleStats는 WaveProgress보다 먼저 EnemyDied를 구독해야 스테이지를 끝내는 마지막 처치까지 센다 (순서 변경 금지)
            builder.RegisterEntryPoint<BattleStats>().AsSelf();
            builder.RegisterEntryPoint<WaveProgress>();
        }
    }
}
