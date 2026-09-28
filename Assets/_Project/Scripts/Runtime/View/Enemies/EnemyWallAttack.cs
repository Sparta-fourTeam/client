using Game.Core.Defense;
using UnityEngine;

namespace Game.View.Enemies
{
    public class EnemyWallAttack : MonoBehaviour
    {
        // #25 데이터 전까지 임시
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _interval = 1f;

        private Wall _wall;
        private WallAttack _attack;

        public bool IsAttacking => _attack != null;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsAttacking || !other.TryGetComponent<Wall>(out Wall wall))
            {
                return;
            }

            _wall = wall;
            _attack = new WallAttack(_interval);
        }

        private void Update()
        {
            if (_attack == null || _wall.IsDestroyed)
            {
                return;
            }

            int hits = _attack.Tick(Time.deltaTime);
            for (int i = 0; i < hits && !_wall.IsDestroyed; i++)
            {
                _wall.TakeDamage(_damage);
            }
        }
    }
}
