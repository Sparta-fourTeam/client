namespace Game.Core
{
    public interface IBurnableTarget
    {
        void ApplyBurn(float damagePerSecond, float duration);
    }
}
