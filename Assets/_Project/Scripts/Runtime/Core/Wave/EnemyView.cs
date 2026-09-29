using UnityEngine;

namespace Game.Core
{
    public class EnemyView : MonoBehaviour
    {
        private Enemy _enemy;

        public void Bind(Enemy enemy)
        {
            _enemy = enemy;
        }

        private void Update()
        {
            // Instantiate 직후 Bind가 호출 되기 전 null 체크
            if (_enemy == null)
            {
                return;
            }

            transform.position = _enemy.Position;
        }
    }
}
