using UnityEngine;

namespace Game.View.Monster
{
    /// <summary>
    /// 몬스터 AnimationClip의 애니메이션 이벤트를 받아 이펙트를 생성한다. Animator와 같은 오브젝트에 붙는다.
    /// </summary>
    public sealed class MonsterAnimationEvents : MonoBehaviour
    {
        [SerializeField] private GameObject _impactEffect;
        [SerializeField] private Vector2 _impactOffset;

        // Attack 클립의 애니메이션 이벤트에서 호출 (Play 모드에서만 이벤트가 불린다)
        // 확인용: Play 중 컴포넌트 ⋮ 메뉴 → Play Impact
        [ContextMenu("Play Impact")]
        public void PlayImpact()
        {
            if (_impactEffect == null)
            {
                return;
            }

            var scale = transform.lossyScale;
            var position = transform.position + Vector3.Scale(_impactOffset, scale);
            var effect = Instantiate(_impactEffect, position, Quaternion.identity);
            effect.transform.localScale = Vector3.Scale(effect.transform.localScale, new Vector3(scale.x, scale.y, 1f));
        }
    }
}
