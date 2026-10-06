namespace Game.Core
{
    /// <summary>몬스터가 가진 패시브 동작. 지금은 사망 시점만 있고, 필요해지면 훅을 늘린다</summary>
    public interface IPassive
    {
        void OnDied(EnemyModel self);
    }
}
