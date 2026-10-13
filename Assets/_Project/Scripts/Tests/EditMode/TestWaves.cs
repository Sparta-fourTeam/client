using Game.Core;
using Game.Core.Messages;

namespace Game.Tests
{
    /// <summary>스포너 테스트가 쓰는 웨이브 메시지. 일반 몬스터(Id 1)만 enemyCount마리 나오는 구성이다</summary>
    internal static class TestWaves
    {
        public static WaveStarted Started(int waveIndex, int enemyCount, bool isFinalWave) =>
            new(waveIndex, isFinalWave, new[] { new WaveSpawn { MonsterId = 1, Count = enemyCount, Type = EnemyType.Normal } });
    }
}
