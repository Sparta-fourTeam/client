using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace Game.Core.Wave
{
    public class EnemySpawner : ITickable
    {
        private readonly IEnemyViewFactory _enemyViewFactory;
        private readonly WaveData _waveData;

        // TODO : 적 제거 (사망 / 화면 밖 이탈) 로직 붙일 때 처리 예정 
        private readonly List<Enemy> _activeEnemies = new();
        private readonly IRandomProvider _randomProvider;

        private float _elapsedTime;
        private int _spawnedCountInOnceSpawn;
        private bool _isWaitingForNextSpawn; // 다음 스폰 기다리는 상태

        private float _nextSpawnInterval;

        public EnemySpawner(IEnemyViewFactory enemyViewFactory, WaveData waveData, IRandomProvider randomProvider)
        {
            _enemyViewFactory = enemyViewFactory;
            _waveData = waveData;
            _randomProvider = randomProvider;
            _nextSpawnInterval = RollSpawnInterval();
        }

        public void Tick()
        {
            Advance(Time.deltaTime);
        }

        // 스폰 로직
        public void Advance(float deltaTime)
        {
            _elapsedTime += deltaTime;

            if (_isWaitingForNextSpawn)
            {
                if (_elapsedTime >= _waveData.SpawnCooldown)
                {
                    _elapsedTime = 0f;
                    _spawnedCountInOnceSpawn = 0;
                    _isWaitingForNextSpawn = false;
                }
                return;
            }

            // 스폰 간격 아직 안 지났으니 대기
            if (_elapsedTime < _nextSpawnInterval)
            {
                return;
            }

            _elapsedTime = 0f;
            Spawn();

            // 스폰 간격 랜덤
            _nextSpawnInterval = RollSpawnInterval();

            // 스폰 수량 채웠으면 대기 상태 전환
            if (_spawnedCountInOnceSpawn >= _waveData.SpawnCount)
            {
                _isWaitingForNextSpawn = true;
            }
        }

        // WaveData의 속도, 타입 그대로 쓰고 스폰 X 위치만 랜덤으로 정해서 생성
        private void Spawn()
        {
            var spawnX = _randomProvider.Range(_waveData.SpawnPositionXMin, _waveData.SpawnPositionXMax);
            var spawnPosition = new Vector2(spawnX, _waveData.SpawnPositionY);

            var enemy = new Enemy(spawnPosition, _waveData.EnemySpeed, _waveData.EnemyType);
            _activeEnemies.Add(enemy);
            _enemyViewFactory.Create(enemy);
            _spawnedCountInOnceSpawn++;
        }

        // 스폰 간격 랜덤
        private float RollSpawnInterval()
        {
            return _randomProvider.Range(_waveData.SpawnIntervalMin, _waveData.SpawnIntervalMax);
        }
    }
}
