using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Messages;
using UnityEngine;

namespace Game.Core
{
    public sealed partial class StageManager
    {
        private readonly IBattleCheckpointStore _checkpoints;
        public StageRuntimeState CaptureRuntime(SkillController skills)
        {
            var s = new StageRuntimeState
            {
                State = State,
                BeforePause = _beforePause,
                Speed = _speed,
                PendingElites = _pendingElites,
                PendingCards = _pendingCardSelections,
                PendingRequest = _pendingRequest,
                PendingProgress = _pendingProgress,
                ProgressRevision = _progressRevision,
                ProgressSourceRevision = (_battleApi as IBattleProgressApi)?.SourceRevision,
                ProgressRequested = _progressRequested,
                ResultKills = _resultKills,
                ResultTime = _resultPlayTime
            };
            s.RewardedElites.AddRange(_rewardedElites);
            s.Choices.Capacity = _choices.Count;
            foreach (var choice in _choices) { s.Choices.Add(skills.SaveChoice(choice)); }
            _stats.Capture(s); _clock.Capture(s); _deck.Capture(s); _rewards?.Capture(s); _roulette?.Capture(s, skills);
            return s;
        }
        public void RestoreRuntime(StageRuntimeState s, SkillController skills, GameDataStore data)
        {
            _speed = s.Speed; _beforePause = s.BeforePause;
            _pendingElites = s.PendingElites; _pendingCardSelections = s.PendingCards;
            _rewardedElites.Clear(); foreach (int id in s.RewardedElites) { _rewardedElites.Add(id); }
            _choices = s.Choices.Select(choice => skills.RestoreChoice(choice, data)).ToList();
            _pendingProgress = s.PendingProgress; _progressRevision = s.ProgressRevision; _progressRequested = s.ProgressRequested;
            _pendingRequest = s.PendingRequest; _resultKills = s.ResultKills; _resultPlayTime = s.ResultTime;
            _stats.Restore(s); _clock.Restore(s); _deck.Restore(s); _rewards?.Restore(s); _roulette?.Restore(s, skills, data);
            Time.timeScale = s.State == StageState.Playing ? _speed : 0f;
            ChangeState(s.State);
            if ((_pendingProgress != null || _progressRequested) && s.State != StageState.Submitting) { QueueProgress(); }
            if (s.State == StageState.Submitting && _pendingRequest != null) { Submit(AutoRetryCount).Forget(Debug.LogException); }
        }
    }

