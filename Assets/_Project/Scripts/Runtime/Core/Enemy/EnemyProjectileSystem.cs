using System.Collections.Generic;
using Game.Core.Defense;
using UnityEngine;

namespace Game.Core
{
    public partial class EnemyProjectileSystem
    {
        private readonly List<EnemyProjectileModel> _projectiles = new();
        public int ActiveCount => _projectiles.Count;

        public EnemyProjectileModel Fire(Vector2 from, int damage, float speed, int monsterId = 0)
        {
            var projectile = new EnemyProjectileModel(from, speed, damage) { MonsterId = monsterId };
            _projectiles.Add(projectile);
            return projectile;
        }

        public void Tick(float deltaTime, Wall wall)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                _projectiles[i].Tick(deltaTime, wall);
                if (_projectiles[i].IsDone)
                {
                    _projectiles.RemoveAt(i);
                }
            }
        }
    }
}
