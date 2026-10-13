using UnityEngine;

namespace Game.Core
{
    public class SpawnArea : MonoBehaviour
    {
        [SerializeField]
        private Vector2 _size;

        public Vector2 Min => (Vector2)transform.position - _size * 0.5f;
        public Vector2 Max => (Vector2)transform.position + _size * 0.5f;

        // 선택하면 스폰 영역이 씬 뷰에 보인다. 영역은 카메라가 보는 월드(가로 6.0, 세로 약 10.7) 위쪽 바깥에 두고,
        // 가로는 가장 넓은 적이 잘리지 않을 만큼만 쓴다. 카메라 크기를 바꾸면 이 영역도 다시 맞춰야 한다
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(_size.x, _size.y, 0f));
        }
    }
}