    public sealed partial class StageClock
    {
        internal void Capture(StageRuntimeState s) => s.Clock = ElapsedSeconds;
        internal void Restore(StageRuntimeState s) => ElapsedSeconds = s.Clock;
    }
    public sealed partial class BattleStats
    {
        internal void Capture(StageRuntimeState s)
        {
            s.Kills = Kills; s.ReachedWave = ReachedWave; s.PlayTime = PlayTime; s.Ended = IsEnded;
            s.DeadEnemyIds.AddRange(_deadEnemies); s.BuildLog.AddRange(BuildLog);
            foreach (var pair in _skillDamage) { s.SkillDamage.Add(pair.Key, pair.Value); }
        }
        internal void Restore(StageRuntimeState s)
        {
            Kills = s.Kills; ReachedWave = s.ReachedWave; PlayTime = s.PlayTime; IsEnded = s.Ended;
            _deadEnemies.Clear(); foreach (int id in s.DeadEnemyIds) { _deadEnemies.Add(id); }
            _skillDamage.Clear(); foreach (var pair in s.SkillDamage) { _skillDamage[pair.Key] = pair.Value; }
            BuildLog.Clear(); BuildLog.AddRange(s.BuildLog);
        }
    }
    public sealed partial class CardDeck
    {
        internal void Capture(StageRuntimeState s)
        { foreach (var pair in _picks) { s.GeneralPicks.Add(pair.Key, pair.Value); } }
        internal void Restore(StageRuntimeState s)
        { _picks.Clear(); foreach (var pair in s.GeneralPicks) { _picks[pair.Key] = pair.Value; } }
    }
    public sealed partial class StageRewardTracker
    {
        internal void Capture(StageRuntimeState s) => s.CompletedWaves = CompletedWaves;
        internal void Restore(StageRuntimeState s)
        { CompletedWaves = s.CompletedWaves; _stopped = s.State is StageState.Submitting or StageState.Finished; Refresh(); }
    }
    public sealed partial class EncounteredMonsters
    {
        public void CaptureRuntime(StageRuntimeState s) { s.Encountered.Clear(); s.Encountered.AddRange(_ids); }
        public void RestoreRuntime(StageRuntimeState s) { _ids.Clear(); _ids.AddRange(s.Encountered); }
    }
    public sealed partial class WaveProgress
    {
        public void CaptureRuntime(StageRuntimeState s)
        {
            s.GaugeKills = _kills; s.Waves.Clear(); s.Waves.Capacity = _waves.Count;
            foreach (var wave in _waves) { s.Waves.Add(new WaveRuntimeState { Index = wave.WaveIndex, Count = wave.EnemyCount, Final = wave.IsFinalWave }); }
        }
        public void RestoreRuntime(StageRuntimeState s)
        {
            _kills = s.GaugeKills; _waves.Clear();
            foreach (var wave in s.Waves) { _waves.Enqueue(new WaveStarted(wave.Index, wave.Count, wave.Final)); }
            if (_waves.Count > 0) { PublishGauge(); }
        }
    }
    public sealed partial class EliteRouletteSession
    {
        internal void Capture(StageRuntimeState s, SkillController skills)
        {
            s.RouletteCards = CardCount; s.RouletteCompleted = CompletedRolls;
            foreach (var slot in Slots) { s.RouletteSlots.Add(skills.SaveChoice(slot)); }
            s.RouletteWinner = PendingRound?.WinnerIndex ?? -1;
            foreach (var selected in _selectedCards) { s.RouletteSelected.Add(new SavedChoice { SkillId = selected.WeaponId ?? 0, CardId = selected.CardId }); }
        }
        internal void Restore(StageRuntimeState s, SkillController skills, GameDataStore data)
        {
            Slots = s.RouletteSlots.Select(choice => skills.RestoreChoice(choice, data)).ToArray();
            CardCount = s.RouletteCards; CompletedRolls = s.RouletteCompleted; _selectedCards.Clear();
            foreach (var selected in s.RouletteSelected) { _selectedCards.Add((selected.SkillId == 0 ? null : (int?)selected.SkillId, selected.CardId)); }
            PendingRound = s.RouletteWinner < 0 ? null : new EliteRouletteRound(Slots, s.RouletteWinner, Slots[s.RouletteWinner]);
        }
    }
    public partial class EnemySpawner
    {
        private int _checkpointWaveIndex;
        public SpawnRuntimeState CaptureRuntime()
        {
            var factory = _enemyViewFactory as EnemyFactory;
            var state = new SpawnRuntimeState
            {
                WaveIndex = _checkpointWaveIndex,
                Spawned = _spawnedCountInOnceSpawn,
                BurstSize = _currentBurstSize,
                NextEnemyId = factory?.NextEnemyId ?? 0,
                Elapsed = _elapsedTime,
                Interval = _nextSpawnInterval,
                Allowed = _isSpawningAllowed,
                Waiting = _isWaitingForNextSpawn,
                Repeat = _isRepeatBurst,
                SpecialWarned = _specialWarned,
                Final = _isFinalWave,
                FinalDone = _finalSpawnDone,
                AllClear = _allClearPublished,
            };
            state.Burst.AddRange(_burst);
            state.Enemies.Capacity = _activeEnemies.Count;
            foreach (var enemy in _activeEnemies) { if (!enemy.IsDead) { state.Enemies.Add(enemy.CaptureEnemy()); } }
            state.Pending.Capacity = _pendingSpawns.Count;
            foreach (var pending in _pendingSpawns)
            { state.Pending.Add(new PendingSpawnState { MonsterId = pending.MonsterId, Position = pending.Position, Summoned = pending.IsSummon }); }
            _projectiles.Capture(state.Projectiles);
            return state;
        }
        public void RestoreRuntime(SpawnRuntimeState s, StageDefinition stage)
        {
            if (_enemyViewFactory is not EnemyFactory factory) { throw new InvalidOperationException("이어하기는 런타임 적 팩터리가 필요합니다."); }
            foreach (var enemy in _activeEnemies) { enemy.SpawnRequested -= OnSpawnRequested; UnityEngine.Object.Destroy(enemy.gameObject); }
            _activeEnemies.Clear(); _pendingSpawns.Clear();
            _checkpointWaveIndex = s.WaveIndex; _waveSpawns = stage.Waves[s.WaveIndex].Spawns;
            _spawnedCountInOnceSpawn = s.Spawned; _currentBurstSize = s.BurstSize; _elapsedTime = s.Elapsed; _nextSpawnInterval = s.Interval;
            _isSpawningAllowed = s.Allowed; _isWaitingForNextSpawn = s.Waiting; _isRepeatBurst = s.Repeat; _specialWarned = s.SpecialWarned;
            _isFinalWave = s.Final; _finalSpawnDone = s.FinalDone; _allClearPublished = s.AllClear;
            _burst.Clear(); _burst.AddRange(s.Burst);
            foreach (var saved in s.Enemies) { Track(factory.Restore(saved, this)); }
            factory.NextEnemyId = s.NextEnemyId;
            foreach (var pending in s.Pending) { _pendingSpawns.Enqueue(new EnemySpawnRequest(pending.MonsterId, pending.Position, pending.Summoned)); }
            _projectiles.Restore(s.Projectiles, factory);
        }
        public IEnemyTarget SavedEnemy(int id) => _activeEnemies.FirstOrDefault(e => e.Id == id);
    }
    public partial class EnemyProjectileSystem
    {
        internal void Capture(List<EnemyProjectileState> result)
        {
            result.Clear(); result.Capacity = _projectiles.Count;
            foreach (var projectile in _projectiles) { if (!projectile.IsDone) { result.Add(projectile.Capture()); } }
        }
        internal void Restore(List<EnemyProjectileState> states, EnemyFactory factory)
        {
            _projectiles.Clear();
            foreach (var state in states)
            {
                var model = Fire(state.Position, state.Damage, state.Speed, state.MonsterId);
                factory.RestoreProjectileVisual(state.MonsterId, model);
            }
        }
    }
}
namespace Game.Core.Stage
{
    public sealed partial class WaveManager
    {
        public void CaptureRuntime(Game.Core.StageRuntimeState s) => s.WaveIndex = _currentWaveIndex;
        public void RestoreRuntime(Game.Core.StageRuntimeState s) => _currentWaveIndex = s.WaveIndex;
    }
}
namespace Game.Core.Defense
{
    public sealed partial class Wall
    {
        public void CaptureRuntime(Game.Core.StageRuntimeState s) { s.WallHp = CurrentHp; s.WallMaxHp = MaxHp; }
        public void RestoreRuntime(Game.Core.StageRuntimeState s)
        {
            MaxHp = s.WallMaxHp; CurrentHp = s.WallHp; IsDestroyed = CurrentHp <= 0;
            ApplySprite(); _hpChangedPublisher.Publish(new WallHpChanged(CurrentHp, MaxHp));
        }
    }
}
