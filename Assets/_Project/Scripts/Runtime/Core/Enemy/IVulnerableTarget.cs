namespace Game.Core
{
    public interface IVulnerableTarget
    {
        void ApplyVulnerability(float ratio, float duration);
    }
}
