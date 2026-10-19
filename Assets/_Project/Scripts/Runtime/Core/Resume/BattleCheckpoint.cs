using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>한 기기에서만 보관하는 전투 실행 상태. 계정 진행도나 보상 지급 기록과 분리한다.</summary>
    [Serializable]
    public sealed class BattleCheckpoint
    {
        public int Version = 1;
        public string BattleId;
        public int StageId;
        public string RandomState;
        public StageRuntimeState Stage;
        public SpawnRuntimeState Spawns;
        public List<SkillRuntimeState> Skills = new();
    }

    public interface IBattleCheckpointStore
    {
        BattleCheckpoint Load();
        /// <summary>독립 스냅샷을 저장하고 완료까지 기다린다.</summary>
        void Save(BattleCheckpoint checkpoint);
        /// <summary>호출자가 더 이상 변경하지 않을 독립 스냅샷을 넘긴다. 최신 대기 한 건만 유지한다.</summary>
        void QueueSave(BattleCheckpoint checkpoint);
        /// <summary>접수한 최신 저장의 완료를 기다린다. 종료·백그라운드 전환에 사용한다.</summary>
        void Flush();
        void Clear();
    }

    [Serializable]
    public sealed class StageRuntimeState
    {
        public StageState State, BeforePause;
        public float Speed, Clock, PlayTime;
        public int Kills, ReachedWave, WallHp, WallMaxHp, WaveIndex, GaugeKills, CompletedWaves;
        public bool Ended;
        public string ProgressSourceRevision;
        public List<int> DeadEnemyIds = new(), Encountered = new(), RewardedElites = new();
        public Dictionary<int, long> SkillDamage = new();
        public List<string> BuildLog = new();
        public Dictionary<string, int> GeneralPicks = new();
        public int PendingElites, PendingCards;
        public List<SavedChoice> Choices = new(), RouletteSlots = new();
        public int RouletteCards, RouletteCompleted, RouletteWinner = -1;
        public List<SavedChoice> RouletteSelected = new();
        public List<WaveRuntimeState> Waves = new();
        public SubmitResultRequest PendingRequest;
        public BattleProgressRequest PendingProgress;
        public long ProgressRevision;
        public bool ProgressRequested;
        public bool PendingProgressAbandon;
        public int ResultKills;
        public float ResultTime;
    }

    [Serializable]
    public sealed class SavedChoice
    {
        public int SkillId;
        public string CardId;
        public bool NewWeapon, General;
    }

    [Serializable]
    public sealed class SpawnRuntimeState
    {
        public int WaveIndex, Spawned, BurstSize, NextEnemyId;
        public float Elapsed, Interval;
        public bool Allowed, Waiting, Repeat, SpecialWarned, Final, FinalDone, AllClear;
        public List<int> Burst = new();
        public List<EnemyRuntimeState> Enemies = new();
        public List<PendingSpawnState> Pending = new();
        public List<EnemyProjectileState> Projectiles = new();
    }

    [Serializable]
    public sealed class PendingSpawnState
    {
        public int MonsterId;
        public Vector2 Position;
        public bool Summoned;
    }

    [Serializable]
    public sealed class EnemyProjectileState
    {
        public Vector2 Position;
        public float Speed;
        public int Damage, MonsterId;
    }

    [Serializable]
    public sealed class EnemyRuntimeState
    {
        public int Id, MonsterId, Hp;
        public bool Summoned;
        public Vector2 Position, Displacement, Pull;
        public float AttackTimer, BurstTimer;
        public int BurstRemaining;
        public EnemyStatusState Status;
        public List<PassiveRuntimeState> Passives = new();
    }

    [Serializable]
    public sealed class PassiveRuntimeState
    {
        public string Kind;
        public bool Used, Active;
        public int Count;
        public float Timer, Remaining, Carry;
    }

    [Serializable]
    public sealed class EnemyStatusState
    {
        public float Freeze, Paralysis, Stun, Slow, SlowRatio, Vulnerability, VulnerabilityRatio;
        public float FreezeDotRatio, FreezeDotElapsed, Burn, BurnDamage, BurnElapsed, BurnHpRatio;
        public BurnExplosionState BurnExplosion;
        public List<TimedStatusState> Immunities = new(), Boosts = new();
        public List<FrostbiteState> Frostbite = new();
    }

    [Serializable]
    public sealed class BurnExplosionState
    {
        public float Radius, Damage;
    }

    [Serializable]
    public sealed class TimedStatusState
    {
        public int Passive;
        public StatusImmunity Flags;
        public float Multiplier, Remaining;
    }

    [Serializable]
    public sealed class FrostbiteState
    {
        public float Damage, Remaining, Elapsed;
    }

    [Serializable]
    public sealed class SkillConfigState
    {
        public float[] Stats;
        public CastType Type;
        public ProjectilePath Path;
        public float Range;
        public List<EffectDef> Effects = new();
        public Dictionary<int, Dictionary<string, float>> Children = new();
        public Dictionary<int, List<EffectDef>> Overlays = new();
    }

    [Serializable]
    public sealed class SkillRuntimeState
    {
        public int SkillId, Level, CastIndex, RemainingCasts;
        public bool Child;
        public float Cooldown, RepeatTimer;
        public Dictionary<string, int> Acquired = new();
        public SkillConfigState Config;
        public List<AttackRuntimeState> Attacks = new();
        public double ReserveCooldown, ReserveTimer;
        public int ReserveCasts;
        public List<int> BurstTargets = new();
    }

    [Serializable]
    public sealed class AttackRuntimeState
    {
        public string Kind;
        public SkillConfigState Config;
        public int SkillId;
        public Element Element;
        public CastType SourceCastType;
        public Vector3 Position, Direction, Scale;
        public float Speed, Lifetime, Elapsed, Radius, Damage, Interval, SincePulse, MoveSpeed, Pull;
        public int TargetId, IgnoreId, RemainingHits;
        public List<int> Visited = new();
        public List<Vector2> Points = new();
        public float Length, Width, FocusBonus, BaseDamage, FocusRampMax, BlastRadius, BlastRatio, CoreRadius, CoreFreezeScale;
        public int MainHits, Refractions, CoreHits;
        public bool FocusAim, CoreDone, Started, HitDone, Secondary;
        public List<AnimatorRuntimeState> Animators = new();
        public Vector2? MoveTarget;
        public double FieldRemaining, FieldElapsed;
        public float SlowRatio, ReservationDamage, ReservationRemaining;
    }
    [Serializable]
    public sealed class AnimatorRuntimeState
    {
        public int Index, Layer, Hash;
        public float Time, Speed;
    }

    [Serializable]
    public sealed class WaveRuntimeState
    {
        public int Index, Count;
        public bool Final;
    }

}
