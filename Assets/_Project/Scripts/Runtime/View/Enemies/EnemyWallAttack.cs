using Game.Core.Defense;
using Game.View.Defense;
using UnityEngine;
using VContainer;

namespace Game.View.Enemies
{
    public class EnemyWallAttack : MonoBehaviour
    {
        // #25 데이터 전까지 임시
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _interval = 1f;

        private IWall _wall;
        private WallAttack _attack;

        public bool IsAttacking => _attack != null;

        [Inject]
        public void Construct(IWall wall)
        {
            _wall = wall;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsAttacking || !other.TryGetComponent<WallView>(out _))
            {
                return;
            }
            _attack = new WallAttack(_wall, _damage, _interval);
        }

        private void Update()
        {
            _attack?.Tick(Time.deltaTime);
        }
    }
}
