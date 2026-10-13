using Game.Core;
using Game.Core.Defense;
using Game.View;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace Game.Sandbox
{
    /// <summary>스킬 샌드박스 씬의 의존성 구성. 스테이지 씬의 스코프와 달리 Root 서비스(저장소, 장면 전환)에 묶이지 않고
    /// MessagePipe와 스킬·적·벽만 등록한다.</summary>
    public sealed class SandboxLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe();
            builder.Register<GameDataStore>(Lifetime.Singleton);
            // 벽 체력은 스테이지 테이블에서 온다. 샌드박스는 첫 스테이지 값을 쓴다
            builder.Register(resolver => resolver.Resolve<GameDataStore>().StageOrFirst(0), Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<Wall>();
            builder.RegisterComponentInHierarchy<SkillController>();
            builder.RegisterComponentInHierarchy<SandboxEnemyField>().AsSelf().As<IEnemyTargetProvider>();
            builder.RegisterComponentInHierarchy<SandboxController>();
            // 인게임 HUD와 같은 SkillHud 프리팹: SkillController가 발행하는 SkillChanged를 받아 쿨타임을 그린다
            builder.RegisterComponentInHierarchy<SkillHudView>();
            builder.Register<ISkillDataProvider, DefaultSkillDataProvider>(Lifetime.Scoped);
            builder.Register<IStartingSkills, NoStartingSkills>(Lifetime.Scoped);
            builder.Register<ISkillUnlock, AllSkillsUnlocked>(Lifetime.Scoped);
            builder.Register<IPermanentSkillEffects, SandboxPermanentSkillEffects>(Lifetime.Scoped);
            builder.RegisterInstance(new SandboxProgression()).AsSelf().As<IWeaponProgression>();
            // 씬에 있는 컴포넌트는 누군가 요청해야 주입되므로, 시작할 때 한 번 꺼내 주입을 끝낸다.
            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Resolve<SkillController>();
                resolver.Resolve<SandboxEnemyField>();
                resolver.Resolve<SandboxController>();
                resolver.Resolve<SkillHudView>();
            });
        }
    }
}
