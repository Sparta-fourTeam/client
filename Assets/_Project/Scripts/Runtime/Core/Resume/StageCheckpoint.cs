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
                RewardedElites = _rewardedElites.ToList(),
                Choices = _choices.Select(skills.SaveChoice).ToList(),
                PendingRequest = _pendingRequest,
                ResultKills = _resultKills,
                ResultTime = _resultPlayTime
            };
            _stats.Capture(s); _clock.Capture(s); _deck.Capture(s); _rewards?.Capture(s); _roulette?.Capture(s, skills);
            return s;
        }
        public void RestoreRuntime(StageRuntimeState s, SkillController skills, GameDataStore data)
        {
            _speed = s.Speed; _beforePause = s.BeforePause;
            _pendingElites = s.PendingElites; _pendingCardSelections = s.PendingCards;
            _rewardedElites.Clear(); foreach (int id in s.RewardedElites) { _rewardedElites.Add(id); }
            _choices = s.Choices.Select(choice => skills.RestoreChoice(choice, data)).ToList();
            _pendingRequest = s.PendingRequest; _resultKills = s.ResultKills; _resultPlayTime = s.ResultTime;
            _stats.Restore(s); _clock.Restore(s); _deck.Restore(s); _rewards?.Restore(s); _roulette?.Restore(s, skills, data);
            Time.timeScale = s.State == StageState.Playing ? _speed : 0f;
            ChangeState(s.State);
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
            s.DeadEnemyIds = _deadEnemies.ToList(); s.SkillDamage = new Dictionary<int, long>(_skillDamage); s.BuildLog = BuildLog.ToList();
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
        internal void Capture(StageRuntimeState s) => s.GeneralPicks = new Dictionary<string, int>(_picks);
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
        public void CaptureRuntime(StageRuntimeState s) => s.Encountered = _ids.ToList();
        public void RestoreRuntime(StageRuntimeState s) { _ids.Clear(); _ids.AddRange(s.Encountered); }
    }
    public sealed partial class WaveProgress
    {
        public void CaptureRuntime(StageRuntimeState s)
        { s.GaugeKills = _kills; s.Waves = _waves.Select(w => new WaveRuntimeState { Index = w.WaveIndex, Count = w.EnemyCount, Final = w.IsFinalWave }).ToList(); }
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
            s.RouletteSlots = Slots.Select(skills.SaveChoice).ToList(); s.RouletteCards = CardCount; s.RouletteCompleted = CompletedRolls;
            s.RouletteWinner = PendingRound?.WinnerIndex ?? -1;
            s.RouletteSelected = _selectedCards.Select(p => new SavedChoice { SkillId = p.WeaponId ?? 0, CardId = p.CardId }).ToList();
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
            return new SpawnRuntimeState
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
                Burst = _burst.ToList(),
                Enemies = _activeEnemies.Where(e => !e.IsDead).Select(e => e.CaptureEnemy()).ToList(),
                Pending = _pendingSpawns.Select(p => new PendingSpawnState { MonsterId = p.MonsterId, Position = p.Position, Summoned = p.IsSummon }).ToList(),
                Projectiles = _projectiles.Capture()
            };
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
        internal List<EnemyProjectileState> Capture() => _projectiles.Where(p => !p.IsDone).Select(p => p.Capture()).ToList();
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
