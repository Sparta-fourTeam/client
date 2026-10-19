using System;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Core.Defense;
using Game.Core.Stage;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace Game.Boot
{
    /// <summary>모든 Update가 끝난 상태를 저장하고, 씬의 정상 Start가 끝난 뒤 한 번만 복원한다.</summary>
    public sealed class BattleCheckpointDriver : MonoBehaviour
    {
        private static readonly ProfilerMarker SaveMarker = new("BattleCheckpoint.Save");
        private static readonly ProfilerMarker CaptureMarker = new("BattleCheckpoint.Capture");
        private StageManager _stage;
        private StageContext _context;
        private IBattleCheckpointStore _store;
        private SkillController _skills;
        private Wall _wall;
        private EnemySpawner _enemies;
        private WaveManager _waves;
        private WaveProgress _progress;
        private EncounteredMonsters _encountered;
        private GameDataStore _data;
        private ISceneNavigator _nav;
        private bool _ready;
        private float _untilSave;

        [Inject]
        public void Construct(StageManager stage, StageContext context, IBattleCheckpointStore store, SkillController skills,
            Wall wall, EnemySpawner enemies, WaveManager waves, WaveProgress progress, EncounteredMonsters encountered,
            GameDataStore data, ISceneNavigator nav)
        {
            _stage = stage; _context = context; _store = store; _skills = skills; _wall = wall;
            _enemies = enemies; _waves = waves; _progress = progress; _encountered = encountered; _data = data; _nav = nav;
        }
        private void Start() => InitializeAsync().Forget(Debug.LogException);
        private async UniTask InitializeAsync()
        {
            try
            {
                await UniTask.WaitUntil(() => _skills.IsReady && _wall.MaxHp > 0, cancellationToken: destroyCancellationToken);
                var checkpoint = _context.Checkpoint;
                if (checkpoint != null)
                {
                    // timeScale을 0으로 바꾼 프레임의 deltaTime은 아직 양수일 수 있다.
                    // 그 프레임의 Tick이 끝난 뒤 복원해 상태이상·공격이 한 번 더 진행되지 않게 한다.
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, destroyCancellationToken);
                    _enemies.RestoreRuntime(checkpoint.Spawns, _data.Stages.GetOrThrow(checkpoint.StageId));
                    _skills.RestoreRuntime(checkpoint.Skills, _enemies.SavedEnemy);
                    _wall.RestoreRuntime(checkpoint.Stage);
                    _waves.RestoreRuntime(checkpoint.Stage); _progress.RestoreRuntime(checkpoint.Stage);
                    _encountered.RestoreRuntime(checkpoint.Stage);
                    UnityEngine.Random.state = JsonUtility.FromJson<UnityEngine.Random.State>(checkpoint.RandomState);
                    _context.Checkpoint = null;
                    _stage.RestoreRuntime(checkpoint.Stage, _skills, _data);
                }
                _stage.PersistProgress = SaveProgressBeforeSend;
                _ready = true; _untilSave = 1f;
                QueueCheckpoint();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Debug.LogException(error); _store.Clear(); _context.Checkpoint = null;
                _context.ResumeError = "전투 기록을 복원하지 못했습니다. 로비에서 새 전투를 시작해 주세요.";
                await _nav.GoToLobby();
            }
        }
        private void LateUpdate()
        {
            if (!_ready) { return; }
            _untilSave -= Time.unscaledDeltaTime;
            if (_untilSave > 0) { return; }
            _untilSave = 1f; QueueCheckpoint();
        }
        public BattleCheckpoint Capture()
        {
            if (!_ready || string.IsNullOrEmpty(_context.BattleId) || _stage.State is StageState.Starting or StageState.Finished) { return null; }
            using var captureSample = CaptureMarker.Auto();
            var state = _stage.CaptureRuntime(_skills);
            _wall.CaptureRuntime(state); _waves.CaptureRuntime(state); _progress.CaptureRuntime(state); _encountered.CaptureRuntime(state);
            var checkpoint = new BattleCheckpoint
            {
                BattleId = _context.BattleId,
                StageId = _context.StageId,
                Stage = state,
                Spawns = _enemies.CaptureRuntime(),
                Skills = _skills.CaptureRuntime(),
                RandomState = JsonUtility.ToJson(UnityEngine.Random.state)
            };
            checkpoint.PinBattleData(_data);
            return checkpoint;
        }
        private void QueueCheckpoint()
        {
            using var saveSample = SaveMarker.Auto();
            try { var checkpoint = Capture(); if (checkpoint != null) { _store.QueueSave(checkpoint); } }
            catch (Exception error) when (error is System.IO.IOException or InvalidOperationException)
            { Debug.LogWarning($"[BattleCheckpoint] 전투 저장 실패: {error.Message}"); }
        }
        private bool SaveProgressBeforeSend()
        {
            var checkpoint = Capture();
            if (checkpoint == null) { return false; }
            try { _store.Save(checkpoint); return true; }
            catch (Exception error) when (error is System.IO.IOException or InvalidOperationException or UnauthorizedAccessException)
            { return false; }
        }
        public void SaveNow()
        {
            QueueCheckpoint();
            _store.Flush();
        }
        private void OnApplicationPause(bool paused) { if (paused) { SaveNow(); } }
        private void OnApplicationFocus(bool focused) { if (!focused) { SaveNow(); } }
        private void OnApplicationQuit() => SaveNow();
    }
}
