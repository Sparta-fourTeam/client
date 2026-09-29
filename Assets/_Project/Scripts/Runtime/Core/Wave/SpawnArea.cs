using UnityEngine;

namespace Game.Core
{
    public class SpawnArea : MonoBehaviour
    {
        [SerializeField]
        private Vector2 _size;

        public Vector2 Min => (Vector2)transform.position - _size * 0.5f;
        public Vector2 Max => (Vector2)transform.position + _size * 0.5f;
    }
}
