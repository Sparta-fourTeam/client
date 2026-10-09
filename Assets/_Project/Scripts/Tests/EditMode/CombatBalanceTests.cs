using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Defense;
using Game.Core.Messages;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    /// <summary>저장 데이터·실제 스폰/처치 진행·스킬 프리팹으로 끝까지 진행한다. 연출과 카드 선택 시간은 측정에서 제외한다.</summary>
    public sealed class CombatBalanceTests
    {
        [TestCase(1, 1, 0, 17, "random")]
        [TestCase(1, 1, 0, 29, "random")]
        [TestCase(1, 1, 0, 43, "damage")]
        [TestCase(1, 1, 0, 71, "mixed")]
        [TestCase(2, 2, 0, 17, "mixed")]
        [TestCase(3, 3, 0, 17, "mixed")]
        public void Stages_HaveCompletableOfferedCardPaths(int stage, int account, int permanent, int seed, string policy)
        {
            var result = CombatBalanceProbe.Play(stage, account, permanent, seed, policy);
            TestContext.Out.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            Assert.IsTrue(result.Cleared, "단계별 계정의 실제 3장 후보에서 고른 경로가 막혔다");
            Assert.AreEqual(20, result.Wave);
            Assert.AreEqual(0, result.EmptyChoices, "모든 비최종 웨이브에 유효한 카드 선택이 필요하다");
            Assert.Greater(result.WallHp, 0);
            Assert.Less(result.Seconds, 420, "반복 스폰이 진행을 끝없이 늘려서는 안 된다");
            Assert.Less(result.PeakEnemies, 100, "이전 웨이브의 적이 과도하게 누적됐다");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(16)]
        [TestCase(18)]
        public void EveryPlayableSkill_DealsDamageWithItsRealPrefab(int id)
        {
            var result = CombatBalanceProbe.Measure(id, 20, 3);
            TestContext.Out.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(result));
            Assert.Greater(result.TotalDamage, 0, "장판·광선·투사체의 실제 펄스/적중을 확인한다");
        }
    }

    /// <summary>수치 조정 후 eval 또는 테스트에서 재실행하는 결정적 전투 측정기. 저장소와 실제 플레이 씬은 변경하지 않는다.</summary>
    public sealed class CombatBalanceProbe : IDisposable, IEnemyFactory, IUpgradeChoiceSource, IUpgradeState
    {
        public sealed class PlayResult
        {
            public int Stage, AccountLevel, PermanentLevel, Seed, Wave, WallHp, PeakEnemies, Spawned, Summoned, EmptyChoices;
            public string Policy;
            public float Seconds;
            public bool Cleared;
            public readonly List<string> Cards = new();
            public readonly List<float> WaveSeconds = new();
        }

        public sealed class DamageResult
        {
            public int SkillId, Targets, TotalDamage;
            public string Layout;
            public float Seconds;
        }

        private sealed class Bus<T> : ISubscriber<T>, IPublisher<T>, IBufferedPublisher<T>
        {
            private readonly List<IMessageHandler<T>> handlers = new();
            public IDisposable Subscribe(IMessageHandler<T> handler, params MessageHandlerFilter<T>[] filters)
            {
                handlers.Add(handler);
                return new Subscription(() => handlers.Remove(handler));
            }
            public void Publish(T message) { foreach (var handler in handlers.ToArray()) { handler.Handle(message); } }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly Action dispose;
            public Subscription(Action dispose) => this.dispose = dispose;
            public void Dispose() => dispose();
        }

        private sealed class RandomSource : IRandomProvider
        {
            private readonly System.Random random;
            public RandomSource(int seed) => random = new System.Random(seed);
            public float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
        }

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private const float Step = .05f;
        private readonly Scene scene;
        private readonly HashSet<GameObject> initialObjects;
        private readonly GameDataStore data = new();
        private readonly List<SkillData> catalog;
        private readonly List<SkillBase> skills = new();
        private readonly List<Enemy> enemies = new();
        private readonly Bus<WaveStarted> started = new();
        private readonly Bus<WaveCompleted> completed = new();
        private readonly Bus<EnemyDied> died = new();
        private readonly Bus<EnemyHpChanged> hp = new();
        private readonly Bus<AllEnemiesCleared> allCleared = new();
        private readonly RandomSource random;
        private readonly SkillAssetTable assets;
        private readonly Wall wall;
        private readonly SpawnArea spawnArea;
        private readonly Transform owner;
        private readonly EnemySpawner spawner;
        private readonly WaveProgress progress;
        private readonly ChildSkillCaster children;
        private readonly CardDeck deck;
        private readonly int accountLevel, permanentLevel;
        private int nextId;
        private bool pendingNext;
        private readonly PlayResult result;
        private readonly IDisposable completionSubscription, clearSubscription;

        private CombatBalanceProbe(int stageId, int level, int permanent, int seed, string policy)
        {
            accountLevel = level; permanentLevel = permanent;
            result = new PlayResult { Stage = stageId, AccountLevel = level, PermanentLevel = permanent, Seed = seed, Policy = policy };
            random = new RandomSource(seed);
            catalog = data.LoadSkills();
            assets = AssetDatabase.LoadAssetAtPath<SkillAssetTable>("Assets/_Project/Data/SkillAssetTable.asset");
            scene = SceneManager.GetActiveScene();
            initialObjects = new HashSet<GameObject>(Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            var layout = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Stage.unity");
            Vector3 spawnPosition, wallPosition, ownerPosition;
            Vector2 spawnSize;
            float wallOffset;
            try
            {
                var roots = layout.GetRootGameObjects();
                var spawn = roots.SelectMany(r => r.GetComponentsInChildren<SpawnArea>(true)).Single();
                var barrier = roots.SelectMany(r => r.GetComponentsInChildren<Wall>(true)).Single();
                var controller = roots.SelectMany(r => r.GetComponentsInChildren<SkillController>(true)).Single();
                spawnPosition = spawn.transform.position;
                spawnSize = new SerializedObject(spawn).FindProperty("_size").vector2Value;
                wallPosition = barrier.transform.position;
                wallOffset = new SerializedObject(barrier).FindProperty("_attackLineOffset").floatValue;
                ownerPosition = controller.transform.position;
            }
            finally { EditorSceneManager.ClosePreviewScene(layout); }
            owner = new GameObject("BalanceCaster").transform;
            owner.position = ownerPosition;
            wall = new GameObject("BalanceWall").AddComponent<Wall>();
            wall.transform.position = wallPosition;
            var wallSo = new SerializedObject(wall);
            wallSo.FindProperty("_attackLineOffset").floatValue = wallOffset;
            wallSo.ApplyModifiedPropertiesWithoutUndo();
            wall.Construct(new Bus<WallHpChanged>(), new Bus<WallDestroyed>(), data.Stages.GetOrThrow(stageId));
            wall.Initialize(data.Stages.GetOrThrow(stageId).WallHp);
            spawnArea = new GameObject("BalanceSpawn").AddComponent<SpawnArea>();
            spawnArea.transform.position = spawnPosition;
            var spawnSo = new SerializedObject(spawnArea);
            spawnSo.FindProperty("_size").vector2Value = spawnSize;
            spawnSo.ApplyModifiedPropertiesWithoutUndo();
            spawner = new EnemySpawner(this, EnemySpawnConfig.From(data.Stages.GetOrThrow(stageId).Spawn), spawnArea, random, started, allCleared, wall, new EnemyProjectileSystem());
            progress = new WaveProgress(started, died, new Bus<WaveGaugeChanged>(), completed);
            children = new ChildSkillCaster(id => CreateSkill(id));
            deck = new CardDeck(this, wall, data.GeneralCards, random);
            spawner.Initialize(); progress.Initialize();
            completionSubscription = completed.Subscribe(message => { if (!message.IsFinalWave) { pendingNext = true; } });
            clearSubscription = allCleared.Subscribe(_ => result.Cleared = true);
        }

        public static PlayResult Play(int stageId, int level, int permanent, int seed, string policy)
        {
            var savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            try
            {
                using var probe = new CombatBalanceProbe(stageId, level, permanent, seed, policy);
                probe.skills.Add(probe.CreateSkill(1));
                probe.StartWave();
                while (!probe.result.Cleared && !probe.wall.IsDestroyed && probe.result.Seconds < 420)
                {
                    probe.Advance();
                    if (probe.pendingNext)
                    {
                        probe.pendingNext = false;
                        var offered = probe.deck.Draw(3);
                        if (offered.Count > 0)
                        {
                            var choice = probe.Choose(offered, policy);
                            if (!probe.deck.TryApply(choice)) { throw new InvalidOperationException("선택 후보 적용 실패"); }
                            probe.result.Cards.Add(choice.IsNewWeapon ? "new:" + choice.newSkillData.id : choice.IsGeneral ? choice.GeneralCard.Id : choice.Option.id);
                        }
                        else { probe.result.EmptyChoices++; }
                        probe.StartWave();
                    }
                }
                probe.result.WallHp = probe.wall.CurrentHp;
                return probe.result;
            }
            finally { UnityEngine.Random.state = savedRandom; }
        }

        public static DamageResult Measure(int skillId, float seconds, int targetCount) => MeasurePattern(skillId, seconds, targetCount, "cluster");

        public static DamageResult MeasurePattern(int skillId, float seconds, int targetCount, string layout)
        {
            using var probe = new CombatBalanceProbe(1, 9, 0, 1, "measure");
            var skill = probe.CreateSkill(skillId);
            probe.skills.Add(skill);
            for (int i = 0; i < targetCount; i++)
            {
                var offset = layout == "lane" ? new Vector2(0, 3 + i) : new Vector2((i - (targetCount - 1) * .5f) * (layout == "spread" ? 2f : .5f), 3);
                var view = probe.Create(2, (Vector2)probe.owner.position + offset);
                typeof(EnemySpawner).GetMethod("Track", Private).Invoke(probe.spawner, new object[] { view });
                view.Attach(new EnemyModel(view.Id, 0, EnemyType.Normal, 100000, new EnemyAttackStats(AttackType.Melee, 0, 1, 0), probe.hp, probe.died), null);
            }
            for (float time = 0; time < seconds; time += Step) { probe.TickSkills(); }
            return new DamageResult { SkillId = skillId, Seconds = seconds, Targets = targetCount, Layout = layout, TotalDamage = probe.enemies.Sum(e => 100000 - e.Model.Hp) };
        }

        private void StartWave()
        {
            result.WaveSeconds.Add(result.Seconds);
            result.Wave++;
            var wave = data.Stages.GetOrThrow(result.Stage).Waves[result.Wave - 1];
            started.Publish(new WaveStarted(result.Wave, result.Wave == 20, wave.Spawns));
        }

        private void Advance()
        {
            spawner.Advance(Step);
            TickSkills();
            result.Seconds += Step;
            result.PeakEnemies = Math.Max(result.PeakEnemies, enemies.Count(e => e != null && !e.IsDead));
            foreach (var enemy in enemies.Where(e => e != null && e.IsDead).ToArray()) { Object.DestroyImmediate(enemy.gameObject); }
            enemies.RemoveAll(e => e == null);
        }

        private void TickSkills()
        {
            Physics2D.SyncTransforms();
            foreach (var skill in skills) { skill.Tick(Step); }
            TickEffects<Projectile>(); TickEffects<AreaZone>(); TickEffects<ChainBolt>(); TickEffects<ElectromagneticField>();
            foreach (var effect in Object.FindObjectsByType<HitscanEffect>(FindObjectsSortMode.None).Where(e => e.gameObject.scene == scene && !initialObjects.Contains(e.gameObject) && e.gameObject.activeInHierarchy))
            {
                var type = typeof(HitscanEffect);
                float elapsed = (float)type.GetField("elapsed", Private).GetValue(effect) + Step;
                type.GetField("elapsed", Private).SetValue(effect, elapsed);
                float delay = (float)type.GetField("hitDelay", Private).GetValue(effect);
                float release = (float)type.GetField("releaseDelay", Private).GetValue(effect);
                using var attribution = DamageAttribution.Begin((DamageSource)type.GetField("sourceSkill", Private).GetValue(effect));
                if (elapsed >= (delay >= 0 ? delay : .2f)) { type.GetMethod("Hit", Private).Invoke(effect, null); }
                if (elapsed >= (release >= 0 ? release : .6f)) { type.GetMethod("Release", Private).Invoke(effect, null); }
            }
        }

        private void TickEffects<T>() where T : Component
        {
            var tick = typeof(T).GetMethod("Tick", Private);
            foreach (var effect in Object.FindObjectsByType<T>(FindObjectsSortMode.None).Where(e => e.gameObject.scene == scene && !initialObjects.Contains(e.gameObject) && e.gameObject.activeInHierarchy))
            { tick.Invoke(effect, new object[] { Step }); }
        }

        public Enemy Create(int monsterId, Vector2 position, bool isSummoned = false)
        {
            var monster = data.Monsters.GetOrThrow(monsterId);
            var view = new GameObject("BalanceEnemy:" + monsterId).AddComponent<Enemy>();
            view.transform.position = position;
            view.gameObject.layer = LayerMask.NameToLayer("Enemy");
            view.gameObject.AddComponent<CircleCollider2D>().radius = .3f;
            view.Attach(new EnemyModel(++nextId, monster.Speed, monster.GetEnemyType(), monster.Hp,
                new EnemyAttackStats(monster.GetAttackType(), monster.Damage, monster.AttackInterval, monster.AttackRange, monster.ProjectileSpeed, monster.BurstCount, monster.BurstInterval),
                hp, died, PassiveBuilder.BuildPassives(monster, random), isSummoned, PassiveBuilder.BuildImmunities(monster), DamageProfile.From(monster), PassiveBuilder.BuildModifiers(monster)), null);
            enemies.Add(view); result.Spawned++;
            if (isSummoned) { result.Summoned++; }
            return view;
        }

        private SkillCaster CreateSkill(int id)
        {
            var definition = catalog.Single(s => s.id == id);
            var config = SkillConfig.FromDefinition(definition);
            if (permanentLevel > 0 && !definition.childOnly && !string.IsNullOrEmpty(definition.progressionId) && data.Upgrades.Contains(definition.progressionId))
            {
                var growth = data.Upgrades.GetOrThrow(definition.progressionId);
                config = PermanentSkillEffect.Apply(definition, PermanentSkillEffect.At(growth, permanentLevel));
            }
            var skill = SkillFactory.Create(definition, assets.GetPrefab(definition.assetKey), owner, (IEnemyTargetProvider)(object)spawner, wall, config);
            skill.UseChildCaster(children);
            return skill;
        }

        private SkillUpgradeChoices Choices => new(skills, catalog, this, id => assets.HasPrefab(catalog.Single(s => s.id == id).assetKey), s => s.unlockLevel <= accountLevel);
        public List<UpgradeChoice> GetUpgradeCandidates() => Choices.Candidates();
        public bool ApplyUpgradeChoice(UpgradeChoice choice)
        {
            if (!choice.IsNewWeapon) { return Choices.TryApply(choice); }
            skills.Add(CreateSkill(choice.newSkillData.id)); return true;
        }
        public int GetWeaponLevel(int id) => skills.FirstOrDefault(s => s.Data.id == id)?.Level ?? 0;
        public int GetPermanentWeaponLevel(int id) => string.IsNullOrEmpty(catalog.Single(s => s.id == id).progressionId) ? 0 : permanentLevel;
        public int GetAcquiredCount(int id, string card) => skills.FirstOrDefault(s => s.Data.id == id)?.GetAcquiredCount(card) ?? 0;

        private UpgradeChoice Choose(List<UpgradeChoice> offered, string policy)
        {
            if (policy == "random") { return offered[0]; }
            float Score(UpgradeChoice choice)
            {
                if (choice.IsGeneral) { return wall.CurrentHp < wall.MaxHp * .3f ? 20 : -1; }
                if (choice.IsNewWeapon) { return policy == "mixed" ? 10 : 0; }
                float score = 0;
                foreach (var e in choice.Option.effects)
                {
                    if (e.kind == "damage" || e.kind == "impactDamage") { score += policy == "control" ? .5f : 3; }
                    if (e.kind == "castCount" || e.kind == "projectileCount") { score += 2; }
                    if (e.kind == "attackSpeed" || e.kind == "projectileSpeed" || e.kind == "knockback") { score += policy == "control" ? 4 : 1; }
                    if (e.kind == "onEvent") { score += policy == "mixed" ? 5 : 1; }
                }
                return score;
            }
            return offered.OrderByDescending(Score).First();
        }

        public void Dispose()
        {
            completionSubscription.Dispose(); clearSubscription.Dispose(); progress.Dispose(); spawner.Dispose();
            foreach (var skill in skills) { skill.Dispose(); }
            children.Dispose();
            foreach (var created in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(g => g.scene == scene && !initialObjects.Contains(g)).ToArray())
            { if (created != null) { Object.DestroyImmediate(created); } }
        }
    }
}
