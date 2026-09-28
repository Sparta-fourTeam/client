namespace Game.Core.Wave
{
    // 랜덤값 추상화
    public interface IRandomProvider
    {
        float Range(float min, float max);
    }
}
