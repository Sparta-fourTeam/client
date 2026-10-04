namespace Game.Core
{
    public interface ISlowableTarget
    {
        void ApplySlow(float ratio, float duration);
    }
}
