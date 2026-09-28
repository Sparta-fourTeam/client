namespace Game.Core.Wave
{
    // EnemyType 별 이동 속도를 조회하기 위한 인터페이스
    public interface IEnemySpeedProvider
    {
        float GetSpeed(EnemyType type);
    }
}
