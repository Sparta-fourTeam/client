using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanEffect : MonoBehaviour
    {
        private IObjectPool<HitscanEffect> pool;
        private float lifetime;

        public void Init(IObjectPool<HitscanEffect> pool, Vector3 position, float lifetime)
        {
            this.pool = pool;
            transform.position = position;
            this.lifetime = lifetime;
        }

        private void Update()
        {
            lifetime -= Time.deltaTime;
            if (lifetime <= 0)
            {
                pool.Release(this);
            }
        }
    }
}
