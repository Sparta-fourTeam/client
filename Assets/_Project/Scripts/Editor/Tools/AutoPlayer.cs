using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Boot;
using Game.Core;
using Game.Core.Defense;
using Game.Network;
using Game.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>밸런스 검증용 오토플레이. Local 백엔드로 재생 중일 때 로비 강화·전투·카드 선택을 사람처럼 반복한다.
    /// 최고 스테이지를 못 깨면 이전 스테이지를 반복 클리어하고 강화해 다시 도전한다. 결과는 Logs/AutoPlay/autoplay.log에 한 줄씩 남는다.
    /// 시작: Game.Editor.AutoPlayer.Begin(maxStage, speed, farmMax). 재생 중에만 동작한다</summary>
    public static class AutoPlayer
    {
        private static CancellationTokenSource _cts;
        private static readonly string LogPath = Path.Combine("Logs", "AutoPlay", "autoplay.log");
        public static string Status { get; private set; } = "idle";

        // 튜닝: 스테이지별 난이도 배수 k (메모리 안의 MonsterBonuses에만 적용하고 JSON은 건드리지 않는다)
        public static readonly Dictionary<int, float> Ks = new();
        private static readonly Dictionary<int, (StageDefinition Def, List<(string Stat, int Percent)> Base)> BaseBonuses = new();
        private static bool _tune, _tuneRequested;
        private static float _tuneStartK = 1f;

        public static void BeginTune(int maxStage = 40, float speed = 20f, int startStage = 0, float startK = 1f, int farmMax = 5, int farmMin = 3)
        {
            _tuneRequested = true;
            _tune = true;
            _tuneStartK = startK;
            Begin(maxStage, speed, farmMax, farmMin, startStage);
        }

        private static float CurrentK(int stage) => _tune ? (_tuneActive.TryGetValue(stage, out var v) ? v : 1f) : (Ks.TryGetValue(stage, out var known) ? known : 1f);
        private static readonly Dictionary<int, float> _tuneActive = new();

        private static void SetK(GameDataStore data, int stage, float k)
        {
            _tuneActive[stage] = k;
            var def = data.Stages.GetOrThrow(stage);
            if (!BaseBonuses.TryGetValue(stage, out var entry) || !ReferenceEquals(entry.Def, def))
            {
                entry = (def, def.MonsterBonuses.Select(b => (b.Stat, b.Percent)).ToList());
                BaseBonuses[stage] = entry;
            }

            for (int i = 0; i < def.MonsterBonuses.Count; i++)
            {
                var bonus = def.MonsterBonuses[i];
                var original = entry.Base[i];
                bonus.Percent = bonus.Stat is "Hp" or "Damage"
                    ? Mathf.RoundToInt(((1f + original.Percent / 100f) * k - 1f) * 100f)
                    : original.Percent;
            }
        }

        public static void Begin(int maxStage = 40, float speed = 8f, int farmMax = 5, int farmMin = 3, int startStage = 0)
        {
            if (!EditorApplication.isPlaying) { throw new InvalidOperationException("재생 중에만 시작할 수 있습니다"); }
            Stop();
            if (!_tuneRequested) { _tune = false; }
            _tuneRequested = false;
            _cts = new CancellationTokenSource();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            Log($"=== begin maxStage={maxStage} speed={speed} farm={farmMin}..{farmMax} start={startStage}");
            Campaign(maxStage, speed, farmMin, farmMax, startStage, _cts.Token).Forget(e => { Log("ERROR " + e); Status = "error"; });
        }

        private const string MenuRoot = "Tools/Project Nova/AutoPlay/";

        [MenuItem(MenuRoot + "Start Campaign (20x)", priority = 40)]
        private static void MenuStartCampaign() => Begin(40, 20f);

        [MenuItem(MenuRoot + "Start Difficulty Tuning (20x)", priority = 41)]
        private static void MenuStartTuning() => BeginTune(40, 20f);

        [MenuItem(MenuRoot + "Stop", priority = 42)]
        private static void MenuStop() => Stop();

        [MenuItem(MenuRoot + "Open Log", priority = 43)]
        private static void MenuOpenLog()
        {
            if (File.Exists(LogPath)) { EditorUtility.RevealInFinder(LogPath); }
        }

        // 재생 중(Local 백엔드, 로비 이후)에만 시작할 수 있다
        [MenuItem(MenuRoot + "Start Campaign (20x)", true)]
        [MenuItem(MenuRoot + "Start Difficulty Tuning (20x)", true)]
        private static bool MenuStartEnabled() => EditorApplication.isPlaying;

        [MenuItem(MenuRoot + "Stop", true)]
        private static bool MenuStopEnabled() => EditorApplication.isPlaying && _cts != null;

        // 한 번 클리어를 얻기 위해 이전 스테이지를 다시 도는 최대 횟수 (계속 못 깨는 스테이지에서 무한 반복하지 않게)
        private const int MaxFarmAttemptsPerClear = 4;

        public static void Stop()
        {
            // 전투 중 멈추면 오토플레이가 올린 배속이 남지 않게 StageManager의 원래 배속으로 되돌린다
            if (EditorApplication.isPlaying)
            {
                var manager = Resolve<StageManager>();
                if (manager != null && manager.State == StageState.Playing) { Time.timeScale = manager.Speed; }
            }

            _cts?.Cancel();
            _cts = null;
            Status = "stopped";
        }

        private static void Log(string line)
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss} {line}\n");
        }

        // ---------- 컨테이너 ----------

        private static T Resolve<T>() where T : class
        {
            var active = SceneManager.GetActiveScene();
            foreach (var scope in Object.FindObjectsByType<LifetimeScope>().Where(s => s.gameObject.scene == active && s.Container != null))
            {
                if (scope.Container.TryResolve(typeof(T), out object result)) { return result as T; }
            }

            var root = Object.FindAnyObjectByType<RootLifetimeScope>();
            return root?.Container != null && root.Container.TryResolve(typeof(T), out object value) ? value as T : null;
        }

        private static async UniTask<T> Need<T>(CancellationToken ct, float seconds = 30f) where T : class
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var value = Resolve<T>();
                if (value != null) { return value; }
                if (Time.realtimeSinceStartup > end) { throw new TimeoutException(typeof(T).Name + " 해석 실패"); }
                await UniTask.Yield();
            }
        }

        private static async UniTask Wait(float seconds, CancellationToken ct) =>
            await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: ct);

        // ---------- 캠페인 ----------

        private static async UniTask Campaign(int maxStage, float speed, int farmMin, int farmMax, int startStage, CancellationToken ct)
        {
            Application.runInBackground = true;
            var nav = await Need<ISceneNavigator>(ct);
            await ToLobby(nav, ct);
            var profile = await Need<PlayerProfile>(ct);
            var data = await Need<GameDataStore>(ct);

            int stage = startStage > 0 ? startStage : Math.Min(profile.HighestUnlockedStage, maxStage);
            float k = _tuneStartK;
            while (stage <= maxStage)
            {
                if (!_tune)
                {
                    int plain = await Attempt(stage, speed, nav, profile, data, farmMin, farmMax, ct);
                    Log($"STAGE {stage} {(plain >= 0 ? "CLEARED_AFTER_FARMS=" + plain : "FAILED_AFTER_FARMS=" + farmMax)}");
                    if (plain < 0) { Status = "stuck at " + stage; return; }
                    stage++;
                    continue;
                }

                // 튜닝: 스테이지 난이도 배수 k를 조절해 "못 깨면 이전 스테이지 3~5회 뒤 깬다" 범위에 맞춘다
                int tries = 0;
                while (true)
                {
                    SetK(data, stage, k);
                    int farms = await Attempt(stage, speed, nav, profile, data, farmMin, farmMax, ct);
                    tries++;
                    Log($"TUNE stage={stage} k={k:F3} farms={farms} tries={tries}");
                    if (farms >= 0)
                    {
                        Ks[stage] = k;
                        k *= farms == 0 ? 1.12f : farms <= 2 ? 1.05f : 1f;
                        break;
                    }

                    k *= 0.85f;
                    if (tries >= 8) { Status = "tune stuck at " + stage; Log("TUNE_STUCK " + stage); return; }
                }

                Log($"FINALK stage={stage} k={Ks[stage]:F3}");
                File.WriteAllText(Path.Combine("Logs", "AutoPlay", "tune.json"),
                    "{" + string.Join(",", Ks.OrderBy(pair => pair.Key).Select(pair => $"\"{pair.Key}\":{pair.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}")) + "}");
                stage++;
            }

            Status = "done";
            Log("=== done");
        }

        // 처음 도전 -> (실패하면) 강화 뒤 재도전 -> 이전 스테이지 3~5회 반복 뒤 재도전. 몇 번째 반복에서 깼는지(0이면 반복 없이), 못 깼으면 -1
        private static async UniTask<int> Attempt(int stage, float speed, ISceneNavigator nav, PlayerProfile profile, GameDataStore data,
            int farmMin, int farmMax, CancellationToken ct)
        {
            SetK(data, stage, Ks.TryGetValue(stage, out var known) && !_tune ? known : CurrentK(stage));
            var first = await Run(stage, speed, nav, ct, "try");
            if (first.Cleared) { return 0; }

            bool cleared = false;
            int farms = 0;
            int bought = await Prepare(profile, data, ct);
            if (bought > 0)
            {
                cleared = (await Run(stage, speed, nav, ct, "retryUpgrade", prepare: false)).Cleared;
            }

            // farms는 이전 스테이지를 실제로 클리어한 횟수다. 실패한 판은 세지 않고, 한 번도 못 깨는 스테이지에서 멈추지 않도록 시도 횟수에 상한을 둔다
            int attempts = 0;
            while (farms < farmMax && !cleared)
            {
                int farmStage = Math.Max(1, stage - 1);
                int target = farms < farmMin ? farmMin : farms + 1;
                while (farms < target && attempts < target * MaxFarmAttemptsPerClear)
                {
                    SetK(data, farmStage, Ks.TryGetValue(farmStage, out var farmK) ? farmK : 1f);
                    attempts++;
                    if ((await Run(farmStage, speed, nav, ct, "farm")).Cleared) { farms++; }
                }

                SetK(data, stage, CurrentK(stage));
                cleared = (await Run(stage, speed, nav, ct, "retry" + farms)).Cleared;
                if (!cleared && farms < target) { break; }
            }

            return cleared ? farms : -1;
        }

        private struct RunResult { public bool Cleared; }

        private static async UniTask<int> Prepare(PlayerProfile profile, GameDataStore data, CancellationToken ct)
        {
            // 에너지 충전
            var store = await Need<LocalSaveStore>(ct);
            var playerApi = await Need<IPlayerApi>(ct);
            var save = store.Load();
            PlaytestCheats.FillEnergy(save, data.Energy);
            store.Flush(save);
            profile.Apply(await playerApi.GetMe());

            // 별 보상 수령
            var stageApi = await Need<IStageApi>(ct);
            foreach (var stageDef in data.Stages.Values.OrderBy(s => s.Id))
            {
                if (profile.ClearRating(stageDef.Id) > profile.ClaimedRating(stageDef.Id))
                {
                    try { profile.Apply(await stageApi.ClaimRatingReward(stageDef.Id)); }
                    catch (Exception e) { Log("claim fail " + stageDef.Id + " " + e.Message); }
                }
            }

            // 강화: 사 줄 수 있는 것을 싼 것부터 한 단계씩 반복해서 모두 산다
            var upgradeApi = await Need<IUpgradeApi>(ct);
            int bought = 0;
            for (bool any = true; any;)
            {
                any = false;
                var plans = data.Upgrades.Values
                    .Select(def => UpgradePurchasePlan.Create(profile, data, def.UpgradeId, false))
                    .Where(p => p.Count > 0)
                    .OrderBy(p => p.CoinCost).ToList();
                foreach (var plan in plans)
                {
                    var now = UpgradePurchasePlan.Create(profile, data, plan.UpgradeId, false);
                    if (now.Count == 0) { continue; }
                    try
                    {
                        profile.Apply(await upgradeApi.Purchase(now.UpgradeId, Guid.NewGuid().ToString("N")));
                        bought++;
                        any = true;
                    }
                    catch (Exception e) { Log("upgrade fail " + now.UpgradeId + " " + e.Message); }
                }
            }

            Log($"  prepared: lv={profile.Level} gold={profile.Gold} bought={bought}");
            return bought;
        }

        private static async UniTask<RunResult> Run(int stageId, float speed, ISceneNavigator nav, CancellationToken ct, string tag, bool prepare = true)
        {
            var profile = await Need<PlayerProfile>(ct);
            var data = await Need<GameDataStore>(ct);
            if (prepare) { await Prepare(profile, data, ct); }
            else
            {
                // 직전 Prepare 뒤라 강화는 끝났지만 에너지는 다시 채운다
                var store = await Need<LocalSaveStore>(ct);
                var save = store.Load();
                PlaytestCheats.FillEnergy(save, data.Energy);
                store.Flush(save);
                profile.Apply(await (await Need<IPlayerApi>(ct)).GetMe());
            }
            int gold0 = profile.Gold, exp0 = profile.Exp, lv0 = profile.Level;
            var launcher = await Need<BattleLauncher>(ct);
            float realStart = Time.realtimeSinceStartup;
            Status = $"stage {stageId} ({tag})";
            if (!await launcher.Launch(stageId)) { throw new InvalidOperationException("전투 발급 거절: stage " + stageId); }

            var manager = await Need<StageManager>(ct);
            int minWall = 100, cards = 0;
            var picked = new List<string>();
            var deadline = Time.realtimeSinceStartup + 1500f;
            while (manager.State != StageState.Finished)
            {
                ct.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup > deadline) { throw new TimeoutException("전투 제한 시간 초과 stage " + stageId); }
                switch (manager.State)
                {
                    case StageState.Playing:
                        Time.timeScale = speed;
                        var wall = Object.FindAnyObjectByType<Wall>();
                        if (wall != null && wall.MaxHp > 0) { minWall = Math.Min(minWall, wall.CurrentHp * 100 / wall.MaxHp); }
                        break;
                    case StageState.CardSelect:
                        int index = ChooseCard(manager.Choices);
                        if (index >= 0)
                        {
                            picked.Add(Describe(manager.Choices[index]));
                            cards++;
                            manager.PickCard(index);
                        }
                        break;
                    case StageState.EliteRoulette:
                        for (int guard = 0; guard < 12 && !manager.Roulette.IsComplete; guard++)
                        {
                            var round = manager.Roulette.PrepareRound();
                            if (round == null) { break; }
                            if (manager.ApplyRouletteReward(round)) { picked.Add("roulette:" + round.Winner.Option.id); }
                        }
                        manager.FinishEliteRoulette();
                        break;
                    case StageState.Starting:
                        if (Resolve<StageContext>()?.Checkpoint != null) { throw new InvalidOperationException("체크포인트 재개 상태"); }
                        break;
                }

                await UniTask.Yield();
            }

            var walls = Object.FindAnyObjectByType<Wall>();
            bool cleared = walls != null && !walls.IsDestroyed;
            var stats = await Need<BattleStats>(ct);
            // 결과 화면을 거치지 않고 로비로 돌아간다. 보상 반영은 서버(Local) 응답을 프로필이 받은 뒤다
            await UniTask.Delay(300, DelayType.Realtime, cancellationToken: ct);
            var playerApi = await Need<IPlayerApi>(ct);
            Time.timeScale = 1f;
            int reached = stats.ReachedWave, kills = stats.Kills;
            profile.Apply(await playerApi.GetMe());
            Log($"RUN stage={stageId} {tag} {(cleared ? "CLEAR" : "FAIL")} wave={reached} kills={kills} minWall={minWall}% lv={lv0}->{profile.Level} gold={gold0}->{profile.Gold} exp+={profile.Exp - exp0} real={Time.realtimeSinceStartup - realStart:F0}s cards={cards}");
            await ToLobby(nav, ct);
            return new RunResult { Cleared = cleared };
        }

        private static async UniTask ToLobby(ISceneNavigator nav, CancellationToken ct)
        {
            float end = Time.realtimeSinceStartup + 120f;
            if (nav.Current == SceneId.Stage) { await nav.GoToLobby(); }
            while (nav.Current != SceneId.Lobby && SceneManager.GetActiveScene().name != "Lobby")
            {
                ct.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup > end) { throw new TimeoutException("로비 진입 실패 " + nav.Current); }
                if (nav.Current is SceneId.Loading or SceneId.Boot) { Tap(); }
                await UniTask.Yield();
            }

            await Wait(1.0f, ct);
            // 로비 진입 직후 프로필 로드 대기
            var profile = await Need<PlayerProfile>(ct);
            while (!profile.IsLoaded) { await UniTask.Yield(); }
        }

        private static void Tap()
        {
            var view = Object.FindAnyObjectByType<TapToStartView>();
            if (view == null) { return; }
            var field = typeof(TapToStartView).GetField("_tapArea", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(view) is Button button && button.gameObject.activeInHierarchy) { button.onClick.Invoke(); }
        }

        // ---------- 카드 선택 ----------

        private static int ChooseCard(IReadOnlyList<UpgradeChoice> choices)
        {
            int best = -1;
            float bestScore = float.MinValue;
            var wall = Object.FindAnyObjectByType<Wall>();
            float wallRatio = wall == null || wall.MaxHp == 0 ? 1f : (float)wall.CurrentHp / wall.MaxHp;
            for (int i = 0; i < choices.Count; i++)
            {
                float score = Score(choices[i], wallRatio);
                if (score > bestScore) { best = i; bestScore = score; }
            }

            return best;
        }

        private static float Score(UpgradeChoice choice, float wallRatio)
        {
            if (choice.IsGeneral) { return wallRatio < 0.6f ? 90f : 5f; }
            if (choice.IsNewWeapon) { return 60f; }
            float score = 40f;
            foreach (var effect in choice.Option?.effects ?? new List<EffectDef>())
            {
                score += effect.kind switch
                {
                    "damage" => Mathf.Min(effect.value, 100f) * 0.3f,
                    "attackSpeed" => Mathf.Min(effect.value, 100f) * 0.3f,
                    "castCount" => 25f,
                    "projectileCount" => 20f,
                    "beamPulses" or "beamPulsesFlat" or "chainBounces" => 10f,
                    _ => 0f,
                };
            }

            return score;
        }

        private static string Describe(UpgradeChoice choice) =>
            choice.IsGeneral ? "general:" + choice.GeneralCard.Id
            : choice.IsNewWeapon ? "new:" + choice.newSkillData.id
            : choice.skill.Data.id + ":" + choice.Option.id;
    }
}
