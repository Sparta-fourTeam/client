using UnityEngine;

namespace Game.Core
{
    public class EnemyProjectile : MonoBehaviour
    {
        private EnemyProjectileModel _model;

        public void Bind(EnemyProjectileModel model)
        {
            _model = model;
            transform.position = model.Position;
        }

        void Update()
        {
            if(_model == null) return;
            
            transform.position = _model.Position;

            if(_model.IsDone)
            {
                Destroy(gameObject);
            }
        }
    }
}
