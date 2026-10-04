namespace Game.Core
{
    public interface IBurnableTarget
    {
        void ApplyBurn(float damagePerSecond, float duration, float maxHpRatio = 0, System.Action<UnityEngine.Vector2> onDeath = null);
    }
}
