namespace Game.Core
{
    /// <summary>몬스터가 가진 패시브 동작. 필요한 훅만 구현한다</summary>
    public interface IPassive
    {
        /// <summary>죽은 직후 한 번</summary>
        void OnDied(EnemyModel self) { }

        /// <summary>살아 있는 동안 매 프레임. 빙결·마비·기절 중에는 불리지 않는다</summary>
        void OnTick(EnemyModel self, float deltaTime) { }
    }
}
