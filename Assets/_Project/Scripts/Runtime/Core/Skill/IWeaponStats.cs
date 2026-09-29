namespace Game.Core
{
    public interface IWeaponStats
    {
        float Cooldown { get; }
        float Damage { get; }
        int HitCount { get; }
    }
}
