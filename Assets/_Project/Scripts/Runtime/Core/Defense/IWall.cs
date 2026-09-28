namespace Game.Core.Defense
{
    public interface IWall
    {
        int CurrentHp { get; }
        int MaxHp { get; }
        bool IsDestroyed { get; }
        void TakeDamage(int damage);
    }
}
