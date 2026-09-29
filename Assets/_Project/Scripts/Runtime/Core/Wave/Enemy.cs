using UnityEngine;

namespace Game.Core
{
    public class Enemy : MonoBehaviour
    {
        private EnemyModel _enemyModel;

        public void Bind(EnemyModel enemyModel)
        {
            _enemyModel = enemyModel;
        }

        private void Update()
        {
            // Instantiate 직후 Bind가 호출 되기 전 null 체크
            if (_enemyModel == null)
            {
                return;
            }

            transform.position = _enemyModel.Position;
        }
    }
}
