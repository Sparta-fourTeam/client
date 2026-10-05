using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Sandbox
{
    /// <summary>샌드박스의 적. 실제 Enemy 프리팹과 EnemyModel을 쓰므로(Hitscan은 충돌체와 Enemy 컴포넌트로 판정한다) 게임과 같은 대상이 된다.
    /// 기본은 제자리의 샌드백이고, 이동 모드를 켜면 EnemySpawner처럼 벽을 향해 내려온다. 적의 공격은 피해가 0이다.</summary>
    public sealed class SandboxEnemyField : MonoBehaviour, IEnemyTargetProvider
    {
        public enum Pattern { Line, Cluster, Scatter }

        private sealed class Entry
        {
            public EnemyModel Model;
            public Vector2 Spawn;
            public int Hp;
            public EnemyType Type;
        }

        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private float defaultSpeed = 0.6f;
        [SerializeField] private float attackRange = 0.5f;

        private IPublisher<EnemyHpChanged> hpPublisher;
        private IPublisher<EnemyDied> diedPublisher;
        private ISubscriber<EnemyHpChanged> hpSubscriber;
        private ISubscriber<EnemyDied> diedSubscriber;
        private Wall wall;
        private IDisposable subscription;
        private readonly List<Entry> entries = new List<Entry>();
        private readonly Dictionary<int, int> lastHp = new Dictionary<int, int>();
        private readonly Queue<(float time, int damage)> recentDamage = new Queue<(float, int)>();
        private int nextId;

        /// <summary>true면 EnemySpawner처럼 벽을 향해 내려온다. false면 제자리</summary>
        public bool Moving { get; set; }
        /// <summary>true면 죽은 적을 같은 자리에 새 적으로 되살린다 (무한 샌드백)</summary>
        public bool Respawn { get; set; } = true;
        public float Speed { get => defaultSpeed; set => defaultSpeed = Mathf.Max(0, value); }
        public long TotalDamage { get; private set; }
        public int AliveCount { get { int n = 0; foreach (var e in entries) { if (!e.Model.IsDead) { n++; } } return n; } }
        public IReadOnlyList<EnemyModel> Models { get { var list = new List<EnemyModel>(); foreach (var e in entries) { list.Add(e.Model); } return list; } }

        [Inject]
        public void Construct(IPublisher<EnemyHpChanged> hpPublisher, IPublisher<EnemyDied> diedPublisher,
            ISubscriber<EnemyHpChanged> hpSubscriber, ISubscriber<EnemyDied> diedSubscriber, Wall wall = null)
        {
            this.hpPublisher = hpPublisher;
            this.diedPublisher = diedPublisher;
            this.hpSubscriber = hpSubscriber;
            this.diedSubscriber = diedSubscriber;
            this.wall = wall;
            subscription = hpSubscriber.Subscribe(OnHpChanged);
        }

        private void OnDestroy() => subscription?.Dispose();

        // 피해량 집계: 적중, 화상, 동상 등 모든 HP 감소가 EnemyHpChanged로 오므로 한 곳에서 센다.
        private void OnHpChanged(EnemyHpChanged message)
        {
            if (!lastHp.TryGetValue(message.EnemyId, out int previous)) { previous = message.Max; }
            int delta = previous - message.Current;
            lastHp[message.EnemyId] = message.Current;
            if (delta <= 0) { return; }
            TotalDamage += delta;
            recentDamage.Enqueue((Time.time, delta));
        }

        /// <summary>최근 seconds초 동안 준 피해의 초당 평균</summary>
        public float RecentDps(float seconds = 3f)
        {
            while (recentDamage.Count > 0 && Time.time - recentDamage.Peek().time > seconds) { recentDamage.Dequeue(); }
            long sum = 0;
            foreach (var item in recentDamage) { sum += item.damage; }
            return sum / Mathf.Max(.01f, seconds);
        }

        public void ResetDamage()
        {
            TotalDamage = 0;
            recentDamage.Clear();
        }

        public EnemyModel Spawn(Vector2 position, int hp, EnemyType type = EnemyType.Normal)
        {
            var model = Create(position, hp, type);
            entries.Add(new Entry { Model = model, Spawn = position, Hp = hp, Type = type });
            return model;
        }

        private EnemyModel Create(Vector2 position, int hp, EnemyType type)
        {
            int id = ++nextId;
            var attack = new EnemyAttackStats(AttackType.Melee, 0, 1f, attackRange);
            var model = new EnemyModel(id, position, defaultSpeed, type, Mathf.Max(1, hp), attack, hpPublisher, diedPublisher);
            lastHp[id] = model.MaxHp;
            if (enemyPrefab != null)
            {
                var view = Instantiate(enemyPrefab, position, Quaternion.identity, transform);
                view.Bind(model, null, hpSubscriber, diedSubscriber);
            }
            return model;
        }

        public void SpawnPattern(Pattern pattern, int count, int hp, Vector2 center)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = pattern switch
                {
                    Pattern.Line => new Vector2((i - (count - 1) * .5f) * 1.0f, 0),
                    Pattern.Cluster => UnityEngine.Random.insideUnitCircle * 0.9f,
                    _ => new Vector2(UnityEngine.Random.Range(-2.6f, 2.6f), UnityEngine.Random.Range(-1.5f, 1.5f))
                };
                Spawn(center + offset, hp);
            }
        }

        public void Clear()
        {
            foreach (var entry in entries)
            {
                // 뷰를 지우려면 죽여야 하지만, 샌드박스 정리는 피해 집계에 넣지 않는다.
                if (!entry.Model.IsDead) { entry.Model.TakeDamage(entry.Model.MaxHp); }
            }
            entries.Clear();
            lastHp.Clear();
            ResetDamage();
        }

        public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
        {
            results.Clear();
            foreach (var entry in entries)
            {
                if (!entry.Model.IsDead) { results.Add(entry.Model); }
            }
            results.Sort((a, b) => (a.Position - from).sqrMagnitude.CompareTo((b.Position - from).sqrMagnitude));
            if (results.Count > count) { results.RemoveRange(count, results.Count - count); }
            return results.Count;
        }

        private void Update() => Advance(Time.deltaTime);

        /// <summary>EnemySpawner.TickCombat과 같은 순서로 적을 진행한다: 이동(또는 제자리), 상태 틱, 죽은 적 정리.</summary>
        public void Advance(float deltaTime)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                var model = entry.Model;
                if (!model.IsDead)
                {
                    bool arrived = wall != null && model.IsInAttackRange(wall);
                    if (Moving && !arrived) { model.Move(deltaTime); }
                    model.TickStatus(deltaTime);
                }
                if (!model.IsDead) { continue; }
                entries.RemoveAt(i);
                lastHp.Remove(model.Id);
                if (Respawn) { entries.Add(new Entry { Model = Create(entry.Spawn, entry.Hp, entry.Type), Spawn = entry.Spawn, Hp = entry.Hp, Type = entry.Type }); }
            }
        }
    }
}
