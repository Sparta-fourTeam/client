using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    public partial class EnemyModel
    {
        internal int MonsterId { get; set; }
        internal EnemyRuntimeState CaptureEnemy() => new()
        {
            Id = Id,
            MonsterId = MonsterId,
            Hp = Hp,
            Summoned = _isSummoned,
            AttackTimer = _attackTimer,
            BurstTimer = _burstTimer,
            BurstRemaining = _burstRemaining,
            Status = _status.Capture(_passives),
            Passives = (_passives ?? Array.Empty<IPassive>()).Select(CapturePassive).ToList()
        };
        internal void RestoreEnemy(EnemyRuntimeState state, IEnemyTargetProvider targets)
        {
            Hp = state.Hp; _attackTimer = state.AttackTimer; _burstTimer = state.BurstTimer; _burstRemaining = state.BurstRemaining;
            if (state.Passives.Count != (_passives?.Count ?? 0)) { throw new InvalidOperationException("몬스터 패시브 구성이 바뀌었습니다."); }
            for (int i = 0; i < state.Passives.Count; i++)
            {
                switch (_passives[i])
                {
                    case ShieldPassive p: p.Restore(state.Passives[i]); break;
                    case LowHpPassive p: p.Restore(state.Passives[i]); break;
                    case SpawnPassive p: p.Restore(state.Passives[i]); break;
                    case SpeedBoostPassive p: p.Restore(state.Passives[i]); break;
                    case EvadePassive p: p.Restore(state.Passives[i]); break;
                }
            }
            _status.Restore(state.Status, _passives, targets);
        }
        private static PassiveRuntimeState CapturePassive(IPassive passive) => passive switch
        {
            ShieldPassive p => p.Capture(),
            LowHpPassive p => p.Capture(),
            SpawnPassive p => p.Capture(),
            SpeedBoostPassive p => p.Capture(),
            EvadePassive p => p.Capture(),
            _ => new PassiveRuntimeState { Kind = passive.GetType().Name }
        };
    }

    public partial class Enemy
    {
        internal EnemyRuntimeState CaptureEnemy()
        {
            var state = _enemyModel.CaptureEnemy(); state.Position = Position;
            state.Displacement = _pendingDisplacement; state.Pull = _pendingPull;
            return state;
        }
        internal void RestoreEnemy(EnemyRuntimeState state, IEnemyTargetProvider targets)
        {
            transform.position = state.Position; _previousPosition = _lastPosition = state.Position;
            _pendingDisplacement = state.Displacement; _pendingPull = state.Pull;
            _enemyModel.RestoreEnemy(state, targets); _lastHp = state.Hp;
        }
    }

    public sealed partial class ShieldPassive
    {
        internal PassiveRuntimeState Capture() => new() { Kind = nameof(ShieldPassive), Used = _used, Active = _active, Count = _hitsLeft, Remaining = _remaining };
        internal void Restore(PassiveRuntimeState s) { _used = s.Used; _active = s.Active; _hitsLeft = s.Count; _remaining = s.Remaining; }
    }
    public sealed partial class LowHpPassive
    {
        internal PassiveRuntimeState Capture() => new() { Kind = nameof(LowHpPassive), Used = _used, Active = _active, Remaining = _remaining, Carry = _healCarry };
        internal void Restore(PassiveRuntimeState s) { _used = s.Used; _active = s.Active; _remaining = s.Remaining; _healCarry = s.Carry; }
    }
    public sealed partial class SpawnPassive
    {
        internal PassiveRuntimeState Capture() => new() { Kind = nameof(SpawnPassive), Timer = _timer, Count = _spawned };
        internal void Restore(PassiveRuntimeState s) { _timer = s.Timer; _spawned = s.Count; }
    }
    public sealed partial class SpeedBoostPassive
    {
        internal PassiveRuntimeState Capture() => new() { Kind = nameof(SpeedBoostPassive), Timer = _timer };
        internal void Restore(PassiveRuntimeState s) => _timer = s.Timer;
    }
    public sealed partial class EvadePassive
    {
        internal PassiveRuntimeState Capture() => new() { Kind = nameof(EvadePassive), Timer = _window };
        internal void Restore(PassiveRuntimeState s) => _window = s.Timer;
    }

    public sealed partial class EnemyStatus
    {
        private static int PassiveIndex(object source, IReadOnlyList<IPassive> passives)
        {
            for (int i = 0; i < (passives?.Count ?? 0); i++) { if (ReferenceEquals(source, passives[i])) { return i; } }
            throw new InvalidOperationException("저장할 수 없는 상태이상 출처입니다.");
        }
        internal EnemyStatusState Capture(IReadOnlyList<IPassive> passives) => new()
        {
            Freeze = FreezeRemaining,
            Paralysis = ParalysisRemaining,
            Stun = StunRemaining,
            Slow = SlowRemaining,
            SlowRatio = slowRatio,
            Vulnerability = VulnerabilityRemaining,
            VulnerabilityRatio = VulnerabilityRatio,
            FreezeDotRatio = freezeDotRatio,
            FreezeDotElapsed = freezeDotElapsed,
            Burn = BurnRemaining,
            BurnDamage = burnDamage,
            BurnElapsed = burnElapsed,
            BurnHpRatio = burnMaxHpRatio,
            BurnExplosion = (burnOnDeath?.Target as SavedBurnExplosion)?.Capture(),
            Frostbite = frostbite.Select(s => new FrostbiteState { Damage = s.Damage, Remaining = s.Remaining, Elapsed = s.Elapsed }).ToList(),
            Immunities = _grants.Select(s => new TimedStatusState { Passive = PassiveIndex(s.Source, passives), Flags = s.Flags, Remaining = s.Remaining }).ToList(),
            Boosts = _boosts.Select(s => new TimedStatusState { Passive = PassiveIndex(s.Source, passives), Multiplier = s.Multiplier, Remaining = s.Remaining }).ToList()
        };
        internal void Restore(EnemyStatusState s, IReadOnlyList<IPassive> passives, IEnemyTargetProvider targets)
        {
            FreezeRemaining = s.Freeze; ParalysisRemaining = s.Paralysis; StunRemaining = s.Stun;
            SlowRemaining = s.Slow; slowRatio = s.SlowRatio; VulnerabilityRemaining = s.Vulnerability; VulnerabilityRatio = s.VulnerabilityRatio;
            freezeDotRatio = s.FreezeDotRatio; freezeDotElapsed = s.FreezeDotElapsed;
            BurnRemaining = s.Burn; burnDamage = s.BurnDamage; burnElapsed = s.BurnElapsed; burnMaxHpRatio = s.BurnHpRatio;
            burnOnDeath = s.BurnExplosion == null ? null : new SavedBurnExplosion(targets, s.BurnExplosion.Radius, s.BurnExplosion.Damage).Invoke;
            frostbite.Clear(); foreach (var stack in s.Frostbite) { frostbite.Add(new FrostbiteStack { Damage = stack.Damage, Remaining = stack.Remaining, Elapsed = stack.Elapsed }); }
            _grants.Clear(); foreach (var grant in s.Immunities) { _grants.Add(new ImmunityGrant { Source = passives[grant.Passive], Flags = grant.Flags, Remaining = grant.Remaining }); }
            _boosts.Clear(); foreach (var boost in s.Boosts) { _boosts.Add(new SpeedBoost { Source = passives[boost.Passive], Multiplier = boost.Multiplier, Remaining = boost.Remaining }); }
            areaSlows.Clear(); // 지속 장판이 자기 출처로 다시 적용한다.
        }
    }

    public partial class EnemyProjectileModel
    {
        internal int MonsterId;
        internal EnemyProjectileState Capture() => new() { Position = Position, Speed = _speed, Damage = _damage, MonsterId = MonsterId };
    }

    public partial class EnemyFactory
    {
        internal int NextEnemyId { get => _nextEnemyId; set => _nextEnemyId = value; }
        internal Enemy Restore(EnemyRuntimeState state, IEnemyTargetProvider targets)
        {
            int next = _nextEnemyId;
            _nextEnemyId = state.Id - 1;
            var enemy = Create(state.MonsterId, state.Position, state.Summoned);
            _nextEnemyId = Math.Max(next, state.Id);
            enemy.RestoreEnemy(state, targets);
            return enemy;
        }
        internal void RestoreProjectileVisual(int monsterId, EnemyProjectileModel model)
        {
            if (monsterId <= 0) { return; }
            var prefab = _assets.GetOrThrow(monsterId).projectilePrefab;
            if (prefab != null) { UnityEngine.Object.Instantiate(prefab).Bind(model); }
        }
    }
}
