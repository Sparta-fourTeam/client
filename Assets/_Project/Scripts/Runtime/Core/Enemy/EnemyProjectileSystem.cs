using System.Collections.Generic;
using Game.Core.Defense;
using UnityEngine;

namespace Game.Core
{
    public class EnemyProjectileSystem
    {
        public const float ProjectileSpeed = 8f;
        private readonly List<EnemyProjectileModel> _projectiles = new();
        public int ActiveCount => _projectiles.Count;

        public EnemyProjectileModel Fire(Vector2 from, int damage)
        {
            var projectile = new EnemyProjectileModel(from, ProjectileSpeed, damage);
            _projectiles.Add(projectile);
            return projectile;
        }

        public void Tick(float deltaTime, Wall wall)
        {
            for(int i = _projectiles.Count - 1; i >= 0; i--)
            {
                _projectiles[i].Tick(deltaTime, wall);
                if (_projectiles[i].IsDone)
                    _projectiles.RemoveAt(i);
            }
        }
    }
}
