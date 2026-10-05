using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanEffect : MonoBehaviour
    {
        private IObjectPool<HitscanEffect> pool;
        [SerializeField] private GameObject secondaryProjectilePrefab;
        public GameObject SecondaryProjectilePrefab => secondaryProjectilePrefab;
        [SerializeField] private LayerMask targetMask;
        private float damage;
        private System.Action<Enemy> onTargetHit;
        private System.Action<Vector2> onHit;
        private System.Action<Vector2> onKilled;
        private IEnemyTarget directTarget;
        private AttackReactions reactions;
        [SerializeField] private float radius;

        // 애니메이션 이벤트(Hit, Release) 대신 시간으로 진행하는 이펙트(파티클 프리팹)용. 0보다 작으면 쓰지 않는다 (기존 프리팹은 그대로)
        [SerializeField] private float hitDelay = -1f;
        [SerializeField] private float releaseDelay = -1f;

        private float elapsed;
        private bool hitDone;
        private bool released;




        public void Init(IObjectPool<HitscanEffect> pool, Vector3 position, float damage, System.Action<Vector2> onHit = null, System.Action<Enemy> onTargetHit = null, System.Action<Vector2> onKilled = null, IEnemyTarget directTarget = null, AttackReactions reactions = null)
        {

            this.pool = pool;
            transform.position = position;
            this.damage = damage;
            this.onHit = onHit;
            this.onKilled = onKilled;
            this.directTarget = directTarget;
            this.onTargetHit = onTargetHit;
            this.reactions = reactions ?? AttackReactions.Empty;
            elapsed = 0f;
            hitDone = false;
            released = false;
            this.reactions.Raise(AttackEvent.Start, new AttackContext(position, Vector3.zero));
        }

        private void Update()
        {
            if (pool == null || released)
            {
                return;
            }

            elapsed += Time.deltaTime;
            reactions.Raise(AttackEvent.Tick, new AttackContext(transform.position, Vector3.zero, deltaTime: Time.deltaTime, elapsed: elapsed));

            if (hitDelay >= 0f && !hitDone && elapsed >= hitDelay)
            {
                Hit();
            }

            if (releaseDelay >= 0f && elapsed >= releaseDelay)
            {
                Release();
            }
        }

        private void Hit()
        {
            if (!gameObject.activeSelf || hitDone || released)
            {
                return;
            }

            hitDone = true;
            // Secondary lightning hits only its chosen target and has no inherited callbacks.
            if (directTarget != null)
            {
                if (!(directTarget is EnemyModel model && model.IsDead))
                {
                    directTarget.TakeDamage(Mathf.Max(1, (int)damage));
                    var context = new AttackContext(transform.position, Vector3.zero, directTarget);
                    reactions.Raise(AttackEvent.Hit, context);
                    if (directTarget is EnemyModel killed && killed.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
                }
                return;
            }
            var killedPositions = new System.Collections.Generic.List<Vector2>();
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, targetMask);

            var damaged = new System.Collections.Generic.HashSet<Enemy>();
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Enemy enemy) && !enemy.IsDead && damaged.Add(enemy))
                {
                    Vector2 position = enemy.transform.position;
                    enemy.TakeDamage((int)damage);
                    if (enemy.IsDead) { killedPositions.Add(position); }
                    onTargetHit?.Invoke(enemy);
                    var context = new AttackContext(position, Vector3.zero, enemy.Target);
                    reactions.Raise(AttackEvent.Hit, context);
                    if (enemy.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
                }
            }
            foreach (var position in killedPositions) { onKilled?.Invoke(position); }
            onHit?.Invoke(transform.position);
        }

        private void Release()
        {
            if (released)
            {
                return;
            }

            released = true;
            reactions.Raise(AttackEvent.Expired, new AttackContext(transform.position, Vector3.zero));
            pool.Release(this);
        }
    }
}
