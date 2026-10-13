namespace Game.Core
{
    public interface IAreaSlowTarget
    {
        void SetAreaSlow(object source, float ratio);
        void RemoveAreaSlow(object source);
    }
}
