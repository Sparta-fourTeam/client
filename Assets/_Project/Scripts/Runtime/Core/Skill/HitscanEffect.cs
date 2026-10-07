using UnityEngine;
using UnityEngine.Pool;

namespace Game.Core
{
    public class HitscanEffect : MonoBehaviour
    {
        private IObjectPool<HitscanEffect> pool;
        [SerializeField] private GameObject judgementVisual;
        [SerializeField] private GameObject regularVisual;

        public void SetVisualForm(SkillForm form)
        {
            if (judgementVisual == null) { return; }
            bool upgraded = form == SkillForm.JudgementThunder;
            judgementVisual.SetActive(upgraded);
            if (regularVisual != null) { regularVisual.SetActive(!upgraded); }
        }

        [SerializeField] private LayerMask targetMask;
        private float damage;
        private IEnemyTarget directTarget;
        // 이 타격이 겨냥한 적. Impact 반응이 "방금 맞은 적"으로 쓴다
        private IEnemyTarget sourceTarget;
        private AttackReactions reactions;
        [SerializeField] private float radius;

        // 애니메이션 이벤트(Hit, Release) 대신 시간으로 진행하는 이펙트(파티클 프리팹)용. 0보다 작으면 쓰지 않는다 (기존 프리팹은 그대로)
        [SerializeField] private float hitDelay = -1f;
        [SerializeField] private float releaseDelay = -1f;

        private float elapsed;
        private bool hitDone;
        private bool released;




        private int sourceSkill;   // 이 타격을 만든 스킬 (피해 집계용)

        public void Init(IObjectPool<HitscanEffect> pool, Vector3 position, float damage, IEnemyTarget directTarget = null, AttackReactions reactions = null, IEnemyTarget sourceTarget = null)
        {
            sourceSkill = Game.Core.Combat.DamageAttribution.Current;
            this.pool = pool;
            transform.position = position;
            this.damage = damage;
            this.directTarget = directTarget;
            this.sourceTarget = sourceTarget;
            this.reactions = reactions ?? AttackReactions.Empty;
            elapsed = 0f;
            hitDone = false;
            released = false;
            this.reactions.Raise(AttackEvent.Start, new AttackContext(position, Vector3.zero));
        }

        private void Update()
        {
            using var scope = Game.Core.Combat.DamageAttribution.Begin(sourceSkill);
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
                if (!directTarget.IsDead)
                {
                    directTarget.TakeDamage(Mathf.Max(1, (int)damage));
                    var context = new AttackContext(transform.position, Vector3.zero, directTarget);
                    reactions.Raise(AttackEvent.Hit, context);
                    if (directTarget.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
                }
                return;
            }
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, targetMask);

            var damaged = new System.Collections.Generic.HashSet<Enemy>();
            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent(out Enemy enemy) && !enemy.IsDead && damaged.Add(enemy))
                {
                    Vector2 position = enemy.transform.position;
                    enemy.TakeDamage((int)damage);
                    var context = new AttackContext(position, Vector3.zero, enemy);
                    reactions.Raise(AttackEvent.Hit, context);
                    if (enemy.IsDead) { reactions.Raise(AttackEvent.Kill, context); }
                }
            }
            reactions.Raise(AttackEvent.Impact, new AttackContext(transform.position, Vector3.zero, sourceTarget));
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
