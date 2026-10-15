using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core;
using Game.Core.Combat;
using Game.Core.Messages;
using MessagePipe;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace Game.Sandbox
{
    /// <summary>실제 적·공격·메시지·표시 경로를 고정 프레임 부하로 측정한다. 출시 빌드에는 포함되지 않는다.</summary>
    public sealed class CombatBenchmark : LifetimeScope, IEnemyTargetProvider
    {
        public enum Workload { Projectiles, Hitscan, Area, Beam, Chain, SimultaneousDeaths }
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private HitscanEffect hitscanPrefab;
        [SerializeField] private AreaZone areaPrefab, beamPrefab;
        [SerializeField] private ChainBolt chainPrefab;
        [SerializeField] private string runLabel = "after";
        [SerializeField] private int enemyCount = 120;
        [SerializeField] private int projectilesPerVolley = 48;
        [SerializeField] private int volleyEveryFrames = 15;
        [SerializeField] private int warmupFrames = 300;
        [SerializeField] private int measurementFrames = 900;
        [SerializeField] private int repetitions = 3;
        [SerializeField] private int seed = 20261015;
        [SerializeField] private bool screenshots = true;

        private static readonly string[] TimingMarkers =
        {
            "Enemy.DeathPresentation", "Enemy.DeathEffect.Load", "Enemy.DeathEffect.Create", "Enemy.DeathEffect.Setup",
            "Enemy.DamagePopup", "DamagePopupPool.Load", "DamagePopupPool.Create", "DamagePopupPool.Show",
            "DamagePopup.SetText", "TMP Layout Text", "TMP.SetArraySizes", "HitscanEffect.QueryTargets", "HitscanEffect.ResolveHits"
        };
        private ProfilerRecorder[] timingRecorders;
        private long[][] timingSamples;
        private Workload[] workloads;
        [SerializeField] private bool showPopups = true, warmPopups, deathsOnly;
        private IPublisher<EnemyHpChanged> hpPublisher;
        private IPublisher<EnemyDied> diedPublisher;
        private ISubscriber<EnemyHpChanged> hpSubscriber;
        private ISubscriber<EnemyDied> diedSubscriber;
        private IDisposable hpSubscription, deathSubscription;
        private Enemy[] enemies;
        private GameObject fieldRoot, attackRoot;
        private ObjectPool<Projectile> projectilePool;
        private ObjectPool<HitscanEffect> hitscanPool;
        private ObjectPool<AreaZone> areaPool, beamPool;
        private ObjectPool<ChainBolt> chainPool;
        private readonly List<AreaZone> areas = new();
        private readonly List<ChainBolt> chains = new();
        private AttackReactions damageReactions;
        private readonly List<Projectile> projectiles = new();
        private readonly List<HitscanEffect> hitscans = new();
        private readonly List<Trial> trials = new();
        private readonly List<Frame> frames = new();
        private ProjectileSpawnSettings spawnSettings;
        private ProfilerRecorder gcAllocation, gcTime;
        private Comparison<IEnemyTarget> compareTargets;
        private Vector2 queryOrigin;
        private long previousTimestamp;
        private int nextEnemyId, hpEvents, deaths, attackRequests;
        private bool running, measuring, interrupted, initialized;
        private int previousFps, previousVsync;
        private float previousCaptureDelta;
        private Random.State previousRandom;
        private bool previousPopups, previousStatuses;
        private string output, status = "Preparing";

        [Serializable]
        private struct Frame
        {
            public string workload;
            public int repetition, frame;
            public double wallMs;
            public long allocatedBytes, gcNanoseconds;
        }
        [Serializable]
        private sealed class Trial
        {
            public string workload;
            public int repetition, frames, attacks, hpEvents, deaths, collections;
            public double meanMs, p50Ms, p95Ms, p99Ms, maxMs;
            public long allocatedBytes, gcNanoseconds;
        }
        [Serializable]
        private sealed class Report
        {
            public string label, utc, unity, device, os, gpu, graphicsApi, resolution, scope, timing;
            public bool complete, interrupted, allocationAvailable, gcTimeAvailable;
            public int enemyCount, projectilesPerVolley, volleyEveryFrames, warmupFrames, measurementFrames, repetitions, seed;
            public string enemyPrefab, projectilePrefab, hitscanPrefab, areaPrefab, beamPrefab, chainPrefab;
            public string[] workloads, timingMarkers;
            public bool showPopups, warmPopups;
            public Trial[] trials;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterMessagePipe();
            builder.RegisterBuildCallback(resolver =>
            {
                hpPublisher = resolver.Resolve<IPublisher<EnemyHpChanged>>();
                diedPublisher = resolver.Resolve<IPublisher<EnemyDied>>();
                hpSubscriber = resolver.Resolve<ISubscriber<EnemyHpChanged>>();
                diedSubscriber = resolver.Resolve<ISubscriber<EnemyDied>>();
            });
        }

        private IEnumerator Start()
        {
            if (enemyPrefab == null || projectilePrefab == null || hitscanPrefab == null || areaPrefab == null || beamPrefab == null || chainPrefab == null || enemyCount < 1 ||
                measurementFrames < 1 || warmupFrames < 1 || repetitions < 1 || volleyEveryFrames < 1 || projectilesPerVolley < 1)
            { status = "Invalid benchmark configuration"; Debug.LogError(status); yield break; }
            var args = Environment.GetCommandLineArgs();
            showPopups &= Array.IndexOf(args, "-novaNoPopups") < 0;
            warmPopups |= Array.IndexOf(args, "-novaWarmPopups") >= 0;
            workloads = deathsOnly || Array.IndexOf(args, "-novaDeathsOnly") >= 0
                ? new[] { Workload.SimultaneousDeaths } : (Workload[])Enum.GetValues(typeof(Workload));
            if (Array.IndexOf(args, "-novaQuickBenchmark") >= 0)
            { warmupFrames = 60; measurementFrames = 120; screenshots = false; }
            runLabel += showPopups ? warmPopups ? "-warm-popups" : "-cold-popups" : "-no-popups";
            initialized = true;
            previousFps = Application.targetFrameRate; previousVsync = QualitySettings.vSyncCount;
            previousCaptureDelta = Time.captureDeltaTime; previousRandom = Random.state;
            previousPopups = DamagePopupPool.Enabled; previousStatuses = EnemyStatusEffects.Enabled;
            Application.targetFrameRate = 60; QualitySettings.vSyncCount = 0;
            // 시뮬레이션 부하는 고정한다. 실제 시간은 deltaTime 대신 Stopwatch로 측정한다.
            Time.captureDeltaTime = 1f / 60f;
            DamagePopupPool.Enabled = showPopups; EnemyStatusEffects.Enabled = true;
            output = Path.Combine(Application.persistentDataPath, "CombatBenchmarks", runLabel + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(output);
            frames.Capacity = measurementFrames * repetitions * workloads.Length;
            timingRecorders = new ProfilerRecorder[TimingMarkers.Length];
            timingSamples = new long[TimingMarkers.Length][];
            for (int i = 0; i < TimingMarkers.Length; i++)
            {
                timingRecorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, TimingMarkers[i], 1,
                    ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                timingSamples[i] = new long[frames.Capacity];
            }
            enemies = new Enemy[enemyCount];
            compareTargets = CompareTargets;
            hpSubscription = hpSubscriber.Subscribe(message => { if (measuring) { hpEvents++; } });
            deathSubscription = diedSubscriber.Subscribe(message => { if (measuring) { deaths++; } });
            gcAllocation = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            gcTime = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Collect", 1);
            running = true;
            foreach (Workload workload in workloads)
            {
                for (int repeat = 0; repeat < repetitions; repeat++)
                {
                    status = workload + " warmup " + (repeat + 1);
                    yield return ResetField();
                    Random.InitState(seed);
                    for (int f = 0; f < warmupFrames; f++)
                    {
                        Drive(workload, f);
                        yield return null;
                        if (interrupted) { Finish(false); yield break; }
                    }
                    if (workload == Workload.SimultaneousDeaths) { yield return ResetField(); }
                    hpEvents = deaths = attackRequests = 0;
                    int firstFrame = frames.Count;
                    int initialCollections = GC.CollectionCount(0);
                    status = workload + " measuring " + (repeat + 1);
                    string workloadName = workload.ToString();
                    measuring = true;
                    for (int f = 0; f < measurementFrames; f++)
                    {
                        previousTimestamp = Stopwatch.GetTimestamp();
                        Drive(workload, f);
                        yield return null;
                        for (int m = 0; m < timingRecorders.Length; m++)
                        { timingSamples[m][frames.Count] = timingRecorders[m].Valid ? timingRecorders[m].LastValue : -1; }
                        frames.Add(new Frame
                        {
                            workload = workloadName,
                            repetition = repeat + 1,
                            frame = f,
                            wallMs = (Stopwatch.GetTimestamp() - previousTimestamp) * 1000d / Stopwatch.Frequency,
                            allocatedBytes = gcAllocation.Valid ? gcAllocation.LastValue : -1,
                            gcNanoseconds = gcTime.Valid ? gcTime.LastValue : -1
                        });
                        if (interrupted) { measuring = false; Finish(false); yield break; }
                    }
                    measuring = false;
                    Summarize(workload, repeat + 1, firstFrame, GC.CollectionCount(0) - initialCollections);
                    Save(false);
                }
                // 스크린샷 읽기/쓰기 비용은 측정 구간에 넣지 않는다.
                if (screenshots)
                {
                    status = workload + " evidence only";
                    yield return ResetField();
                    Random.InitState(seed);
                    Capture(workload + "_prepared");
                    for (int f = 0; f < 120; f++)
                    {
                        Drive(workload, f);
                        yield return null;
                        if (f == 0 || f == 15 || f == 60) { Capture(workload + "_frame" + f); }
                    }
                }
            }
            Finish(true);
        }

        private IEnumerator ResetField()
        {
            projectilePool?.Clear(); hitscanPool?.Clear(); areaPool?.Clear(); beamPool?.Clear(); chainPool?.Clear();
            foreach (var a in areas) { if (a != null) { Destroy(a.gameObject); } }
            foreach (var c in chains) { if (c != null) { Destroy(c.gameObject); } }
            areas.Clear(); chains.Clear();
            foreach (var p in projectiles) { if (p != null) { Destroy(p.gameObject); } }
            foreach (var h in hitscans) { if (h != null) { Destroy(h.gameObject); } }
            projectiles.Clear(); hitscans.Clear();
            if (fieldRoot != null) { Destroy(fieldRoot); }
            if (attackRoot != null) { Destroy(attackRoot); }
            DamagePopupPool.Clear();
            // 사망 이펙트의 잔여 파티클을 다음 시행에 섞지 않는다.
            yield return new WaitForSecondsRealtime(3);
            fieldRoot = new GameObject("BenchmarkEnemies"); fieldRoot.transform.SetParent(transform);
            attackRoot = new GameObject("BenchmarkAttacks"); attackRoot.transform.SetParent(transform);
            for (int i = 0; i < enemies.Length; i++)
            {
                var position = PositionAt(i);
                var enemy = Instantiate(enemyPrefab, position, Quaternion.identity, fieldRoot.transform);
                var model = new EnemyModel(++nextEnemyId, 0f, EnemyType.Elite, int.MaxValue,
                    new EnemyAttackStats(AttackType.Melee, 0, 1f, 0f), hpPublisher, diedPublisher);
                enemy.Bind(model, null, hpSubscriber, diedSubscriber);
                enemies[i] = enemy;
            }
            projectilePool = new ObjectPool<Projectile>(() =>
            {
                var p = Instantiate(projectilePrefab, attackRoot.transform); projectiles.Add(p); return p;
            }, p => p.gameObject.SetActive(true), p => p.gameObject.SetActive(false), p => Destroy(p.gameObject));
            hitscanPool = new ObjectPool<HitscanEffect>(() =>
            {
                var h = Instantiate(hitscanPrefab, attackRoot.transform); hitscans.Add(h); return h;
            }, h => h.gameObject.SetActive(true), h => h.gameObject.SetActive(false), h => Destroy(h.gameObject));
            areaPool = CreateAreaPool(areaPrefab); beamPool = CreateAreaPool(beamPrefab);
            chainPool = new ObjectPool<ChainBolt>(() =>
            {
                var c = Instantiate(chainPrefab, attackRoot.transform); chains.Add(c); return c;
            }, c => c.gameObject.SetActive(true), c => c.gameObject.SetActive(false), c => Destroy(c.gameObject));
            damageReactions = new AttackReactions(new[] { new ReactionBinding(AttackEvent.Hit, new DamageReaction(10, isImpact: true)) });
            spawnSettings = new ProjectileSpawnSettings
            {
                Speed = 16f,
                Lifetime = 2f,
                PierceCount = 4,
                CollisionRadius = .3f,
                TargetProvider = this,
                HitReactions = damageReactions
            };
            if (showPopups && warmPopups)
            {
                for (int i = 0; i < DamagePopupPool.MaxActive; i++)
                { DamagePopupPool.Show(int.MaxValue, PositionAt(i), gameObject.scene); }
                // 실제 TMP 메시 생성과 트윈 반환을 기다린다. 느린 기기에서도 고정 대기로 준비 완료를 추정하지 않는다.
                var popups = FindAnyObjectByType<DamagePopupPool>();
                float deadline = Time.realtimeSinceStartup + 5f;
                while (popups != null && popups.ActiveCount > 0 && Time.realtimeSinceStartup < deadline)
                { yield return null; }
                if (popups == null || popups.ActiveCount != 0 || popups.FreeCount != DamagePopupPool.MaxActive)
                {
                    interrupted = true;
                    Debug.LogError("[CombatBenchmark] Popup warmup did not finish; run is invalid.");
                    yield break;
                }
            }
            yield return null;
        }

        private ObjectPool<AreaZone> CreateAreaPool(AreaZone prefab) => new ObjectPool<AreaZone>(() =>
        {
            var a = Instantiate(prefab, attackRoot.transform); areas.Add(a); return a;
        }, a => a.gameObject.SetActive(true), a => a.gameObject.SetActive(false), a => Destroy(a.gameObject));

        private Vector3 PositionAt(int i) => new Vector3((i % 12 - 5.5f) * .36f, .7f + i / 12 * .38f, 0);

        private void Drive(Workload workload, int frame)
        {
            if (workload == Workload.SimultaneousDeaths)
            {
                if (frame != 0) { return; }
                for (int i = 0; i < enemies.Length; i++)
                {
                    if (enemies[i] != null && !enemies[i].IsDead)
                    { enemies[i].TakeDamage(new DamageInfo(int.MaxValue, skillId: 1, isImpact: true)); if (measuring) { attackRequests++; } }
                }
                return;
            }
            if (frame % volleyEveryFrames != 0) { return; }
            using var source = DamageAttribution.Begin(new DamageSource(1));
            if (workload == Workload.Projectiles)
            {
                for (int i = 0; i < projectilesPerVolley; i++)
                {
                    spawnSettings.StartPos = new Vector3((i % 12 - 5.5f) * .36f, -.8f, 0);
                    spawnSettings.Direction = Vector3.up;
                    projectilePool.Get().Init(projectilePool, spawnSettings);
                    if (measuring) { attackRequests++; }
                }
            }
            else if (workload == Workload.Hitscan)
            {
                // 실제 HitscanEffect의 지연 Hit와 범위 판정/피해/반응 경로를 실행한다.
                hitscanPool.Get().Init(hitscanPool, new Vector3(0, 2.5f, 0), 10, impactRadius: 8f);
                if (measuring) { attackRequests++; }
            }
            else
            {
                // 실제 영역·광선·연쇄 컴포넌트의 Update, 판정, 피해 및 VFX를 실행한다.
                for (int i = 0; i < 4; i++)
                {
                    var target = enemies[(frame / volleyEveryFrames * 4 + i) % enemies.Length];
                    if (workload == Workload.Area)
                    {
                        areaPool.Get().Init(areaPool, this, target.Position, new AreaSettings(1.2f, 1f, .2f), damageReactions, AttackReactions.Empty);
                    }
                    else if (workload == Workload.Beam)
                    {
                        beamPool.Get().Init(beamPool, this, new Vector2((i - 1.5f) * .6f, -.8f),
                            new AreaSettings(0, 1f, .2f, length: 8f, width: .6f, baseDamage: 10), damageReactions, AttackReactions.Empty, aimTarget: target);
                    }
                    else if (workload == Workload.Chain)
                    {
                        chainPool.Get().Init(chainPool, this, new Vector2(0, -.8f), target,
                            new ChainSettings(12, .03f), damageReactions, AttackReactions.Empty);
                    }

                    if (measuring) { attackRequests++; }
                }
            }
        }

        public int GetNearest(Vector2 from, int count, List<IEnemyTarget> results)
        {
            results.Clear();
            for (int i = 0; i < enemies.Length; i++) { var e = enemies[i]; if (e != null && !e.IsDead) { results.Add(e); } }
            queryOrigin = from; results.Sort(compareTargets);
            if (results.Count > count) { results.RemoveRange(count, results.Count - count); }
            return results.Count;
        }
        private int CompareTargets(IEnemyTarget a, IEnemyTarget b) =>
            (a.Position - queryOrigin).sqrMagnitude.CompareTo((b.Position - queryOrigin).sqrMagnitude);

        private void Summarize(Workload workload, int repeat, int first, int collections)
        {
            var sorted = new double[frames.Count - first];
            var trial = new Trial
            {
                workload = workload.ToString(),
                repetition = repeat,
                frames = sorted.Length,
                attacks = attackRequests,
                hpEvents = hpEvents,
                deaths = deaths,
                collections = collections
            };
            for (int i = 0; i < sorted.Length; i++)
            {
                var f = frames[first + i]; sorted[i] = f.wallMs; trial.meanMs += f.wallMs;
                if (f.allocatedBytes >= 0) { trial.allocatedBytes += f.allocatedBytes; }
                if (f.gcNanoseconds >= 0) { trial.gcNanoseconds += f.gcNanoseconds; }
            }
            Array.Sort(sorted); trial.meanMs /= sorted.Length;
            trial.p50Ms = Percentile(sorted, .5); trial.p95Ms = Percentile(sorted, .95); trial.p99Ms = Percentile(sorted, .99);
            trial.maxMs = sorted[sorted.Length - 1]; trials.Add(trial);
        }
        private static double Percentile(double[] sorted, double p) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * p) - 1)];
        private void Capture(string name)
        {
            var path = Path.Combine(output, name + ".png");
            // 모바일 CaptureScreenshot은 persistentDataPath를 자체적으로 앞에 붙인다.
            ScreenCapture.CaptureScreenshot(Application.isMobilePlatform
                ? Path.GetRelativePath(Application.persistentDataPath, path) : path);
        }
        private void Finish(bool complete)
        {
            measuring = running = false; status = complete ? "Complete — " + output : "Interrupted — invalid run";
            Save(complete); RestoreSettings(); Debug.Log("[CombatBenchmark] " + status);
        }
        private void Save(bool complete)
        {
            var report = new Report
            {
                label = runLabel,
                utc = DateTime.UtcNow.ToString("O"),
                unity = Application.unityVersion,
                device = SystemInfo.deviceModel,
                os = SystemInfo.operatingSystem,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                resolution = Screen.width + "x" + Screen.height,
                scope = "Synthetic stress: actual Enemy, Projectile, HitscanEffect, AreaZone (circle and beam), ChainBolt, MessagePipe and presentation. No StageManager, rewards, saves or maximum-upgrade skill build.",
                timing = "Fixed simulation delta 1/60; Stopwatch wall-clock frame intervals; allocations cover all threads. Evidence screenshots outside measurement.",
                complete = complete,
                interrupted = interrupted,
                allocationAvailable = gcAllocation.Valid,
                gcTimeAvailable = gcTime.Valid,
                enemyCount = enemyCount,
                projectilesPerVolley = projectilesPerVolley,
                volleyEveryFrames = volleyEveryFrames,
                warmupFrames = warmupFrames,
                measurementFrames = measurementFrames,
                repetitions = repetitions,
                seed = seed,
                enemyPrefab = enemyPrefab.name,
                projectilePrefab = projectilePrefab.name,
                hitscanPrefab = hitscanPrefab.name,
                areaPrefab = areaPrefab.name,
                beamPrefab = beamPrefab.name,
                chainPrefab = chainPrefab.name,
                workloads = Array.ConvertAll(workloads, value => value.ToString()),
                timingMarkers = TimingMarkers,
                showPopups = showPopups,
                warmPopups = warmPopups,
                trials = trials.ToArray()
            };
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonUtility.ToJson(report, true));
            var csv = new StringBuilder("workload,repetition,frame,wall_ms,gc_allocated_bytes,gc_collect_ns");
            foreach (var marker in TimingMarkers) { csv.Append(',').Append(marker).Append("_ns"); }
            csv.Append('\n');
            for (int i = 0; i < frames.Count; i++)
            {
                var f = frames[i];
                csv.Append(f.workload).Append(',').Append(f.repetition).Append(',').Append(f.frame).Append(',').Append(f.wallMs.ToString("F5", CultureInfo.InvariantCulture)).Append(',').Append(f.allocatedBytes).Append(',').Append(f.gcNanoseconds);
                for (int m = 0; m < TimingMarkers.Length; m++) { csv.Append(',').Append(timingSamples[m][i]); }
                csv.Append('\n');
            }
            File.WriteAllText(Path.Combine(output, "frames.csv"), csv.ToString());
        }
        private void OnApplicationPause(bool paused) { if (paused && running) { interrupted = true; } }
        private void OnApplicationFocus(bool focus) { if (!focus && running && !Application.isEditor) { interrupted = true; } }
        private void RestoreSettings()
        {
            if (!initialized) { return; }
            initialized = false;
            Application.targetFrameRate = previousFps; QualitySettings.vSyncCount = previousVsync;
            Time.captureDeltaTime = previousCaptureDelta; Random.state = previousRandom;
            DamagePopupPool.Enabled = previousPopups; EnemyStatusEffects.Enabled = previousStatuses;
        }
        protected override void OnDestroy()
        {
            RestoreSettings(); hpSubscription?.Dispose(); deathSubscription?.Dispose();
            gcAllocation.Dispose(); gcTime.Dispose();
            if (timingRecorders != null) { for (int i = 0; i < timingRecorders.Length; i++) { timingRecorders[i].Dispose(); } }
            base.OnDestroy();
        }
        private void OnGUI()
        {
            if (measuring) { return; }
            GUI.Label(new Rect(12, 12, Screen.width - 24, 90), "Combat benchmark " + runLabel + "\n" + status);
        }
    }
}
